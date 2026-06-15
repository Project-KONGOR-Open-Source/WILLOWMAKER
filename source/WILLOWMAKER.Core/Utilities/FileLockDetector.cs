namespace WILLOWMAKER.Core.Utilities;

/// <summary>
///     A single process holding a lock on a file, identified by its process ID and friendly application name.
/// </summary>
public sealed record FileLockingProcess(int ProcessID, string ApplicationName);

/// <summary>
///     Provides methods for detecting which processes are currently holding file locks.
/// </summary>
public static class FileLockDetector
{
    [StructLayout(LayoutKind.Sequential)]
    private struct RM_UNIQUE_PROCESS
    {
        public int dwProcessID;

        public FILETIME FileTime;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct RM_PROCESS_INFO
    {
        public RM_UNIQUE_PROCESS Process;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
        public string strAppName;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
        public string strServiceShortName;

        public int ApplicationType;

        public uint AppStatus;

        public uint TSSessionID;

        [MarshalAs(UnmanagedType.Bool)]
        public bool bRestartable;
    }

    [DllImport("rstrtmgr.dll", CharSet = CharSet.Unicode)]
    private static extern int RmStartSession(out uint pSessionHandle, uint dwSessionFlags, string strSessionKey);

    [DllImport("rstrtmgr.dll")]
    private static extern int RmEndSession(uint dwSessionHandle);

    [DllImport("rstrtmgr.dll", CharSet = CharSet.Unicode)]
    private static extern int RmRegisterResources(uint dwSessionHandle, uint nFiles, string[] rgsFileNames, uint nApplications, RM_UNIQUE_PROCESS[]? rgApplications, uint nServices, string[]? rgsServiceNames);

    [DllImport("rstrtmgr.dll", CharSet = CharSet.Unicode)]
    private static extern int RmGetList(uint dwSessionHandle, out uint pnProcInfoNeeded, ref uint pnProcInfo, [In, Out] RM_PROCESS_INFO[]? rgAffectedApps, out uint lpdwRebootReasons);

    /// <summary>
    ///     Retrieves the list of processes that currently hold a lock on the specified file.
    ///     Each supported operating system is served by its native, in-box mechanism, so no external tooling has to be installed.
    /// </summary>
    public static List<FileLockingProcess> GetLockingProcesses(string filePath)
    {
        if (OperatingSystem.IsWindows())
            return GetLockingProcessesWindows(filePath);

        if (OperatingSystem.IsLinux())
            return GetLockingProcessesLinux(filePath);

        if (OperatingSystem.IsMacOS())
            return GetLockingProcessesMacOS(filePath);

        return new List<FileLockingProcess>();
    }

    private static List<FileLockingProcess> GetLockingProcessesWindows(string filePath)
    {
        List<FileLockingProcess> processes = new List<FileLockingProcess>();

        string sessionKey = Guid.NewGuid().ToString();

        int result = RmStartSession(out uint sessionHandle, 0, sessionKey);

        if (result != 0)
        {
            return processes;
        }

        try
        {
            string[] resources = [ filePath ];

            result = RmRegisterResources(sessionHandle, (uint) resources.Length, resources, 0, null, 0, null);

            if (result != 0)
            {
                return processes;
            }

            uint procInfoNeeded = 0;
            uint procInfo = 0;

            result = RmGetList(sessionHandle, out procInfoNeeded, ref procInfo, null, out uint rebootReasons);

            // 234 Represents ERROR_MORE_DATA Which Is Expected When Querying The Size Of The Process List
            if (result == 234)
            {
                RM_PROCESS_INFO[] processInfos = new RM_PROCESS_INFO[procInfoNeeded];

                procInfo = procInfoNeeded;

                result = RmGetList(sessionHandle, out procInfoNeeded, ref procInfo, processInfos, out rebootReasons);

                if (result == 0)
                {
                    for (int processIndex = 0; processIndex < procInfo; processIndex++)
                    {
                        // The Restart Manager Already Reports A Friendly Application Name And The Owning PID, So No Secondary Process Lookup Is Needed
                        RM_PROCESS_INFO processInfo = processInfos[processIndex];

                        processes.Add(new FileLockingProcess(processInfo.Process.dwProcessID, processInfo.strAppName));
                    }
                }
            }
        }

        finally
        {
            RmEndSession(sessionHandle);
        }

        return processes;
    }

    private static List<FileLockingProcess> GetLockingProcessesLinux(string filePath)
    {
        List<FileLockingProcess> processes = new List<FileLockingProcess>();

        // The Linux Kernel Exposes Every Process's Open File Descriptors As Symbolic Links Under "/proc/<pid>/fd", So The Lock Holder Can Be Found By Reading The "procfs" Filesystem Directly Without Any External Tooling
        foreach (string processDirectory in Directory.EnumerateDirectories("/proc"))
        {
            if (int.TryParse(Path.GetFileName(processDirectory), out int processID) is false)
                continue;

            try
            {
                foreach (string fileDescriptor in Directory.EnumerateFileSystemEntries($"/proc/{processID}/fd"))
                {
                    FileSystemInfo? target = File.ResolveLinkTarget(fileDescriptor, returnFinalTarget: false);

                    if (target is not null && string.Equals(target.FullName, filePath, StringComparison.Ordinal))
                    {
                        processes.Add(new FileLockingProcess(processID, ReadLinuxProcessName(processID)));

                        break;
                    }
                }
            }

            catch
            {
                // Swallowed Deliberately: The Process Might Have Exited, Or Its File Descriptors Might Belong To Another User And Be Unreadable Without Elevated Privileges
            }
        }

        return processes;
    }

    private static string ReadLinuxProcessName(int processID)
    {
        try
        {
            // "/proc/<pid>/comm" Holds The Process's Command Name On A Single Line
            return File.ReadAllText($"/proc/{processID}/comm").Trim();
        }

        catch
        {
            return "Unknown Process";
        }
    }

    private static List<FileLockingProcess> GetLockingProcessesMacOS(string filePath)
    {
        List<FileLockingProcess> processes = new List<FileLockingProcess>();

        try
        {
            // macOS Has No "procfs", But "lsof" Ships As Part Of The Base System On Every Release, So It Is A Reliable In-Box Source
            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                FileName = "lsof",
                Arguments = $@"-F pcn -- ""{filePath}""",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using Process? process = Process.Start(startInfo);

            if (process is not null)
            {
                string output = process.StandardOutput.ReadToEnd();
                process.WaitForExit();

                // "lsof -F" Emits One Field Per Line, Each Prefixed With A Type Character: "p" For The Process ID And "c" For The Command Name
                int currentProcessID = 0;

                foreach (string line in output.Split('\n', StringSplitOptions.RemoveEmptyEntries))
                {
                    char fieldType = line[0];
                    string fieldValue = line[1..];

                    if (fieldType is 'p' && int.TryParse(fieldValue, out int parsedProcessID))
                    {
                        currentProcessID = parsedProcessID;
                    }

                    else if (fieldType is 'c' && currentProcessID is not 0)
                    {
                        processes.Add(new FileLockingProcess(currentProcessID, fieldValue));
                    }
                }
            }
        }

        catch
        {
            // Swallowed Deliberately: Permissions Might Be Insufficient To Inspect Processes Owned By Another User
        }

        return processes;
    }
}

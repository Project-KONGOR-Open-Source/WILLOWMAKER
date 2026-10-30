using System.Runtime.Versioning;
using System.Text.Json;

using TUnit.Core.Enums;

namespace WILLOWMAKER.Tests.Services.Synchronisation;

/// <summary>
///     Verifies the synchronisation engine's local reconciliation behaviour: up-front partial cleanup, mirror deletion, target exclusions, up-to-date detection, the execute permission, and the manifest's shape.
///     The scenarios are constructed so that no downloads succeed, which keeps the tests free of network access.
/// </summary>
public sealed class ContentBrokerTests
{
    private static string CreateTemporaryDirectory()
    {
        string directory = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());

        Directory.CreateDirectory(directory);

        return directory;
    }

    // Readable By Everyone And Writable By Its Owner, As A Download Creates A File, With No Execute Bit
    private const UnixFileMode DownloadedMode = UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.GroupRead | UnixFileMode.OtherRead;

    private static Manifest CreateManifest(IReadOnlyDictionary<string, ManifestEntry>? files = null, IReadOnlyList<string>? excludeFromTarget = null) => new ()
    {
        VersionResources = "5.26.12.20",
        VersionExecutable = "5.26.12.24",
        HashAlgorithm = "SHA-256",
        ExcludeFromSource = [],
        ExcludeFromTarget = excludeFromTarget ?? [],
        Files = files ?? new Dictionary<string, ManifestEntry>()
    };

    private static ManifestEntry CreateEntry(byte[] content, bool executable = false)
        => new () { Size = content.Length, Hash = Convert.ToHexStringLower(SHA256.HashData(content)), Executable = executable };

    // "Progress<T>" Posts Its Callbacks Asynchronously, So Events Could Arrive After The Synchronisation Returns; Recording Synchronously Keeps The Order Deterministic
    private sealed class RecordingProgress : IProgress<SynchronisationEvent>
    {
        public List<SynchronisationEvent> Events { get; } = [];

        public void Report(SynchronisationEvent value) => Events.Add(value);
    }

    [Test]
    public async Task A_Leftover_Partial_File_Is_Deleted_Before_The_Plan_Is_Reported()
    {
        string directory = CreateTemporaryDirectory();

        try
        {
            string partialPath = Path.Combine(directory, "leftover.partial");

            File.WriteAllText(partialPath, "interrupted download");

            RecordingProgress recorder = new ();

            SynchronisationSummary summary = await ContentBroker.Synchronise(CreateManifest(), "was", directory, progress: recorder);

            int deletedIndex = recorder.Events.FindIndex(synchronisationEvent => synchronisationEvent.Kind is SynchronisationEventKind.Deleted);
            int planIndex    = recorder.Events.FindIndex(synchronisationEvent => synchronisationEvent.Kind is SynchronisationEventKind.PlanReady);

            using (Assert.Multiple())
            {
                await Assert.That(File.Exists(partialPath)).IsFalse();
                await Assert.That(deletedIndex).IsGreaterThanOrEqualTo(0);
                await Assert.That(deletedIndex).IsLessThan(planIndex);
                await Assert.That(recorder.Events[planIndex].Plan).IsNotNull();
                await Assert.That(recorder.Events[planIndex].Plan?.FilesToDelete).IsEqualTo(1);
                await Assert.That(summary.FilesDeleted).IsEqualTo(1);
                await Assert.That(summary.FilesFailed).IsEqualTo(0);
            }
        }

        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Test]
    public async Task A_Local_File_Not_In_The_Manifest_Is_Deleted()
    {
        string directory = CreateTemporaryDirectory();

        try
        {
            string stalePath = Path.Combine(directory, "stale.txt");

            File.WriteAllText(stalePath, "no longer distributed");

            SynchronisationSummary summary = await ContentBroker.Synchronise(CreateManifest(), "was", directory);

            using (Assert.Multiple())
            {
                await Assert.That(File.Exists(stalePath)).IsFalse();
                await Assert.That(summary.FilesDeleted).IsEqualTo(1);
                await Assert.That(summary.FilesFailed).IsEqualTo(0);
            }
        }

        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Test]
    public async Task A_Local_File_Matching_A_Target_Exclusion_Is_Preserved()
    {
        string directory = CreateTemporaryDirectory();

        try
        {
            string protectedPath = Path.Combine(directory, "COMPEL.json");

            File.WriteAllText(protectedPath, "operator configuration");

            SynchronisationSummary summary = await ContentBroker.Synchronise(CreateManifest(excludeFromTarget: [ "COMPEL*" ]), "was", directory);

            using (Assert.Multiple())
            {
                await Assert.That(File.Exists(protectedPath)).IsTrue();
                await Assert.That(summary.FilesDeleted).IsEqualTo(0);
                await Assert.That(summary.FilesFailed).IsEqualTo(0);
            }
        }

        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Test]
    public async Task An_Up_To_Date_File_Is_Neither_Downloaded_Nor_Deleted()
    {
        string directory = CreateTemporaryDirectory();

        try
        {
            byte[] content = Encoding.UTF8.GetBytes("distributed content");

            string localPath = Path.Combine(directory, "data.bin");

            File.WriteAllBytes(localPath, content);

            Dictionary<string, ManifestEntry> files = new ()
            {
                ["data.bin"] = CreateEntry(content)
            };

            SynchronisationSummary summary = await ContentBroker.Synchronise(CreateManifest(files), "was", directory);

            using (Assert.Multiple())
            {
                await Assert.That(File.Exists(localPath)).IsTrue();
                await Assert.That(summary.FilesUpToDate).IsEqualTo(1);
                await Assert.That(summary.FilesDownloaded).IsEqualTo(0);
                await Assert.That(summary.FilesDeleted).IsEqualTo(0);
                await Assert.That(summary.FilesFailed).IsEqualTo(0);
            }
        }

        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Test]
    public async Task A_Manifest_In_The_Old_Shape_Is_Refused()
    {
        const string oldManifest = """{"version": "4.10.1.20261029", "hashAlgorithm": "SHA-256", "excludeFromSource": ["manifest.json"], "excludeFromTarget": [], "files": {"hon.exe": {"size": 1, "hash": "00"}}}""";

        await Assert.That(() => JsonSerializer.Deserialize(oldManifest, ManifestJSONContext.Default.Manifest)).Throws<JsonException>();
    }

    [Test]
    public async Task A_Flagged_File_That_Failed_To_Download_Is_Counted_Once()
    {
        string directory = CreateTemporaryDirectory();

        try
        {
            Dictionary<string, ManifestEntry> files = new ()
            {
                ["hon"] = CreateEntry(Encoding.UTF8.GetBytes("a program that is not present locally"), executable: true)
            };

            RecordingProgress recorder = new ();

            // Nothing Listens On The Discard Port, So The Connection Is Refused At Once
            SynchronisationSummary summary = await ContentBroker.Synchronise(CreateManifest(files), "was", directory, baseURL: "http://127.0.0.1:9/", progress: recorder);

            using (Assert.Multiple())
            {
                await Assert.That(summary.FilesFailed).IsEqualTo(1);
                await Assert.That(recorder.Events.Any(synchronisationEvent => synchronisationEvent.Kind is SynchronisationEventKind.PermissionFailed)).IsFalse();
            }
        }

        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Test]
    [ExcludeOn(OS.Windows)]
    [UnsupportedOSPlatform("windows")]
    public async Task An_Up_To_Date_Flagged_File_Gains_The_Execute_Permission()
    {
        string directory = CreateTemporaryDirectory();

        try
        {
            byte[] content = Encoding.UTF8.GetBytes("a program");

            string localPath = Path.Combine(directory, "hon");

            File.WriteAllBytes(localPath, content);
            File.SetUnixFileMode(localPath, DownloadedMode);

            SynchronisationSummary summary = await ContentBroker.Synchronise(CreateManifest(new Dictionary<string, ManifestEntry> { ["hon"] = CreateEntry(content, executable: true) }), "was", directory);

            UnixFileMode mode = File.GetUnixFileMode(localPath);

            using (Assert.Multiple())
            {
                await Assert.That(summary.FilesFailed).IsEqualTo(0);
                await Assert.That(mode.HasFlag(UnixFileMode.UserExecute)).IsTrue();
                await Assert.That(mode.HasFlag(UnixFileMode.GroupExecute)).IsTrue();
                await Assert.That(mode.HasFlag(UnixFileMode.OtherExecute)).IsTrue();
            }
        }

        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Test]
    [ExcludeOn(OS.Windows)]
    [UnsupportedOSPlatform("windows")]
    public async Task A_File_Without_The_Flag_Keeps_Its_Mode()
    {
        string directory = CreateTemporaryDirectory();

        try
        {
            byte[] content = Encoding.UTF8.GetBytes("a resource");

            string localPath = Path.Combine(directory, "readme.txt");

            File.WriteAllBytes(localPath, content);
            File.SetUnixFileMode(localPath, DownloadedMode);

            await ContentBroker.Synchronise(CreateManifest(new Dictionary<string, ManifestEntry> { ["readme.txt"] = CreateEntry(content) }), "was", directory);

            await Assert.That(File.GetUnixFileMode(localPath)).IsEqualTo(DownloadedMode);
        }

        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Test]
    [ExcludeOn(OS.Windows)]
    [UnsupportedOSPlatform("windows")]
    public async Task A_Flagged_File_Excluded_From_The_Target_Keeps_Its_Mode()
    {
        string directory = CreateTemporaryDirectory();

        try
        {
            byte[] content = Encoding.UTF8.GetBytes("the launcher");

            string localPath = Path.Combine(directory, "COMPEL");

            File.WriteAllBytes(localPath, content);
            File.SetUnixFileMode(localPath, DownloadedMode);

            Dictionary<string, ManifestEntry> files = new () { ["COMPEL"] = CreateEntry(content, executable: true) };

            await ContentBroker.Synchronise(CreateManifest(files, excludeFromTarget: [ "COMPEL" ]), "was", directory);

            await Assert.That(File.GetUnixFileMode(localPath)).IsEqualTo(DownloadedMode);
        }

        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}

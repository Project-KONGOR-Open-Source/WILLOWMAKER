namespace WILLOWMAKER.Tests.Infrastructure;

/// <summary>
///     Owns the headless Avalonia session that every view model test dispatches its work to, so that the work runs on the UI thread.
/// </summary>
public static class HeadlessSession
{
    private static HeadlessUnitTestSession Session { get; } = HeadlessUnitTestSession.StartNew(typeof(HeadlessApplication), AvaloniaTestIsolationLevel.PerAssembly);

    [Before(HookType.Assembly)]
    public static void Before_All_Tests()
    {
        // The Main View Model Writes Its Log File To The Current Directory, So The Current Directory Is Pinned To The Test Output Directory Regardless Of Where The Tests Are Launched From
        Environment.CurrentDirectory = AppContext.BaseDirectory;
    }

    [After(HookType.Assembly)]
    public static void After_All_Tests()
        => Session.Dispose();

    /// <summary>
    ///     Runs the specified action on the headless UI thread and returns its result.
    /// </summary>
    public static Task<TResult> Dispatch<TResult>(Func<TResult> action)
        => ResumeOnThreadPool(Session.Dispatch(action, CancellationToken.None));

    /// <summary>
    ///     Runs the specified asynchronous action on the headless UI thread and returns its result.
    /// </summary>
    public static Task<TResult> Dispatch<TResult>(Func<Task<TResult>> action)
        => ResumeOnThreadPool(Session.Dispatch(action, CancellationToken.None));

    // A Dispatched Task Completes On The UI Thread, Where The Awaiting Test Would Otherwise Resume Inline And Then Stall Once Background Work Is Queued To The UI Thread
    // Yielding Moves The Test Back To The Thread Pool, Whether The Dispatched Action Succeeded Or Failed
    private static async Task<TResult> ResumeOnThreadPool<TResult>(Task<TResult> dispatch)
    {
        try
        {
            return await dispatch.ConfigureAwait(false);
        }

        finally
        {
            await Task.Yield();
        }
    }
}

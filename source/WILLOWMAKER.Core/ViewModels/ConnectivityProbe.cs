namespace WILLOWMAKER.Core.ViewModels;

/// <summary>
///     Probes whether an address responds over HTTP, and exposes the outcome as observable state for the status message displayed beneath an address input.
///     Starting a new probe supersedes the one in progress, whose result is then discarded.
/// </summary>
/// <param name="subject">The name of what is being probed, as shown in the status messages (e.g. "CDN").</param>
/// <param name="log">Writes a line to the application log.</param>
public sealed partial class ConnectivityProbe(string subject, Action<string> log) : ObservableObject
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Failed))]
    public partial bool InProgress { get; set; } = false;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Failed))]
    public partial bool Succeeded { get; set; } = false;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Failed))]
    public partial string StatusMessage { get; set; } = string.Empty;

    public bool Failed => InProgress is false && Succeeded is false && string.IsNullOrEmpty(StatusMessage) is false;

    private CancellationTokenSource? cancellationTokenSource;

    /// <summary>
    ///     Cancels the probe in progress, if any, and clears the previous result.
    /// </summary>
    public void Reset()
    {
        Cancel();

        Succeeded     = false;
        StatusMessage = string.Empty;
    }

    /// <summary>
    ///     Cancels the probe in progress, if any, and reports that the address is invalid, unless no address has been entered.
    /// </summary>
    public void ReportInvalidAddress(string? address)
    {
        Cancel();

        Succeeded     = false;
        StatusMessage = string.IsNullOrWhiteSpace(address) ? string.Empty : $"Invalid {subject} Address";
    }

    /// <summary>
    ///     Probes the specified URL with an HTTP HEAD request once the specified delay has elapsed.
    ///     The previous result is cleared straight away, so that it is never shown for the new address while the delay elapses.
    /// </summary>
    public void Start(string probeURL, TimeSpan delay)
    {
        Reset();

        cancellationTokenSource = new CancellationTokenSource();

        CancellationToken cancellationToken = cancellationTokenSource.Token;

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(delay, cancellationToken).ConfigureAwait(false);

                await Probe(probeURL, cancellationToken).ConfigureAwait(false);
            }

            catch (OperationCanceledException)
            {
            }
        }, cancellationToken);
    }

    private void Cancel()
    {
        cancellationTokenSource?.Cancel();
        cancellationTokenSource?.Dispose();
        cancellationTokenSource = null;

        InProgress = false;
    }

    private async Task Probe(string probeURL, CancellationToken cancellationToken)
    {
        SetState(inProgress: true, succeeded: false, statusMessage: $"Probing {subject} Connectivity ...", cancellationToken);

        bool succeeded = false;
        string result;
        string? failureDetail = null;

        try
        {
            using HttpClient client = new ();
            client.DefaultRequestHeaders.UserAgent.ParseAdd($"WILLOWMAKER/{VersionChecker.CurrentVersionDisplay}");
            client.Timeout = TimeSpan.FromSeconds(5);

            using HttpRequestMessage request = new (HttpMethod.Head, probeURL);
            using HttpResponseMessage response = await client.SendAsync(request, cancellationToken).ConfigureAwait(false);

            succeeded = response.IsSuccessStatusCode;
            result    = $"HTTP {(int) response.StatusCode} ({response.StatusCode})";
        }

        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return;
        }

        catch (Exception exception)
        {
            result        = DescribeFailure(exception);
            failureDetail = exception.Message;
        }

        // The Final State Is Applied Before Anything Is Logged, So That A Failure To Write To The Log Can Neither Leave The Probe In Progress Nor Change Its Result
        SetState(inProgress: false, succeeded: succeeded, statusMessage: succeeded ? $"{subject} Is Online: {result}" : $"{subject} Is Unreachable: {result}", cancellationToken);

        log($@"INIT: Probing {subject} At ""{probeURL}""");
        log(succeeded ? $"INIT: {subject} Probe Succeeded: {result}" : $"WARN: {subject} Probe Failed: {result}" + (failureDetail is null ? string.Empty : $" :: {failureDetail}"));
    }

    // The Status Message Has Room For A Short Reason Only, So The Common Network Failures Are Described Briefly; The Full Exception Message Is Written To The Log Instead
    private static string DescribeFailure(Exception exception) => exception switch
    {
        HttpRequestException { StatusCode: not null } httpRequestException                => $"HTTP {(int) httpRequestException.StatusCode} ({httpRequestException.StatusCode})",
        HttpRequestException { HttpRequestError: HttpRequestError.NameResolutionError }    => "Host Not Found",
        HttpRequestException { HttpRequestError: HttpRequestError.ConnectionError }        => "Connection Failed",
        HttpRequestException { HttpRequestError: HttpRequestError.SecureConnectionError }  => "Secure Connection Failed",
        TaskCanceledException                                                              => "Timed Out",
        _                                                                                  => exception.Message
    };

    private void SetState(bool inProgress, bool succeeded, string statusMessage, CancellationToken cancellationToken)
    {
        void Apply()
        {
            // A Probe Which Was Superseded While Its Result Was Being Dispatched Must Not Overwrite The State Of The Probe Which Replaced It
            if (cancellationToken.IsCancellationRequested)
                return;

            InProgress    = inProgress;
            Succeeded     = succeeded;
            StatusMessage = statusMessage;
        }

        if (Dispatcher.UIThread.CheckAccess())
            Apply();
        else
            Dispatcher.UIThread.Post(Apply);
    }
}

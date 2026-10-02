namespace WILLOWMAKER.Tests.Infrastructure;

/// <summary>
///     A minimal loopback HTTP server which answers every request with a fixed status code after an optional delay, and records the most recent request.
/// </summary>
public sealed class TestHTTPServer : IAsyncDisposable
{
    private HttpListener Listener { get; } = new ();

    private Task ListenTask { get; }

    public string BaseURL { get; }

    public string? LastRequestMethod { get; private set; }

    public string? LastRequestPath { get; private set; }

    public TestHTTPServer(HttpStatusCode statusCode, TimeSpan responseDelay = default)
    {
        BaseURL = $"http://127.0.0.1:{GetAvailablePort()}/";

        Listener.Prefixes.Add(BaseURL);
        Listener.Start();

        ListenTask = Task.Run(() => Listen(statusCode, responseDelay));
    }

    private async Task Listen(HttpStatusCode statusCode, TimeSpan responseDelay)
    {
        while (Listener.IsListening)
        {
            HttpListenerContext context;

            try
            {
                context = await Listener.GetContextAsync();
            }

            catch (Exception)
            {
                // The Listener Was Stopped While Waiting For A Request
                return;
            }

            LastRequestMethod = context.Request.HttpMethod;
            LastRequestPath   = context.Request.Url?.AbsolutePath;

            // Each Request Is Answered Independently, So That A Delayed Response Does Not Hold Up The Requests Which Follow It
            _ = Respond(context, statusCode, responseDelay);
        }
    }

    private static async Task Respond(HttpListenerContext context, HttpStatusCode statusCode, TimeSpan responseDelay)
    {
        try
        {
            await Task.Delay(responseDelay);

            context.Response.StatusCode = (int) statusCode;
            context.Response.Close();
        }

        catch (Exception)
        {
            // The Client Abandoned The Request Before The Response Was Sent
        }
    }

    /// <summary>
    ///     Returns a loopback port which nothing is listening on at the time of the call.
    /// </summary>
    public static int GetAvailablePort()
    {
        using TcpListener listener = new (IPAddress.Loopback, 0);

        listener.Start();

        return ((IPEndPoint) listener.LocalEndpoint).Port;
    }

    public async ValueTask DisposeAsync()
    {
        Listener.Stop();
        Listener.Close();

        await ListenTask;
    }
}

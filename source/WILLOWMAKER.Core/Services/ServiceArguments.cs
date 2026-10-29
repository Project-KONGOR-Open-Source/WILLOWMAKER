namespace WILLOWMAKER.Core.Services;

/// <summary>
///     The service addresses WILLOWMAKER starts the game client with. The Project KONGOR master server answers every service the game reaches over HTTP, so each one is pointed at its address.
/// </summary>
public static class ServiceArguments
{
    /// <summary>
    ///     The arguments that point each service at the master server's address.
    /// </summary>
    public static string[] For(string address) =>
    [
        $"-masterserver {address}",
        $"-webserver {address}",
        $"-messageserver {address}",
        $"-ascensionserver {address}"
    ];
}

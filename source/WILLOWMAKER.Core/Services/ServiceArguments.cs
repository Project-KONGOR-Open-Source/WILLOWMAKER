namespace WILLOWMAKER.Core.Services;

/// <summary>
///     The service addresses WILLOWMAKER starts the game client with. The Project KONGOR master server answers every service the game reaches over HTTP, so each one is pointed at its address; the user portal, where players register and recover their passwords, has an address of its own.
/// </summary>
public static class ServiceArguments
{
    /// <summary>
    ///     The arguments that point each service at the master server's address, followed by the user portal's address when there is one.
    /// </summary>
    public static string[] For(string address, string? userPortal = null) =>
    [
        $"-masterserver {address}",
        $"-webserver {address}",
        $"-messageserver {address}",
        $"-ascensionserver {address}",
        .. string.IsNullOrEmpty(userPortal) ? Array.Empty<string>() : [$"-userportal {userPortal}"]
    ];
}

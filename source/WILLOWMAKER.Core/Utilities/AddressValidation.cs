namespace WILLOWMAKER.Core.Utilities;

/// <summary>
///     Provides address validation and URL normalisation for content delivery network endpoints and services.
/// </summary>
public static class AddressValidation
{
    /// <summary>
    ///     Normalises a content delivery network address or URL into an absolute URL ending with a trailing slash.
    /// </summary>
    public static string NormaliseCDNURL(string? rawURL)
    {
        if (string.IsNullOrWhiteSpace(rawURL))
            return string.Empty;

        string trimmed = rawURL.Trim();

        // An Address Which Already Has A Scheme Keeps It, So That Validation Rejects An Unsupported Scheme Instead Of Accepting It Nested Inside A Prepended One
        string urlWithScheme = trimmed.Contains("://", StringComparison.Ordinal)
            ? trimmed
            : IsLoopbackHost(trimmed) ? $"http://{trimmed}" : $"https://{trimmed}";

        return urlWithScheme.EndsWith('/') ? urlWithScheme : $"{urlWithScheme}/";
    }

    /// <summary>
    ///     Normalises a user portal address into the form the game client is started with, a host with an optional port and path, or returns NULL when the game client would refuse it.
    /// </summary>
    public static string? NormaliseUserPortalAddress(string? rawAddress)
    {
        if (string.IsNullOrEmpty(rawAddress))
            return null;

        // The Game Client Always Reaches The Portal Over HTTPS And Refuses An Address With A Scheme, So That Scheme Is Dropped, While Any Other Scheme, Or A Backslash, Makes The Address Invalid
        string address = rawAddress.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ? rawAddress["https://".Length..] : rawAddress;

        address = address.TrimEnd('/');

        if (address.StartsWith('/') || address.Contains("://", StringComparison.Ordinal) || address.Contains('\\'))
            return null;

        return IsValidAddress(address) ? address : null;
    }

    /// <summary>
    ///     Determines whether the specified raw address or URL string is syntactically valid for network communication.
    /// </summary>
    public static bool IsValidAddress(string? rawAddress)
    {
        if (string.IsNullOrWhiteSpace(rawAddress))
            return false;

        // A Query ("?"), A Fragment ("#"), Or User Information ("@") Would Displace The Paths Appended To The Address, So That Requests Would No Longer Reach The Intended Files
        foreach (char character in rawAddress)
        {
            if (char.IsWhiteSpace(character) || char.IsControl(character) || character is '"' or ';' or '?' or '#' or '@')
                return false;
        }

        string? candidateURL = rawAddress.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || rawAddress.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
            ? rawAddress
            : rawAddress.Contains("://", StringComparison.Ordinal)
                ? null
                : $"http://{rawAddress}";

        if (candidateURL is null)
            return false;

        if (Uri.TryCreate(candidateURL, UriKind.Absolute, out Uri? uri) is false)
            return false;

        if (uri.Scheme is not ("http" or "https"))
            return false;

        if (string.IsNullOrEmpty(uri.Host))
            return false;

        return Uri.CheckHostName(uri.DnsSafeHost) is not UriHostNameType.Unknown;
    }

    private static bool IsLoopbackHost(string address)
    {
        if (address.StartsWith('['))
        {
            int closingBracketIndex = address.IndexOf(']');

            if (closingBracketIndex > 0)
            {
                string bracketedHost = address[..(closingBracketIndex + 1)];

                return bracketedHost.Equals("[::1]", StringComparison.OrdinalIgnoreCase);
            }
        }

        int delimiterIndex = address.IndexOfAny(['/', ':']);
        string host = delimiterIndex >= 0 ? address[..delimiterIndex] : address;

        return host.Equals("localhost", StringComparison.OrdinalIgnoreCase)
            || host.Equals("127.0.0.1", StringComparison.OrdinalIgnoreCase);
    }
}

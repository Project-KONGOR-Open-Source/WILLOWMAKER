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
    ///     Determines whether the specified raw address or URL string is syntactically valid for network communication.
    /// </summary>
    public static bool IsValidAddress(string? rawAddress)
    {
        if (string.IsNullOrWhiteSpace(rawAddress))
            return false;

        foreach (char character in rawAddress)
        {
            if (char.IsWhiteSpace(character) || char.IsControl(character) || character is '"' or ';')
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

namespace WILLOWMAKER.Tests.Utilities;

/// <summary>
///     Verifies content delivery network address validation and URL normalisation rules.
/// </summary>
public sealed class AddressValidationTests
{
    [Test]
    public async Task Normalise_CDN_URL_Prepends_HTTPS_And_Appends_Trailing_Slash_For_External_Host()
    {
        string normalised = AddressValidation.NormaliseCDNURL("cdn.kongor.net");

        await Assert.That(normalised).IsEqualTo("https://cdn.kongor.net/");
    }

    [Test]
    public async Task Normalise_CDN_URL_Prepends_HTTP_And_Appends_Trailing_Slash_For_Localhost()
    {
        string normalised = AddressValidation.NormaliseCDNURL("localhost:5555/cdn");

        await Assert.That(normalised).IsEqualTo("http://localhost:5555/cdn/");
    }

    [Test]
    public async Task Normalise_CDN_URL_Prepends_HTTP_And_Appends_Trailing_Slash_For_Loopback_IP_Address()
    {
        string normalised = AddressValidation.NormaliseCDNURL("127.0.0.1:5555/cdn");

        await Assert.That(normalised).IsEqualTo("http://127.0.0.1:5555/cdn/");
    }

    [Test]
    public async Task Normalise_CDN_URL_Prepends_HTTP_And_Appends_Trailing_Slash_For_IPv6_Loopback_Address()
    {
        using (Assert.Multiple())
        {
            await Assert.That(AddressValidation.NormaliseCDNURL("[::1]:5555/cdn")).IsEqualTo("http://[::1]:5555/cdn/");
            await Assert.That(AddressValidation.NormaliseCDNURL("[::1]")).IsEqualTo("http://[::1]/");
        }
    }

    [Test]
    public async Task Normalise_CDN_URL_Preserves_Existing_HTTPS_Scheme_And_Appends_Trailing_Slash()
    {
        string normalised = AddressValidation.NormaliseCDNURL("https://cdn.kongor.net");

        await Assert.That(normalised).IsEqualTo("https://cdn.kongor.net/");
    }

    [Test]
    public async Task Normalise_CDN_URL_Preserves_Existing_HTTP_Scheme_And_Trailing_Slash()
    {
        string normalised = AddressValidation.NormaliseCDNURL("http://custom.domain.com/cdn/");

        await Assert.That(normalised).IsEqualTo("http://custom.domain.com/cdn/");
    }

    [Test]
    public async Task Normalise_CDN_URL_Trims_Surrounding_Whitespace()
    {
        string normalised = AddressValidation.NormaliseCDNURL("   cdn.kongor.net   ");

        await Assert.That(normalised).IsEqualTo("https://cdn.kongor.net/");
    }

    [Test]
    public async Task Normalise_CDN_URL_Returns_Empty_String_When_Given_Null_Or_Whitespace()
    {
        using (Assert.Multiple())
        {
            await Assert.That(AddressValidation.NormaliseCDNURL(null)).IsEqualTo(string.Empty);
            await Assert.That(AddressValidation.NormaliseCDNURL(string.Empty)).IsEqualTo(string.Empty);
            await Assert.That(AddressValidation.NormaliseCDNURL("   ")).IsEqualTo(string.Empty);
        }
    }

    [Test]
    public async Task Is_Valid_Address_Returns_True_For_Valid_Host_Names_IP_Addresses_And_URLs()
    {
        using (Assert.Multiple())
        {
            await Assert.That(AddressValidation.IsValidAddress("cdn.kongor.net")).IsTrue();
            await Assert.That(AddressValidation.IsValidAddress("localhost:5555/cdn")).IsTrue();
            await Assert.That(AddressValidation.IsValidAddress("192.168.1.100:8080")).IsTrue();
            await Assert.That(AddressValidation.IsValidAddress("https://cdn.kongor.net")).IsTrue();
            await Assert.That(AddressValidation.IsValidAddress("[::1]:5555/cdn")).IsTrue();
            await Assert.That(AddressValidation.IsValidAddress("[::1]")).IsTrue();
        }
    }

    [Test]
    public async Task Is_Valid_Address_Returns_False_For_Unbracketed_IPv6_Addresses()
    {
        using (Assert.Multiple())
        {
            await Assert.That(AddressValidation.IsValidAddress("::1")).IsFalse();
            await Assert.That(AddressValidation.IsValidAddress("::1:5555/cdn")).IsFalse();
        }
    }

    [Test]
    public async Task Is_Valid_Address_Returns_False_For_Normalised_Addresses_With_Unsupported_Schemes()
    {
        using (Assert.Multiple())
        {
            await Assert.That(AddressValidation.IsValidAddress(AddressValidation.NormaliseCDNURL("ftp://cdn.kongor.net"))).IsFalse();
            await Assert.That(AddressValidation.IsValidAddress(AddressValidation.NormaliseCDNURL("file:///srv/cdn"))).IsFalse();
        }
    }

    [Test]
    public async Task Is_Valid_Address_Returns_False_For_Null_Empty_Or_Whitespace()
    {
        using (Assert.Multiple())
        {
            await Assert.That(AddressValidation.IsValidAddress(null)).IsFalse();
            await Assert.That(AddressValidation.IsValidAddress(string.Empty)).IsFalse();
            await Assert.That(AddressValidation.IsValidAddress("   ")).IsFalse();
        }
    }

    [Test]
    public async Task Is_Valid_Address_Returns_False_For_Addresses_With_A_Query_A_Fragment_Or_User_Information()
    {
        using (Assert.Multiple())
        {
            await Assert.That(AddressValidation.IsValidAddress("cdn.kongor.net?version=1")).IsFalse();
            await Assert.That(AddressValidation.IsValidAddress("cdn.kongor.net?")).IsFalse();
            await Assert.That(AddressValidation.IsValidAddress("https://cdn.kongor.net/cdn?version=1")).IsFalse();
            await Assert.That(AddressValidation.IsValidAddress("cdn.kongor.net#files")).IsFalse();
            await Assert.That(AddressValidation.IsValidAddress("user:password@cdn.kongor.net")).IsFalse();
            await Assert.That(AddressValidation.IsValidAddress("https://user@cdn.kongor.net")).IsFalse();
        }
    }

    [Test]
    public async Task Is_Valid_Address_Returns_False_For_Addresses_Containing_Double_Quotes()
    {
        using (Assert.Multiple())
        {
            await Assert.That(AddressValidation.IsValidAddress("\"cdn.kongor.net\"")).IsFalse();
            await Assert.That(AddressValidation.IsValidAddress("cdn.kongor.net/\"test\"")).IsFalse();
        }
    }

    [Test]
    public async Task Is_Valid_Address_Returns_False_For_Addresses_Containing_Semicolons()
    {
        await Assert.That(AddressValidation.IsValidAddress("cdn.kongor.net;injection")).IsFalse();
    }

    [Test]
    public async Task Is_Valid_Address_Returns_False_For_Addresses_Containing_Whitespace_Or_Control_Characters()
    {
        using (Assert.Multiple())
        {
            await Assert.That(AddressValidation.IsValidAddress("cdn .kongor.net")).IsFalse();
            await Assert.That(AddressValidation.IsValidAddress("  cdn.kongor.net  ")).IsFalse();
            await Assert.That(AddressValidation.IsValidAddress("cdn.kongor.net\n")).IsFalse();
            await Assert.That(AddressValidation.IsValidAddress("cdn.kongor.net\r\n")).IsFalse();
            await Assert.That(AddressValidation.IsValidAddress("cdn.kongor.net\t")).IsFalse();
            await Assert.That(AddressValidation.IsValidAddress("cdn.kongor.net\0")).IsFalse();
        }
    }
}

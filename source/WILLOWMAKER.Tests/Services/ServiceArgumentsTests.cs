namespace WILLOWMAKER.Tests.Services;

/// <summary>
///     Verifies the service addresses WILLOWMAKER starts the game client with: every service the game reaches over HTTP, Ascension included, answers at the master server's address.
/// </summary>
public sealed class ServiceArgumentsTests
{
    [Test]
    public async Task The_Game_Client_Reaches_Every_Service_At_The_Master_Server()
        => await Assert.That(string.Join('|', ServiceArguments.For("127.0.0.1:5555"))).IsEqualTo("-masterserver 127.0.0.1:5555|-webserver 127.0.0.1:5555|-messageserver 127.0.0.1:5555|-ascensionserver 127.0.0.1:5555");
}

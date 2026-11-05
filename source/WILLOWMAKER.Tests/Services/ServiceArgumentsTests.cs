namespace WILLOWMAKER.Tests.Services;

/// <summary>
///     Verifies the service addresses WILLOWMAKER starts the game client with: every service the game reaches over HTTP, Ascension included, answers at the master server's address, and the user portal, when there is one, at its own.
/// </summary>
public sealed class ServiceArgumentsTests
{
    [Test]
    public async Task The_Game_Client_Reaches_Every_Service_At_The_Master_Server()
        => await Assert.That(string.Join('|', ServiceArguments.For("127.0.0.1:5555"))).IsEqualTo("-masterserver 127.0.0.1:5555|-webserver 127.0.0.1:5555|-messageserver 127.0.0.1:5555|-ascensionserver 127.0.0.1:5555");

    [Test]
    public async Task The_Game_Client_Is_Told_The_User_Portal_After_The_Other_Services()
        => await Assert.That(string.Join('|', ServiceArguments.For("127.0.0.1:5555", "localhost:5557"))).IsEqualTo("-masterserver 127.0.0.1:5555|-webserver 127.0.0.1:5555|-messageserver 127.0.0.1:5555|-ascensionserver 127.0.0.1:5555|-userportal localhost:5557");

    [Test]
    public async Task The_Game_Client_Is_Told_No_User_Portal_When_There_Is_None()
        => await Assert.That(string.Join('|', ServiceArguments.For("127.0.0.1:5555", string.Empty))).IsEqualTo("-masterserver 127.0.0.1:5555|-webserver 127.0.0.1:5555|-messageserver 127.0.0.1:5555|-ascensionserver 127.0.0.1:5555");
}

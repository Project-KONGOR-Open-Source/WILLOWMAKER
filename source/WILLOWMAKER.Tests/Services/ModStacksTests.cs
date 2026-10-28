namespace WILLOWMAKER.Tests.Services;

/// <summary>
///     Verifies the game program WILLOWMAKER starts and the mod stacks it starts it with, which the engine's own defaults match: the KONGOR folders sit just below the module's own folder, with "KONGOR/extensions" last.
/// </summary>
public sealed class ModStacksTests
{
    [Test]
    public async Task The_Game_Program_Has_No_Architecture_In_Its_Name()
    {
        if (OperatingSystem.IsWindows() is false && OperatingSystem.IsLinux() is false)
            return;

        await Assert.That(DeploymentManifest.HeroesOfNewerthExecutableFileName).IsEqualTo(OperatingSystem.IsWindows() ? "hon.exe" : "hon");
    }

    [Test]
    public async Task The_Game_Client_Starts_With_The_KONGOR_Folders_Below_Its_Own()
        => await Assert.That(string.Join(';', ModStacks.GameClient)).IsEqualTo("base;game;KONGOR/configuration;KONGOR/updates;KONGOR/extensions;client");

    [Test]
    public async Task The_Map_Editor_Starts_From_Its_Kebab_Case_Folder()
        => await Assert.That(string.Join(';', ModStacks.MapEditor)).IsEqualTo("base;game;map-editor;KONGOR/configuration;KONGOR/updates;KONGOR/extensions;map-editor");
}

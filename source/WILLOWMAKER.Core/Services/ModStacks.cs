namespace WILLOWMAKER.Core.Services;

/// <summary>
///     The mod stacks WILLOWMAKER starts Heroes Of Newerth with, which the engine's own defaults match: "game", the tool's own folder where it has one, the KONGOR folders with "KONGOR/extensions" last, so that it overrides every shipped resource, and last of all the module's folder, which names its user folder.
/// </summary>
public static class ModStacks
{
    /// <summary>
    ///     The game client's stack.
    /// </summary>
    public static string[] GameClient =>
    [
        // relative to executable directory (e.g. "D:\Games\HoN Game Client")
        "base", // base resources; always needs to be loaded first; loaded automatically, but included for clarity
        "game", // game resources; always needs to be loaded immediately after base resources
        $"{FileSystem.RuntimeDirectoryName}/configuration", // custom configuration files to override default configuration; configuration file load order: 1) startup.cfg, 2) login.cfg, 3) init.cfg, 4) autoexec.cfg
        $"{FileSystem.RuntimeDirectoryName}/updates", // custom resource files to override default game resources; reserved for game updates
        $"{FileSystem.RuntimeDirectoryName}/extensions", // custom resource files to override default game resources; reserved for mods and extensions

        // relative to configuration directory (e.g. "C:\Users\KONGOR\HON")
        "client" // the last path in the mod stack defines where user configuration files are saved to and loaded from
    ];

    /// <summary>
    ///     The map editor's stack.
    /// </summary>
    public static string[] MapEditor =>
    [
        // relative to executable directory (e.g. "D:\Games\HoN Game Client")
        "base", // base resources; always needs to be loaded first; loaded automatically, but included for clarity
        "game", // game resources; always needs to be loaded immediately after base resources
        "map-editor", // map editor resources; loaded immediately after game resources to overlay editor tooling onto the game stack
        $"{FileSystem.RuntimeDirectoryName}/configuration", // custom configuration files to override default configuration; configuration file load order: 1) startup.cfg, 2) login.cfg, 3) init.cfg, 4) autoexec.cfg
        $"{FileSystem.RuntimeDirectoryName}/updates", // custom resource files to override default game resources; reserved for game updates
        $"{FileSystem.RuntimeDirectoryName}/extensions", // custom resource files to override default game resources; reserved for mods and extensions

        // relative to configuration directory (e.g. "C:\Users\KONGOR\HON")
        "map-editor" // the last path in the mod stack defines where user configuration files are saved to and loaded from
    ];
}

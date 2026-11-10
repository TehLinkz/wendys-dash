using System;
using System.IO;
using WendysDash;

internal static class LinuxTests
{
    public static void Run()
    {
        string parent = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar);
        string root = Path.Combine(parent, "wendys-dash-discovery-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            string home = Path.Combine(root, "home"), media = Path.Combine(root, "media"), empty = Path.Combine(root, "app");
            string internalGame = CreateGame(Path.Combine(home, ".local", "share", "Steam"));
            Check(LinuxSteamLocator.FindGames(home, empty, media).Contains(internalGame), "internal Steam library");
            string sdGame = CreateGame(Path.Combine(media, "deck", "SD Card"));
            Check(LinuxSteamLocator.FindGames(home, empty, media).Contains(sdGame), "SD card with spaces");
            string custom = Path.Combine(root, "Library with spaces"), customGame = CreateGame(custom);
            File.WriteAllText(Path.Combine(home, ".local", "share", "Steam", "steamapps", "libraryfolders.vdf"), "\"libraryfolders\" { \"1\" { \"path\" \"" + custom + "\" } }");
            Check(LinuxSteamLocator.FindGames(home, empty, media).Contains(customGame), "libraryfolders.vdf path");
            string flatpak = CreateGame(Path.Combine(home, ".var", "app", "com.valvesoftware.Steam", "data", "Steam"));
            Check(LinuxSteamLocator.FindGames(home, empty, media).Contains(flatpak), "Flatpak Steam library");
            Check(GameProcessGuard.ContainsGameExecutable("/usr/bin/wine\0/run/media/deck/SD Card/WendyQuest.exe\0"), "Proton command line");
            Check(GameProcessGuard.ContainsGameExecutable("wine\0Z:\\games\\WendyQuest.exe\0"), "Wine Windows path");
            Check(!GameProcessGuard.ContainsGameExecutable("cat\0/notes/WendyQuest.exe.backup\0"), "similarly named file is not a game process");
            Check(LinuxSteamLocator.ReadLibraryPaths("\"path\" \"relative/location\"").Count == 0, "relative library path refused");
            Check(LinuxSteamLocator.FindGames(home, internalGame, media).FindAll(p => p == internalGame).Count == 1, "duplicate candidate removed");
        }
        finally
        {
            string full = Path.GetFullPath(root);
            if (Path.GetDirectoryName(full) != parent || !Path.GetFileName(full).StartsWith("wendys-dash-discovery-", StringComparison.Ordinal)) throw new IOException("Unexpected test cleanup path.");
            if (Directory.Exists(full)) Directory.Delete(full, true);
        }
    }
    private static string CreateGame(string library)
    {
        string game = Path.Combine(library, "steamapps", "common", "Wendy Quest");
        string target = Path.Combine(game, "package.nw", "js", "plugins", "MapScene.js");
        Directory.CreateDirectory(Path.GetDirectoryName(target));
        File.WriteAllText(target, "// fixture");
        return game;
    }
    private static void Check(bool condition, string name)
    {
        if (!condition) throw new Exception("FAIL Linux: " + name);
        Console.WriteLine("PASS Linux: " + name);
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

namespace WendysDash
{
    internal static class LinuxSteamLocator
    {
        public static List<string> FindGames()
        {
            return FindGames(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                AppDomain.CurrentDomain.BaseDirectory, "/run/media");
        }

        internal static List<string> FindGames(string home, string appFolder, string mediaRoot)
        {
            List<string> roots = new List<string> {
                Path.Combine(home, ".local", "share", "Steam"),
                Path.Combine(home, ".steam", "steam"),
                Path.Combine(home, ".steam", "root"),
                Path.Combine(home, ".var", "app", "com.valvesoftware.Steam", "data", "Steam")
            };
            foreach (string root in roots.ToArray())
            {
                try
                {
                    string vdf = Path.Combine(root, "steamapps", "libraryfolders.vdf");
                    if (File.Exists(vdf)) roots.AddRange(ReadLibraryPaths(File.ReadAllText(vdf)));
                }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
            }
            // Check only mount roots and one user subdirectory, never whole disks.
            foreach (string mount in ChildDirectories(mediaRoot))
            {
                roots.Add(mount);
                roots.AddRange(ChildDirectories(mount));
            }
            List<string> candidates = new List<string> { appFolder };
            foreach (string root in roots)
                candidates.Add(Path.Combine(root, "steamapps", "common", "Wendy Quest"));
            List<string> games = new List<string>();
            foreach (string candidate in candidates)
            {
                if (String.IsNullOrWhiteSpace(candidate)) continue;
                string full = Path.GetFullPath(candidate);
                if (File.Exists(Path.Combine(full, "package.nw", "js", "plugins", "MapScene.js")) && !games.Contains(full)) games.Add(full);
            }
            return games;
        }

        internal static List<string> ReadLibraryPaths(string vdf)
        {
            List<string> paths = new List<string>();
            foreach (Match match in Regex.Matches(vdf, "\"path\"\\s+\"((?:\\\\.|[^\"\\\\])*)\""))
            {
                string value = match.Groups[1].Value.Replace("\\\"", "\"").Replace("\\\\", "\\");
                if (Path.IsPathRooted(value)) paths.Add(value);
            }
            return paths;
        }

        private static string[] ChildDirectories(string path)
        {
            try { return Directory.Exists(path) ? Directory.GetDirectories(path) : new string[0]; }
            catch (IOException) { return new string[0]; }
            catch (UnauthorizedAccessException) { return new string[0]; }
        }
    }
}

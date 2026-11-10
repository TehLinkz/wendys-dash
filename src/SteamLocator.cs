using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

namespace WendysDash
{
    internal static class SteamLocator
    {
        public static string FindGame()
        {
            List<string> libraries = new List<string>();
            List<string> candidates = new List<string>();
            candidates.Add(AppDomain.CurrentDomain.BaseDirectory);
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam"))
                    if (key != null) Add(libraries, key.GetValue("SteamPath") as string);
                using (RegistryKey key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\WOW6432Node\Valve\Steam"))
                    if (key != null) Add(libraries, key.GetValue("InstallPath") as string);
            }
            catch (System.Security.SecurityException) { }
            Add(libraries, Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Steam"));
            foreach (string steam in libraries.ToArray())
            {
                try
                {
                    string vdf = Path.Combine(steam, @"steamapps\libraryfolders.vdf");
                    if (File.Exists(vdf))
                        foreach (Match match in Regex.Matches(File.ReadAllText(vdf), "\"path\"\\s+\"([^\"]+)\""))
                            Add(libraries, match.Groups[1].Value.Replace("\\\\", "\\"));
                }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
            }
            foreach (string library in libraries) candidates.Add(Path.Combine(library, @"steamapps\common\Wendy Quest"));
            foreach (string candidate in candidates)
                if (File.Exists(Path.Combine(candidate, @"package.nw\js\plugins\MapScene.js"))) return candidate.TrimEnd(Path.DirectorySeparatorChar);
            return String.Empty;
        }

        private static void Add(List<string> list, string path)
        {
            if (!String.IsNullOrWhiteSpace(path) && !list.Exists(p => String.Equals(p, path, StringComparison.OrdinalIgnoreCase))) list.Add(path);
        }
    }
}

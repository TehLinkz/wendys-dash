using System;
using System.Diagnostics;
using System.IO;

namespace WendysDash
{
    internal static class GameProcessGuard
    {
        public static bool IsGameRunning()
        {
            foreach (string name in new[] { "WendyQuest", "WendyQuest.exe" })
            {
                Process[] processes = Process.GetProcessesByName(name);
                bool found = processes.Length > 0;
                foreach (Process process in processes) process.Dispose();
                if (found) return true;
            }
            // Wine/Proton may expose the Windows executable as an argument.
            if (Environment.OSVersion.Platform != PlatformID.Unix || !Directory.Exists("/proc")) return false;
            foreach (string directory in Directory.GetDirectories("/proc"))
            {
                int pid;
                if (!Int32.TryParse(Path.GetFileName(directory), out pid)) continue;
                try
                {
                    if (ContainsGameExecutable(File.ReadAllText(Path.Combine(directory, "cmdline")))) return true;
                }
                catch (IOException) { } // Processes can exit while being inspected.
                catch (UnauthorizedAccessException) { }
            }
            return false;
        }

        internal static bool ContainsGameExecutable(string commandLine)
        {
            foreach (string argument in commandLine.Split('\0'))
            {
                string normalized = argument.Trim('"').Replace('\\', '/');
                string name = normalized.Substring(normalized.LastIndexOf('/') + 1);
                if (String.Equals(name, "WendyQuest.exe", StringComparison.OrdinalIgnoreCase)) return true;
            }
            return false;
        }
    }
}

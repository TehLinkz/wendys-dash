using System;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace WendysDash
{
    public enum PatchState { Missing, Unsupported, Ready, Installed, LegacyInstalled, Conflict }

    public sealed class PatchInfo
    {
        public PatchState State { get; internal set; }
        public string Message { get; internal set; }
        public string TargetPath { get; internal set; }
        internal byte[] Bytes;
        internal int SuffixLength;
    }

    public sealed class PatchResult
    {
        public bool Changed { get; internal set; }
        public string Message { get; internal set; }
        public string BackupPath { get; internal set; }
    }

    public sealed class PatchService
    {
        public const string BeginMarker = "// BEGIN WENDY_QUEST_HOLD_X_RUN_V1";
        public const string EndMarker = "// END WENDY_QUEST_HOLD_X_RUN_V1";
        internal const string LegacyOn = "Game_Player[a15_0x519f09(0x192)]['isDashing']=function(){return this._dashing;}";
        internal const string LegacyOff = "Game_Player[a15_0x519f09(0x192)]['isDashing']=function(){return!0x1;}";
        private const int MaxFileBytes = 16 * 1024 * 1024;
        private static readonly UTF8Encoding Utf8 = new UTF8Encoding(false, true);
        private readonly Func<bool> gameRunning;

        // Stable, identifiable suffix for repeatable installation and exact removal.
        public static readonly string Payload = String.Join("\r\n", new[] {
            "", BeginMarker,
            ";(function () {",
            "    \"use strict\";",
            "    // Xbox X uses the same native dash input as keyboard Shift.",
            "    Input.gamepadMapper[2] = \"shift\";",
            "    Game_Player.prototype.isDashing = function () {",
            "        return this._dashing;",
            "    };",
            "})();", EndMarker, ""
        });

        public PatchService() : this(IsGameRunning) { }
        public PatchService(Func<bool> runningCheck)
        {
            if (runningCheck == null) throw new ArgumentNullException("runningCheck");
            gameRunning = runningCheck;
        }

        public bool Running { get { return gameRunning(); } }

        public static bool IsGameRunning()
        {
            Process[] processes = Process.GetProcessesByName("WendyQuest");
            bool running = processes.Length > 0;
            foreach (Process process in processes) process.Dispose();
            return running;
        }

        public PatchInfo Inspect(string folder)
        {
            PatchInfo info = new PatchInfo();
            info.State = PatchState.Missing;
            info.Message = "Choose your Wendy Quest installation folder.";
            if (String.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder)) return info;
            string root = Path.GetFullPath(folder);
            info.TargetPath = Path.Combine(root, @"package.nw\js\plugins\MapScene.js");
            string objects = Path.Combine(root, @"package.nw\js\rmmz_objects.js");
            string input = Path.Combine(root, @"package.nw\js\plugins\InputAdjustments.js");
            if (!File.Exists(info.TargetPath) || !File.Exists(objects) || !File.Exists(input))
            {
                info.Message = "Select the game folder containing package.nw.";
                return info;
            }
            if (new FileInfo(info.TargetPath).Length > MaxFileBytes || new FileInfo(objects).Length > MaxFileBytes)
                return Set(info, PatchState.Unsupported, "The game files differ from the supported layout.");
            string engine = File.ReadAllText(objects, Utf8);
            if (!engine.Contains("Game_Player.prototype.updateDashing") ||
                !engine.Contains("this._dashing") || !engine.Contains("this.isDashing() ? 1 : 0"))
                return Set(info, PatchState.Unsupported, "This version uses different movement code. No changes were made.");
            info.Bytes = File.ReadAllBytes(info.TargetPath);
            string source = Utf8.GetString(info.Bytes);
            if (!source.Contains("Game_Player"))
                return Set(info, PatchState.Unsupported, "The map plugin does not match the expected game code.");
            int begins = Count(source, BeginMarker), ends = Count(source, EndMarker);
            string suffix = source.EndsWith(Payload, StringComparison.Ordinal) ? Payload :
                source.EndsWith(Payload.Replace("\r\n", "\n"), StringComparison.Ordinal) ? Payload.Replace("\r\n", "\n") : null;
            if (begins == 1 && ends == 1 && suffix != null)
            {
                info.SuffixLength = Utf8.GetByteCount(suffix);
                return Set(info, PatchState.Installed, "Hold X or Shift to run. Release to walk.");
            }
            if (begins != 0 || ends != 0)
                return Set(info, PatchState.Conflict, "The patch block was modified. Restore or verify game files before retrying.");
            if (Count(source, LegacyOn) == 1)
                return Set(info, PatchState.LegacyInstalled, "An existing dash modification is active. Undo can remove it.");
            return Set(info, PatchState.Ready, "Ready to enable hold-to-run controls.");
        }

        public PatchResult Apply(string folder) { return Change(folder, false); }
        public PatchResult Undo(string folder) { return Change(folder, true); }

        private PatchResult Change(string folder, bool undo)
        {
            if (Running) throw new InvalidOperationException("Close Wendy Quest, then try again.");
            PatchInfo info = Inspect(folder);
            bool installed = info.State == PatchState.Installed || info.State == PatchState.LegacyInstalled;
            if ((!undo && installed) || (undo && info.State == PatchState.Ready))
                return new PatchResult { Changed = false, Message = installed ? "The patch is already installed." : "No patch is installed." };
            if (info.State != PatchState.Ready && !installed)
                throw new InvalidOperationException(info.Message);

            byte[] after;
            if (!undo)
            {
                byte[] suffix = Utf8.GetBytes(Payload);
                after = new byte[info.Bytes.Length + suffix.Length];
                Buffer.BlockCopy(info.Bytes, 0, after, 0, info.Bytes.Length);
                Buffer.BlockCopy(suffix, 0, after, info.Bytes.Length, suffix.Length);
            }
            else if (info.State == PatchState.LegacyInstalled)
                after = Utf8.GetBytes(Utf8.GetString(info.Bytes).Replace(LegacyOn, LegacyOff));
            else
            {
                after = new byte[info.Bytes.Length - info.SuffixLength];
                Buffer.BlockCopy(info.Bytes, 0, after, 0, after.Length);
            }

            string beforeHash = Hash(info.Bytes);
            string backup = info.TargetPath + (undo ? ".before-undo-hold-x-run-" : ".before-hold-x-run-") + beforeHash.Substring(0, 12) + ".bak";
            // Lock the selected file, recheck its contents, then back it up before writing.
            using (FileStream stream = new FileStream(info.TargetPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            {
                if (stream.Length != info.Bytes.Length || Hash(ReadStream(stream)) != beforeHash)
                    throw new IOException("The game file changed during inspection. Refresh and try again.");
                if (Running) throw new InvalidOperationException("Close Wendy Quest, then try again.");
                EnsureBackup(backup, info.Bytes, beforeHash);
                try
                {
                    WriteStream(stream, after);
                    if (Hash(ReadStream(stream)) != Hash(after)) throw new IOException("File verification failed.");
                }
                catch (Exception writeError)
                {
                    try { WriteStream(stream, info.Bytes); }
                    catch (Exception restoreError)
                    {
                        throw new IOException("The write and recovery failed. Restore the backup: " + backup,
                            new AggregateException(writeError, restoreError));
                    }
                    throw new IOException("The write failed; the original contents were restored. " + writeError.Message, writeError);
                }
            }
            return new PatchResult {
                Changed = true,
                BackupPath = backup,
                Message = undo ? "Patch removed. The previous movement behavior is restored." : "Patch installed. Hold X or Shift to run at 2x speed."
            };
        }

        private static void EnsureBackup(string path, byte[] bytes, string hash)
        {
            if (!File.Exists(path))
            {
                using (FileStream backup = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    backup.Write(bytes, 0, bytes.Length);
                    backup.Flush(true);
                }
            }
            if (Hash(File.ReadAllBytes(path)) != hash)
                throw new IOException("An existing backup has unexpected contents. It was not overwritten: " + path);
        }

        private static byte[] ReadStream(FileStream stream)
        {
            if (stream.Length > MaxFileBytes + 4096) throw new IOException("The selected game file is too large.");
            stream.Position = 0;
            byte[] bytes = new byte[(int)stream.Length];
            int offset = 0, read;
            while (offset < bytes.Length && (read = stream.Read(bytes, offset, bytes.Length - offset)) != 0) offset += read;
            if (offset != bytes.Length) throw new EndOfStreamException();
            return bytes;
        }

        private static void WriteStream(FileStream stream, byte[] bytes)
        {
            stream.Position = 0;
            stream.Write(bytes, 0, bytes.Length);
            stream.SetLength(bytes.Length);
            stream.Flush(true);
        }

        internal static string Hash(byte[] bytes)
        {
            using (SHA256 sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", "");
        }

        private static PatchInfo Set(PatchInfo info, PatchState state, string message)
        { info.State = state; info.Message = message; return info; }

        private static int Count(string source, string value)
        {
            int count = 0, start = 0, index;
            while ((index = source.IndexOf(value, start, StringComparison.Ordinal)) >= 0) { count++; start = index + value.Length; }
            return count;
        }
    }
}

using System;
using System.IO;
using System.Text;
using System.Windows.Forms;
using WendysDash;

internal static class PatchServiceTests
{
    private const string Engine = "Game_Player.prototype.updateDashing = function() { this._dashing = true; };\nGame_CharacterBase.prototype.realMoveSpeed = function() { return this._moveSpeed + (this.isDashing() ? 1 : 0); };";
    private const string Map = "// Synthetic fixture; no game code.\nGame_Player.prototype.isDashing=function(){return false;};\n";
    private static int passed;

    [STAThread]
    private static int Main(string[] args)
    {
        try
        {
            if (args.Length == 1 && args[0] == "--ui")
            {
                using (Fixture f = new Fixture(Map)) {
                    Application.EnableVisualStyles();
                    Application.SetCompatibleTextRenderingDefault(false);
                    Application.Run(new MainForm(new PatchService(delegate { return false; }), f.Root));
                }
                return 0;
            }
            Test("apply, backup, repeat apply, and exact undo", delegate {
                using (Fixture f = new Fixture(Map)) {
                    PatchResult applied = f.Service.Apply(f.Root);
                    Check(applied.Changed && File.ReadAllText(applied.BackupPath) == Map, "Original backup not preserved");
                    Check(f.Service.Inspect(f.Root).State == PatchState.Installed, "Installed state missing");
                    string after = File.ReadAllText(f.Target);
                    Check(!f.Service.Apply(f.Root).Changed && File.ReadAllText(f.Target) == after, "Duplicate application changed file");
                    f.Service.Undo(f.Root);
                    Check(File.ReadAllText(f.Target) == Map, "Undo did not restore original bytes");
                    Check(!f.Service.Undo(f.Root).Changed, "Duplicate undo changed file");
                }
            });
            Test("UTF-8 BOM and Unicode survive a round trip", delegate {
                using (Fixture f = new Fixture(Map)) {
                    File.WriteAllText(f.Target, Map + "// caf\u00e9 \u96ea\n", new UTF8Encoding(true));
                    byte[] before = File.ReadAllBytes(f.Target);
                    f.Service.Apply(f.Root); f.Service.Undo(f.Root);
                    Check(PatchService.Hash(before) == PatchService.Hash(File.ReadAllBytes(f.Target)), "Encoding or BOM changed");
                }
            });
            Test("undo preserves unrelated changes before the patch", delegate {
                using (Fixture f = new Fixture(Map)) {
                    f.Service.Apply(f.Root);
                    File.WriteAllText(f.Target, "// another mod\n" + File.ReadAllText(f.Target), new UTF8Encoding(false));
                    f.Service.Undo(f.Root);
                    Check(File.ReadAllText(f.Target) == "// another mod\n" + Map, "Unrelated edit lost");
                }
            });
            Test("both CRLF and LF patch suffixes are recognized", delegate {
                foreach (string suffix in new[] { PatchService.Payload, PatchService.Payload.Replace("\r\n", "\n") }) {
                    using (Fixture f = new Fixture(Map + suffix)) {
                        Check(f.Service.Inspect(f.Root).State == PatchState.Installed, "Suffix not recognized");
                        f.Service.Undo(f.Root);
                        Check(File.ReadAllText(f.Target) == Map, "Suffix undo failed");
                    }
                }
            });
            Test("modified, incomplete, and duplicate markers are refused", delegate {
                foreach (string broken in new[] { PatchService.Payload.Replace("return this._dashing;", "return true;"), PatchService.BeginMarker, PatchService.EndMarker, PatchService.Payload + PatchService.Payload }) {
                    using (Fixture f = new Fixture(Map + broken)) {
                        Check(f.Service.Inspect(f.Root).State == PatchState.Conflict, "Conflict not detected");
                        ExpectFailure(delegate { f.Service.Apply(f.Root); });
                        ExpectFailure(delegate { f.Service.Undo(f.Root); });
                        Check(File.ReadAllText(f.Target) == Map + broken, "Conflict file changed");
                    }
                }
            });
            Test("corrupt backup is never overwritten", delegate {
                using (Fixture f = new Fixture(Map)) {
                    PatchResult installed = f.Service.Apply(f.Root); f.Service.Undo(f.Root);
                    File.WriteAllText(installed.BackupPath, "corrupt backup");
                    ExpectFailure(delegate { f.Service.Apply(f.Root); });
                    Check(File.ReadAllText(f.Target) == Map, "Game file changed despite corrupt backup");
                    Check(File.ReadAllText(installed.BackupPath) == "corrupt backup", "Backup overwritten");
                }
            });
            Test("running-game guard prevents writes", delegate {
                using (Fixture f = new Fixture(Map)) {
                    PatchService running = new PatchService(delegate { return true; });
                    ExpectFailure(delegate { running.Apply(f.Root); });
                    Check(File.ReadAllText(f.Target) == Map, "Running game file changed");
                    Check(Directory.GetFiles(Path.GetDirectoryName(f.Target), "*.bak").Length == 0, "Unexpected backup created");
                }
            });
            Test("second running-game check prevents a late launch", delegate {
                using (Fixture f = new Fixture(Map)) {
                    int calls = 0;
                    PatchService racing = new PatchService(delegate { return ++calls > 1; });
                    ExpectFailure(delegate { racing.Apply(f.Root); });
                    Check(File.ReadAllText(f.Target) == Map, "Late game launch was ignored");
                }
            });
            Test("unsupported engine and missing files are refused", delegate {
                using (Fixture f = new Fixture(Map)) {
                    File.WriteAllText(Path.Combine(f.Root, @"package.nw\js\rmmz_objects.js"), "different engine");
                    Check(f.Service.Inspect(f.Root).State == PatchState.Unsupported, "Unsupported engine accepted");
                    ExpectFailure(delegate { f.Service.Apply(f.Root); });
                    Check(f.Service.Inspect(Path.Combine(f.Root, "missing")).State == PatchState.Missing, "Missing folder accepted");
                }
            });
            Test("known inline modification is detected and reversible", delegate {
                using (Fixture f = new Fixture(Map + PatchService.LegacyOn)) {
                    Check(f.Service.Inspect(f.Root).State == PatchState.LegacyInstalled, "Existing edit not detected");
                    Check(!f.Service.Apply(f.Root).Changed, "Existing edit was patched twice");
                    f.Service.Undo(f.Root);
                    Check(File.ReadAllText(f.Target) == Map + PatchService.LegacyOff, "Inline undo failed");
                }
            });
            Test("a file opened by another writer remains untouched", delegate {
                using (Fixture f = new Fixture(Map)) {
                    using (FileStream lockFile = new FileStream(f.Target, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                        ExpectFailure(delegate { f.Service.Apply(f.Root); });
                    Check(File.ReadAllText(f.Target) == Map, "Locked file changed");
                }
            });
            Console.WriteLine("Passed " + passed + " checks.");
            return 0;
        }
        catch (Exception e) { Console.Error.WriteLine(e); return 1; }
    }

    private static void Test(string name, Action action) { action(); passed++; Console.WriteLine("PASS " + name); }
    private static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
    private static void ExpectFailure(Action action)
    {
        bool failed = false;
        try { action(); } catch (IOException) { failed = true; } catch (InvalidOperationException) { failed = true; }
        Check(failed, "An unsafe operation was not rejected");
    }

    private sealed class Fixture : IDisposable
    {
        internal readonly string Root, Target;
        internal readonly PatchService Service = new PatchService(delegate { return false; });
        private readonly string parent;
        internal Fixture(string source)
        {
            parent = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "WendysDash.Tests"));
            Root = Path.Combine(parent, Guid.NewGuid().ToString("N"));
            Target = Path.Combine(Root, @"package.nw\js\plugins\MapScene.js");
            Directory.CreateDirectory(Path.GetDirectoryName(Target));
            File.WriteAllText(Target, source, new UTF8Encoding(false));
            File.WriteAllText(Path.Combine(Root, @"package.nw\js\rmmz_objects.js"), Engine);
            File.WriteAllText(Path.Combine(Root, @"package.nw\js\plugins\InputAdjustments.js"), "// synthetic input file");
        }
        public void Dispose()
        {
            string resolved = Path.GetFullPath(Root);
            if (!resolved.StartsWith(parent + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) throw new IOException("Unsafe fixture cleanup path");
            if (Directory.Exists(resolved)) Directory.Delete(resolved, true);
        }
    }
}

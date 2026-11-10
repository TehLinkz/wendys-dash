"""Exercise the exact Python program embedded in the downloadable shell script."""
import ast
import fcntl
import json
from pathlib import Path
import re
import subprocess
import tempfile
import types
import unittest
from unittest.mock import patch

ROOT = Path(__file__).resolve().parent.parent
SCRIPT = ROOT / "linux/WendysDash.sh"
source = SCRIPT.read_text().split("<<'WENDYS_DASH_PYTHON'\n", 1)[1].rsplit("\nWENDYS_DASH_PYTHON", 1)[0]
app = types.ModuleType("wendys_dash_script")
exec(compile(source, str(SCRIPT), "exec"), app.__dict__)
ENGINE = b"Game_Player.prototype.updateDashing = function() { this._dashing = true; };\nGame_CharacterBase.prototype.realMoveSpeed = function() { return this._moveSpeed + (this.isDashing() ? 1 : 0); };"
MAP = b"// Synthetic fixture, no game code.\nGame_Player.prototype.isDashing=function(){return false;};\n"


class ScriptTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory(prefix="wendys-dash-script-")
        self.root = Path(self.temp.name)
        self.game = self.make_game(self.root / "Game with spaces")
        self.target = self.game / app.RELATIVE_TARGET
        self.running = patch.object(app, "game_running", return_value=False)
        self.running.start()

    def tearDown(self):
        self.running.stop()
        self.temp.cleanup()

    def make_game(self, folder):
        target = folder / app.RELATIVE_TARGET
        target.parent.mkdir(parents=True)
        target.write_bytes(MAP)
        (folder / "package.nw/js/rmmz_objects.js").write_bytes(ENGINE)
        (folder / "package.nw/js/plugins/InputAdjustments.js").write_bytes(b"// fixture")
        return folder

    def test_apply_repeat_backup_exact_undo(self):
        app.change(self.game)
        installed = self.target.read_bytes()
        self.assertEqual(app.inspect(self.game)[2], "installed")
        self.assertEqual(next(self.target.parent.glob("*.before-hold-x-run-*.bak")).read_bytes(), MAP)
        app.change(self.game)
        self.assertEqual(self.target.read_bytes(), installed)
        app.change(self.game, undo=True)
        app.change(self.game, undo=True)
        self.assertEqual(self.target.read_bytes(), MAP)

    def test_windows_patch_protocol_matches(self):
        cs = (ROOT / "src/PatchService.cs").read_text()
        lines = re.search(r'Payload = String.Join\("\\r\\n", new\[\] \{(.*?)\}\);', cs, re.S).group(1)
        tokens = re.findall(r'"(?:\\.|[^"\\])*"|BeginMarker|EndMarker', lines)
        values = [app.BEGIN if t == "BeginMarker" else app.END if t == "EndMarker" else json.loads(t) for t in tokens]
        self.assertEqual(app.PAYLOAD, "\r\n".join(values).encode())

    def test_bom_unicode_and_unrelated_edits_survive(self):
        original = b"\xef\xbb\xbf" + MAP + "// café 雪\n".encode()
        self.target.write_bytes(original)
        app.change(self.game)
        self.target.write_bytes(b"// other mod\n" + self.target.read_bytes())
        app.change(self.game, undo=True)
        self.assertEqual(self.target.read_bytes(), b"// other mod\n" + original)

    def test_both_newlines_and_legacy_patch(self):
        for suffix in (app.PAYLOAD, app.PAYLOAD.replace(b"\r\n", b"\n")):
            self.target.write_bytes(MAP + suffix)
            app.change(self.game, undo=True)
            self.assertEqual(self.target.read_bytes(), MAP)
        self.target.write_bytes(MAP + app.LEGACY_ON)
        app.change(self.game)
        self.assertEqual(self.target.read_bytes(), MAP + app.LEGACY_ON)
        app.change(self.game, undo=True)
        self.assertEqual(self.target.read_bytes(), MAP + app.LEGACY_OFF)

    def test_conflicts_leave_bytes_unchanged(self):
        for broken in (app.PAYLOAD.replace(b"return this._dashing;", b"return true;"), app.BEGIN.encode(), app.END.encode(), app.PAYLOAD * 2):
            self.target.write_bytes(MAP + broken)
            for undo in (False, True):
                with self.assertRaises(RuntimeError):
                    app.change(self.game, undo)
            self.assertEqual(self.target.read_bytes(), MAP + broken)

    def test_corrupt_backup_not_overwritten(self):
        app.change(self.game)
        app.change(self.game, undo=True)
        backup = next(self.target.parent.glob("*.before-hold-x-run-*.bak"))
        backup.write_bytes(b"corrupt")
        with self.assertRaises(RuntimeError):
            app.change(self.game)
        self.assertEqual(backup.read_bytes(), b"corrupt")
        self.assertEqual(self.target.read_bytes(), MAP)

    def test_running_and_late_launch_guards(self):
        for results in ([True], [False, True]):
            with patch.object(app, "game_running", side_effect=results):
                with self.assertRaises(RuntimeError):
                    app.change(self.game)
            self.assertEqual(self.target.read_bytes(), MAP)
            self.assertFalse(list(self.target.parent.glob("*.bak")))

    def test_lock_and_bad_layout_refused(self):
        with self.target.open("r+b") as locked:
            fcntl.flock(locked, fcntl.LOCK_EX | fcntl.LOCK_NB)
            with self.assertRaises(RuntimeError):
                app.change(self.game)
        (self.game / "package.nw/js/rmmz_objects.js").write_bytes(b"other engine")
        with self.assertRaises(RuntimeError):
            app.change(self.game)
        self.assertEqual(self.target.read_bytes(), MAP)

    def test_discovery_internal_sd_flatpak_and_vdf(self):
        home, media = self.root / "home", self.root / "media"
        internal = self.make_game(home / ".local/share/Steam/steamapps/common/Wendy Quest")
        sd = self.make_game(media / "deck/SD Card/steamapps/common/Wendy Quest")
        flatpak = self.make_game(home / ".var/app/com.valvesoftware.Steam/data/Steam/steamapps/common/Wendy Quest")
        custom = self.make_game(self.root / "Custom library/steamapps/common/Wendy Quest")
        vdf = home / ".local/share/Steam/steamapps/libraryfolders.vdf"
        vdf.write_text('"path" "' + str(self.root / "Custom library") + '"\n"path" "relative/path"')
        (home / ".steam").mkdir()
        (home / ".steam/steam").symlink_to(home / ".local/share/Steam", target_is_directory=True)
        self.assertCountEqual(app.find_games(home, media), [internal, sd, flatpak, custom])

    def test_proton_process_matching(self):
        self.assertTrue(app.contains_game(b"wine\0/run/media/deck/SD Card/WendyQuest.exe\0"))
        self.assertTrue(app.contains_game(b"wine\0Z:\\games\\WendyQuest.exe\0"))
        self.assertFalse(app.contains_game(b"cat\0/notes/WendyQuest.exe.backup\0"))

    def test_cli_through_actual_shell_script(self):
        def run(action):
            return subprocess.run(["bash", str(SCRIPT), "--game-dir", str(self.game), action], text=True, capture_output=True, check=True).stdout
        self.assertEqual(run("--status").strip(), "ready")
        self.assertIn("Patch installed", run("--apply"))
        self.assertEqual(run("--status").strip(), "installed")
        self.assertIn("Patch removed", run("--undo"))
        self.assertEqual(self.target.read_bytes(), MAP)

    def test_dialog_actions_and_cancel(self):
        with patch.object(app.Dialogs, "menu", return_value=None):
            self.assertEqual(app.main(["--game-dir", str(self.game)]), 0)
        self.assertEqual(self.target.read_bytes(), MAP)
        with patch.object(app.Dialogs, "menu", return_value="apply"), patch.object(app.Dialogs, "message") as message:
            self.assertEqual(app.main(["--game-dir", str(self.game)]), 0)
            self.assertIn("Patch installed", message.call_args[0][0])
        with patch.object(app.Dialogs, "menu", return_value="undo"), patch.object(app.Dialogs, "message"):
            self.assertEqual(app.main(["--game-dir", str(self.game)]), 0)
        self.assertEqual(self.target.read_bytes(), MAP)


if __name__ == "__main__":
    unittest.main(verbosity=2)

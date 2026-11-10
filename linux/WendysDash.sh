#!/usr/bin/env bash
# Wendy's Dash 1.1.0 — MIT license — https://github.com/TehLinkz/wendys-dash
# Uses Python 3's standard library. Nothing is downloaded or installed.
set -euo pipefail
if ! command -v python3 >/dev/null 2>&1; then
    message="Wendy's Dash needs Python 3. No game files were changed."
    if command -v kdialog >/dev/null 2>&1; then
        kdialog --title "Wendy's Dash" --error "$message"
    else
        printf '%s\n' "$message" >&2
    fi
    exit 1
fi
python3 - "$@" <<'WENDYS_DASH_PYTHON'
import argparse
import fcntl
import hashlib
import os
from pathlib import Path
import re
import shutil
import subprocess
import sys

TITLE = "Wendy's Dash"
BEGIN = "// BEGIN WENDY_QUEST_HOLD_X_RUN_V1"
END = "// END WENDY_QUEST_HOLD_X_RUN_V1"
PAYLOAD = "\r\n".join([
    "", BEGIN,
    ";(function () {",
    '    "use strict";',
    "    // Xbox X uses the same native dash input as keyboard Shift.",
    '    Input.gamepadMapper[2] = "shift";',
    "    Game_Player.prototype.isDashing = function () {",
    "        return this._dashing;",
    "    };",
    "})();", END, ""
]).encode("utf-8")
LEGACY_ON = b"Game_Player[a15_0x519f09(0x192)]['isDashing']=function(){return this._dashing;}"
LEGACY_OFF = b"Game_Player[a15_0x519f09(0x192)]['isDashing']=function(){return!0x1;}"
MAX_BYTES = 16 * 1024 * 1024
RELATIVE_TARGET = Path("package.nw/js/plugins/MapScene.js")


def read_limited(path):
    with path.open("rb") as stream:
        data = stream.read(MAX_BYTES + 4097)
    if len(data) > MAX_BYTES + 4096:
        raise RuntimeError("The game file is larger than the supported layout.")
    return data


def inspect(folder):
    folder = Path(folder).expanduser().resolve()
    target = folder / RELATIVE_TARGET
    engine_path = folder / "package.nw/js/rmmz_objects.js"
    input_path = folder / "package.nw/js/plugins/InputAdjustments.js"
    if not all(p.is_file() for p in (target, engine_path, input_path)):
        raise RuntimeError("Choose the Wendy Quest game folder containing package.nw.")
    engine = read_limited(engine_path).decode("utf-8")
    if not all(s in engine for s in ("Game_Player.prototype.updateDashing", "this._dashing", "this.isDashing() ? 1 : 0")):
        raise RuntimeError("This game version uses different movement code. No changes were made.")
    before = read_limited(target)
    source = before.decode("utf-8")
    if "Game_Player" not in source:
        raise RuntimeError("The map plugin does not match the expected game code.")
    begins, ends = source.count(BEGIN), source.count(END)
    for suffix in (PAYLOAD, PAYLOAD.replace(b"\r\n", b"\n")):
        if begins == ends == 1 and before.endswith(suffix):
            return target, before, "installed", len(suffix)
    if begins or ends:
        raise RuntimeError("The patch block was modified. Restore or verify the game files before retrying.")
    if before.count(LEGACY_ON) == 1:
        return target, before, "legacy", 0
    return target, before, "ready", 0


def contains_game(command_line):
    return any(arg.strip(b'"').replace(b"\\", b"/").rsplit(b"/", 1)[-1].lower() == b"wendyquest.exe"
               for arg in command_line.split(b"\0"))


def game_running(proc_root=Path("/proc")):
    for process in proc_root.iterdir():
        if not process.name.isdigit():
            continue
        try:
            if contains_game((process / "cmdline").read_bytes()):
                return True
        except (FileNotFoundError, ProcessLookupError, PermissionError):
            pass
    return False


def change(folder, undo=False):
    if game_running():
        raise RuntimeError("Close Wendy Quest, then try again.")
    target, before, state, suffix_length = inspect(folder)
    if (not undo and state != "ready") or (undo and state == "ready"):
        return "No patch is installed." if undo else "The patch is already installed."
    after = (before.replace(LEGACY_ON, LEGACY_OFF) if state == "legacy" else before[:-suffix_length]) if undo else before + PAYLOAD
    digest = hashlib.sha256(before).hexdigest().upper()
    kind = ".before-undo-hold-x-run-" if undo else ".before-hold-x-run-"
    backup = Path(str(target) + kind + digest[:12] + ".bak")
    with target.open("r+b") as stream:
        try:
            fcntl.flock(stream, fcntl.LOCK_EX | fcntl.LOCK_NB)
        except BlockingIOError:
            raise RuntimeError("Another program is changing the game file. Close it and try again.") from None
        if stream.read(MAX_BYTES + 4097) != before or os.fstat(stream.fileno()).st_ino != target.stat().st_ino:
            raise RuntimeError("The game file changed during inspection. Try again.")
        if game_running():
            raise RuntimeError("Close Wendy Quest, then try again.")
        try:
            with backup.open("xb") as saved:
                saved.write(before)
                saved.flush()
                os.fsync(saved.fileno())
        except FileExistsError:
            pass
        if read_limited(backup) != before:
            raise RuntimeError("An existing backup has unexpected contents. It was not overwritten:\n" + str(backup))

        def write_verified(data):
            stream.seek(0)
            stream.write(data)
            stream.truncate()
            stream.flush()
            os.fsync(stream.fileno())
            stream.seek(0)
            if stream.read(MAX_BYTES + 4097) != data:
                raise OSError("File verification failed.")

        try:
            write_verified(after)
        except OSError as error:
            try:
                write_verified(before)
            except OSError:
                raise RuntimeError("Writing and recovery failed. Restore this backup:\n" + str(backup)) from error
            raise RuntimeError("Writing failed. The original file was restored.") from error
    message = "Patch removed. Normal movement restored." if undo else "Patch installed! Hold X or Shift to run at 2x speed. Release to walk."
    return message + "\n\nBackup: " + str(backup)


def children(path):
    try:
        return [p for p in path.iterdir() if p.is_dir()]
    except (FileNotFoundError, PermissionError):
        return []


def find_games(home=None, media=Path("/run/media")):
    home = Path.home() if home is None else Path(home)
    roots = [home / p for p in (".local/share/Steam", ".steam/steam", ".steam/root", ".var/app/com.valvesoftware.Steam/data/Steam")]
    for root in list(roots):
        try:
            vdf = read_limited(root / "steamapps/libraryfolders.vdf").decode("utf-8")
            for raw in re.findall(r'"path"\s+"((?:\\.|[^"\\])*)"', vdf):
                path = Path(raw.replace('\\"', '"').replace("\\\\", "\\"))
                if path.is_absolute():
                    roots.append(path)
        except (FileNotFoundError, PermissionError, UnicodeError):
            pass
    for mount in children(Path(media)):
        roots.append(mount)
        roots.extend(children(mount))
    games = []
    for root in roots:
        candidate = (root / "steamapps/common/Wendy Quest").resolve()
        if (candidate / RELATIVE_TARGET).is_file() and candidate not in games:
            games.append(candidate)
    return games


class Dialogs:
    def __init__(self, terminal=False):
        graphical = not terminal and bool(os.environ.get("DISPLAY") or os.environ.get("WAYLAND_DISPLAY"))
        self.kind = ("kdialog" if shutil.which("kdialog") else "zenity" if shutil.which("zenity") else None) if graphical else None

    def run(self, arguments):
        result = subprocess.run([self.kind, "--title", TITLE] + arguments, text=True, capture_output=True)
        if result.returncode == 1:
            return None  # Cancel closes the patcher without writing anything.
        if result.returncode != 0:
            raise RuntimeError("The desktop dialog could not open. Run this script in Konsole with --terminal.")
        return result.stdout.rstrip("\n")

    def prompt(self, text):
        # Python's stdin contains this embedded program; read interactive input from the terminal.
        try:
            with open("/dev/tty", "w") as output, open("/dev/tty", "r") as terminal:
                output.write(text + " ")
                output.flush()
                return terminal.readline().strip()
        except OSError:
            raise RuntimeError("Open this script with Run in Konsole, or run: bash WendysDash.sh --terminal") from None

    def folder(self):
        if self.kind == "kdialog":
            return self.run(["--getexistingdirectory", str(Path.home())])
        if self.kind == "zenity":
            return self.run(["--file-selection", "--directory"])
        return self.prompt("Wendy Quest game folder (blank to cancel):") or None

    def menu(self, text, choices):
        if self.kind == "kdialog":
            args = ["--menu", text]
            for key, label in choices:
                args.extend([key, label])
            return self.run(args)
        if self.kind == "zenity":
            args = ["--list", "--text", text, "--column", "Action", "--column", "Description", "--hide-column", "1", "--print-column", "1", "--width", "600", "--height", "360"]
            for key, label in choices:
                args.extend([key, label])
            return self.run(args)
        lines = [text] + [key + ": " + label for key, label in choices]
        return self.prompt("\n".join(lines) + "\nChoose an action (blank to cancel):") or None

    def message(self, text, error=False):
        if self.kind:
            try:
                self.run((["--error" if error else "--msgbox", text] if self.kind == "kdialog" else ["--error" if error else "--info", "--text", text, "--width", "560"]))
                return
            except RuntimeError:
                pass
        print(text, file=sys.stderr if error else sys.stdout)


def main(argv=None):
    parser = argparse.ArgumentParser(description="Wendy's Dash 1.1.0 — hold X to run in Wendy Quest.")
    parser.add_argument("--game-dir", type=Path, help="Wendy Quest installation folder")
    action = parser.add_mutually_exclusive_group()
    action.add_argument("--apply", action="store_true", help="Apply directly to --game-dir")
    action.add_argument("--undo", action="store_true", help="Undo directly in --game-dir")
    action.add_argument("--status", action="store_true", help="Print status without writing")
    parser.add_argument("--terminal", action="store_true", help="Use text prompts instead of desktop dialogs")
    args = parser.parse_args(argv)
    direct = args.apply or args.undo or args.status
    if direct and args.game_dir is None:
        parser.error("--apply, --undo and --status require --game-dir")
    ui = Dialogs(terminal=args.terminal or direct)
    try:
        if direct:
            print(inspect(args.game_dir)[2] if args.status else change(args.game_dir, undo=args.undo))
            return 0
        folder = args.game_dir
        if folder is None:
            games = find_games()
            if len(games) == 1:
                folder = games[0]
            elif len(games) > 1:
                choice = ui.menu("Choose your Wendy Quest installation:", [(str(i), str(p)) for i, p in enumerate(games)] + [("browse", "Browse for another folder")])
                if choice is None:
                    return 0
                folder = games[int(choice)] if choice.isdigit() and int(choice) < len(games) else None
            if folder is None:
                folder = ui.folder()
        while folder:
            folder = Path(folder).expanduser().resolve()
            try:
                state = inspect(folder)[2]
                installed = state in ("installed", "legacy")
                text = str(folder) + ("\n\nThe patch is installed." if installed else "\n\nReady to enable hold X to run.")
                choices = [("undo", "Undo patch")] if installed else [("apply", "Apply patch")]
            except (RuntimeError, OSError, UnicodeError) as error:
                text, choices = str(error), []
            choice = ui.menu(text, choices + [("browse", "Choose another game folder"), ("exit", "Exit")])
            if choice in (None, "exit"):
                return 0
            if choice == "browse":
                folder = ui.folder()
            elif choice in dict(choices):
                ui.message(change(folder, undo=choice == "undo"))
                return 0
        return 0
    except (RuntimeError, OSError, UnicodeError) as error:
        ui.message(str(error), error=True)
        return 1


if __name__ == "__main__":
    sys.exit(main())
WENDYS_DASH_PYTHON

# Wendy's Dash for Linux and Steam Deck

**`WendysDash.sh` is the entire Linux patcher.** It has simple Apply/Undo dialogs and detects Wendy Quest on internal storage and SD cards. No installer, .NET runtime, pip packages, administrator password, or SteamOS system changes are needed. It uses Bash and Python 3's standard library, plus KDialog or Zenity for desktop dialogs. Without a dialog tool, it offers text prompts in Konsole.

## Use on Steam Deck

1. Close Wendy Quest and switch to **Desktop Mode**.
2. Download **`WendysDash.sh`** into Downloads.
3. Open **Konsole** and run:

   ```sh
   bash ~/Downloads/WendysDash.sh
   ```

4. Check the detected game folder, then choose **Apply patch**. If the game isn't found, a folder picker opens.
5. Return to Gaming Mode and launch Wendy Quest normally. **Hold X to run; release it to walk.** Keyboard Shift also works.

After downloading, you can also enable **Properties → Permissions → Is executable** and run the script from Dolphin. The small `.tar.gz` download preserves the script's executable permission when extracted.

To undo, close the game and run the same script again. It offers **Undo patch** when the patch is installed. The script recognizes patches made by the Windows application too.

## Find the game manually

In Steam's Desktop Mode library, open Wendy Quest's **Properties → Installed Files → Browse**. Choose that game folder, containing `package.nw`. An internal library typically uses:

```text
/home/deck/.local/share/Steam/steamapps/common/Wendy Quest
```

SD cards commonly mount under `/run/media/`. Select the actual game directory, not the Proton `compatdata` folder. Auto-detection checks internal, custom, SD-card, and Flatpak Steam libraries.

## Troubleshooting

- **No dialogs:** run `bash ~/Downloads/WendysDash.sh --terminal` for text prompts.
- **Python 3 missing:** the script stops without changing any files. Other Linux distributions need Python 3 installed; no extra Python packages are used.
- **Game running:** close Wendy Quest before applying or undoing. The script checks Proton/Wine process arguments too.
- **No change to X:** your Steam Input layout must send the normal gamepad X button. Steam file verification or a game update may remove the patch; rerun the script if it says it is ready to apply.

The script creates verified backups beside `package.nw/js/plugins/MapScene.js` and shows their locations after each change. Save files are never read or edited. A modified patch block or incompatible movement code is refused instead of overwritten.

## Command-line options

```sh
bash WendysDash.sh --game-dir "/path/to/Wendy Quest" --status
bash WendysDash.sh --game-dir "/path/to/Wendy Quest" --apply
bash WendysDash.sh --game-dir "/path/to/Wendy Quest" --undo
```

These explicit commands print their results directly and do not open dialogs. `--status` does not write anything.

## Testing

The script is tested through Bash on an Ubuntu CI runner using synthetic game files. Checks cover apply/undo, Windows patch compatibility, backups, conflicting edits, file locks, running-game guards, Steam library discovery, and dialog action/cancellation behavior. **A physical Steam Deck has not been tested yet.** The script does not establish the game's compatibility with a particular Proton version.

Run the tests on Linux with:

```sh
bash -n linux/WendysDash.sh
python3 linux/test_script.py
```

The app does not connect to the network or include any game files.


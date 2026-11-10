# Wendy's Dash v1.1.0 — Linux and Steam Deck

A small, single-file Linux patcher: **`WendysDash.sh`**.

- Run in Steam Deck's Desktop Mode and choose **Apply patch**. Hold X in-game to run; release to walk.
- Auto-detect internal, SD-card, custom, and Flatpak Steam libraries.
- Undo by running the same script again. Windows and Linux use the same patch format.
- Keep verified backups and leave save files untouched.
- No installer, .NET download, or administrator access. Uses Bash, Python 3, and optional KDialog/Zenity dialogs; text prompts also work.

Download `WendysDash.sh`, open Konsole, and run:

```sh
bash ~/Downloads/WendysDash.sh
```

Alternatively, extract `WendysDash-1.1.0-linux.tar.gz` and run the executable script inside. Windows users should download **`WendyQuestDashPatcher.exe`**.

Automated Linux tests pass. **Physical Steam Deck testing is still needed.** No game files are distributed.


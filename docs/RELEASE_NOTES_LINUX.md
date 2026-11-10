# Wendy's Dash v1.1.0 — Linux and Steam Deck candidate

Adds a native Linux x86-64 GUI for Steam Deck's Desktop Mode and other Linux desktops.

- Detect Steam libraries on internal storage and SD cards, including custom libraries and Flatpak Steam.
- Apply and undo the same hold-X-to-run patch used by the Windows version.
- Detect Wendy Quest running through Proton before changing files.
- Bundle .NET so users can extract and launch the app without installing a runtime.
- Keep verified backups and preserve save files.

Download **`WendysDash-1.1.0-linux-x64.tar.gz`**, extract it, and run **`WendysDash`** in Desktop Mode. Keep its supporting files in the same directory. See `STEAM_DECK.md` in the archive for instructions.

The Linux package is checked on Ubuntu CI with patch tests, Steam-library discovery checks, and a GUI apply/undo smoke test. **Physical Steam Deck testing is still needed.** This does not certify the game's Proton compatibility.

This is an unofficial community patch. No game files are distributed.

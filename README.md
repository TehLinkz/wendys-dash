# Wendy's Dash

A small Windows utility that adds **hold-to-run controls** to Wendy Quest. Hold **X** on an Xbox controller or **Shift** on a keyboard to move at twice the normal walking speed. Release to walk again.

An unofficial community project. Not affiliated with or endorsed by Wendy's or the game's creators.

## Use the patcher

1. Download [`WendyQuestDashPatcher.exe`](https://github.com/TehLinkz/wendys-dash/releases/latest/download/WendyQuestDashPatcher.exe) from [Releases](https://github.com/TehLinkz/wendys-dash/releases). A ZIP with usage notes and a checksum is also available.
2. Close Wendy Quest and open `WendyQuestDashPatcher.exe`.
3. Confirm the detected game folder, or select it with **Browse**. Choose the folder containing `WendyQuest.exe` and `package.nw`.
4. Click **Apply patch**, then start the game normally through Steam.

To remove the change, close the game, reopen the patcher, and click **Undo patch**.

The application runs locally. It has no installer, telemetry, network requests, or third-party runtime packages.

## Features

- Steam library detection and manual folder selection.
- Clear installed, ready, running-game, and unsupported-version states.
- Content-verified backups before applying or undoing a patch.
- Repeat installs do not stack the speed increase.
- Undo removes the patch while preserving unrelated changes before its block.
- A local activity window shows results and backup locations.
- Save files are never read or modified.

## Requirements and compatibility

Windows with **.NET Framework 4.8 or newer**, plus an installed copy of the Windows Steam version of Wendy Quest. See Microsoft's [.NET Framework installation guide](https://learn.microsoft.com/en-us/dotnet/framework/install/) for runtime details.

The patcher checks the RPG Maker movement signatures and expected game layout before writing. Installation and exact undo were verified against two locally available map-plugin builds; this does not guarantee compatibility with every future update or other gameplay mod. Controller behavior uses the standard Xbox gamepad mapping. A custom Steam Input layout may need to send the normal X button.

Steam updates or file verification can replace modified game files. If the app subsequently shows **Ready to patch**, reapply it. If it reports unsupported code or a modified patch block, it stops without changing the file.

## What changes

The game already maps Xbox X to its native `shift` action, but its map plugin disables dashing. The patcher appends a small, marked override to:

```text
package.nw/js/plugins/MapScene.js
```

The override maps X to `shift` and returns the engine's existing `_dashing` state from `Game_Player.prototype.isDashing`. The native movement code supplies the speed increase and normal movement restrictions. Keyboard Shift works through the same path. Existing engine behavior for click-to-move also applies.

Backups remain beside `MapScene.js`, with a content hash in the filename. Apply backups use `MapScene.js.before-hold-x-run-<hash>.bak`; undo operations also back up the patched file. The patcher never silently replaces a backup with different contents. The activity window gives the exact path.

If manual recovery is needed, close the game and copy the desired backup over `MapScene.js`. Steam's file verification can also restore distributed game files, but may remove other local modifications.

## Build

No NuGet packages or SDK downloads are needed for the script build. On a Windows machine with .NET Framework 4.8 installed, run this from the repository folder:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\build.ps1 -Test -Package
```

The build script uses the Windows .NET Framework C# compiler, builds the GUI, runs the checks, and creates the release ZIP in `artifacts/`. The execution-policy override applies only to this process.

Alternatively, open `WendysDash.sln` in Visual Studio with the **.NET desktop development** workload and **.NET Framework 4.8 targeting pack**, then build Release. Source code intentionally stays compatible with C# 5 so the script build works without a separate SDK.

For UI development, after running `build.ps1 -Test`, launch:

```powershell
.\artifacts\WendysDash.Tests.exe --ui
```

This opens the real form against a temporary synthetic game folder. Its process check is stubbed only in the test harness; it does not modify an installed game.

## Validation

The dependency-free check runner covers apply and exact undo; duplicate operations; UTF-8 and BOM preservation; preservation of unrelated edits; both common newline styles; modified or duplicate patch markers; corrupt-backup protection; running-game checks; incompatible layouts; an existing inline modification; and files locked by another writer. Test fixtures contain synthetic code and are removed after the run.

GitHub Actions builds, tests, and uploads the portable ZIP on pushes and pull requests. The first successful push to `main` also publishes `v1.0.0`, including the directly downloadable executable. If that release already exists, the workflow leaves it unchanged. Pull requests never publish releases. The workflow uses the official [checkout](https://github.com/actions/checkout) and [upload-artifact](https://github.com/actions/upload-artifact) actions.

## Publishing a release

1. Push this source tree to your repository.
2. Run `build.ps1 -Test -Package`, or download the successful build artifact from Actions.
3. The initial push to `main` publishes `v1.0.0` automatically after all checks pass. For manual publication, attach `WendyQuestDashPatcher.exe`, `WendysDash-1.0.0-win.zip`, and `SHA256SUMS.txt` to the release.
4. Use [`docs/RELEASE_NOTES.md`](docs/RELEASE_NOTES.md) for the release description.

The binary is unsigned. A public publisher can add Authenticode signing with their own certificate. The source and release package contain no game code or assets.

## License

[MIT](LICENSE), covering this patcher and its original icon. Wendy Quest and its associated names and assets belong to their respective owners.

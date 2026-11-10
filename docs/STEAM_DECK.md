# Wendy's Dash for Linux and Steam Deck

The native Linux GUI applies the same hold-X-to-run modification as the Windows patcher. It targets **Linux x86-64**, including Steam Deck in **Desktop Mode**, and patches the Windows Steam game installed for use through Proton.

The .NET runtime is included. You do not need to install .NET, use an administrator password, or disable SteamOS's read-only system protection. Normal desktop graphics libraries are still required.

## Steam Deck steps

1. Close Wendy Quest. Switch your Deck to **Desktop Mode**.
2. Download `WendysDash-1.1.0-linux-x64.tar.gz` and extract it into a folder in your home directory, such as Downloads.
3. Open the extracted folder and launch **`WendysDash`**. Keep the other files beside it. The `Launch-WendysDash.sh` script also starts the app.
4. Confirm the detected game folder or use **Browse**. Auto-detection checks Steam's internal library, `libraryfolders.vdf`, SD-card mount roots, and the Flatpak Steam location.
5. Click **Apply patch**, close the patcher, and return to Gaming Mode. Launch the game normally through Steam.
6. Hold the Deck's **X** button to run; release it to walk. Your Steam Input layout should send the standard gamepad X button. Keyboard Shift also works.

To undo, close the game, open the patcher in Desktop Mode, and click **Undo patch**. The Windows and Linux patchers recognize the same patch format.

If the file manager does not treat the launcher as executable, open its **Properties → Permissions** and enable execution. For a terminal launch from the extracted folder:

```sh
./WendysDash
```

## Finding the game manually

In Steam's Desktop Mode library, open Wendy Quest's properties and use **Installed Files → Browse**. Select that folder in the patcher. A typical internal path is:

```text
/home/deck/.local/share/Steam/steamapps/common/Wendy Quest
```

An SD card usually lives under `/run/media/`. Choose the actual game folder containing `package.nw`, not its Proton compatibility-data folder.

## Backups and compatibility

Backups are stored beside `package.nw/js/plugins/MapScene.js`, with a content hash in each filename. The activity panel shows the exact path. Save files are never read or edited. Steam updates and file verification may replace the patch; run the patcher again afterward if it reports **Ready to patch**.

The Linux build includes automated patch tests, library-discovery tests, Proton-process matching checks, and a GUI apply/undo smoke test on an Ubuntu CI runner. **It has not been tested on a physical Steam Deck.** Treat it as a Steam Deck candidate until that hardware check is complete. This utility does not establish whether the underlying game works with a particular Proton version.

For other Linux desktops, Avalonia requires X11/XWayland and desktop libraries such as libX11, libICE, libSM, and fontconfig. See [Avalonia's Linux deployment documentation](https://docs.avaloniaui.net/docs/deployment/linux). Valve documents how [Proton runs Windows games on SteamOS](https://partner.steamgames.com/doc/steamhardware/proton).

## Build on Linux

Install the .NET 10 SDK, then run:

```sh
bash linux/package.sh
```

The script runs shared and Linux-specific tests, publishes a self-contained `linux-x64` app, and produces the `.tar.gz` and checksum in `artifacts/`. If `xvfb-run` is available, it also verifies the GUI against synthetic game files. CI requires this GUI check. No actual game files are needed for tests or included in the download.

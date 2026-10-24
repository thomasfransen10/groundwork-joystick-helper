GROUNDWORK JOYSTICK HELPER 0.3.0
Community compatibility helper for Windows

QUICK START
1. Extract the whole ZIP to a folder you can write to.
2. Connect your controllers and install their manufacturer's normal drivers.
3. Open Groundwork-Joystick-Helper.exe. Keep Steam open.
4. The helper searches your Steam libraries for Groundwork Simulator.
   If necessary, use Browse to select the ORIGINAL game.exe.
5. Select a controller in the list and move its axes / press buttons.
   The live test shows the native input available through Windows.
6. Save your game and close it before clicking Start game.
7. In the game, open Controls > Joystick > Set up sticks. Assign the desired
   left and right devices, then bind directions/buttons and calibrate them.
8. Use this helper for subsequent starts while this workaround is needed.

WHAT IS AUTOMATIC
- Discovery of the Steam installation, including additional library drives.
- Discovery and hot-plug refresh of Windows Gaming Input controllers.
- Display of available axes, buttons, native readings and friendly names.
- Verification of the supported game build.
- Creation and verification of a separate compatible game copy.

WHAT THE COMPATIBILITY MODE DOES
The supported game build classifies joysticks using words in device names.
Localized Windows names such as HID-spelbesturing can fail this check.
This helper removes that name restriction inside the game's existing gaming-
controller pipeline. It is independent of manufacturer, model, USB IDs and
the language of the device name. Existing raw axes and buttons stay native.

The helper changes exactly two byte values in a separate executable named
game-joystickhelper.exe beside the original game.exe. It retains the game's
existing input-reading and cleanup code. The original Steam game.exe and the
earlier game-joystickfix.exe are not overwritten.

Gaming controllers and wheels may also appear in the game's joystick menu.
Assign the two devices you actually want to operate. Left/right roles, axis
directions, buttons and calibration are configured in the game, not guessed
from an unknown controller's physical layout.

SUPPORTED SCOPE
- Windows 10/11, 64-bit, with the Windows Gaming Input interface available.
- The installed Microsoft .NET Framework runtime; no .NET SDK is required.
- Original Groundwork Simulator Steam build 26171350, version 0.1.1.
- Controller devices that Windows exposes through RawGameController and that
  supply usable axis/button input. The live test helps check that first.
- Write access to the game's installation folder to create the separate copy.

This is not a universal USB translator or a T.16000M emulator. It does not
install a virtual joystick driver. DirectInput-only devices, proprietary
protocols, missing input, driver faults and unsupported game builds are not
repaired by this name-filter workaround.

TEST STATUS
The automatic detector has been checked with a T.Flight HOTAS One and a
SOL-R Flightstick. Both supplied real native Windows input. After launching
this general release, the user tested the sticks separately and confirmed
that both respond in the game's joystick setup menu. See VERIFICATION.txt
for the validation details. Other controller models remain untested.

UPDATES AND FILE PROTECTION
The complete original game SHA256 must match:
6B3703C22C8B0F67A2D35089A22E9B202921B1F466821D3DB16097D8A8EC5898
Unknown, modified and updated game images are refused. The helper does not
blindly patch a new version and does not overwrite a different existing copy.
The compatible copy still uses the normal game assets, settings and saves.
The game may update its own settings/saves during normal use.

LOCAL DATA AND REVERSAL
No network services or telemetry are used by the helper. No firmware, USB IDs,
drivers, registry settings or system environment variables are changed.
Before launch, the helper copies the existing game settings to:
%LOCALAPPDATA%\GroundworkJoystickHelper\backups
Local start/error messages are written to helper.log in that same parent folder.
These runtime files are not included in the distribution ZIP.

To return to normal, close the compatible game copy and launch from Steam.
Steam uses the preserved original game.exe. The additional executable can be
removed later. Left/right bindings made inside the game are normal settings;
the backup can be used if you want to restore the earlier configuration.

SHARING
Share the clean ZIP. It contains the helper, these instructions, source code,
verification notes and checksums. Do not bundle a game executable, your game
saves, runtime logs or settings backups. Recipients use their own installation.

SOURCE AND REFERENCES
Source is in source/. build.ps1 rebuilds the helper with the Windows compiler.
Microsoft RawGameController documentation:
https://learn.microsoft.com/en-us/uwp/api/windows.gaming.input.rawgamecontroller
Microsoft native reading documentation:
https://learn.microsoft.com/en-us/uwp/api/windows.gaming.input.rawgamecontroller.getcurrentreading
This is an independent, unofficial compatibility helper.

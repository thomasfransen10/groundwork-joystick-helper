# Groundwork Joystick Helper

Unofficial Windows compatibility helper for **Groundwork Simulator 0.1.1 / Steam build 26171350**.

## Security status: binary distribution on hold

The original locally built helper version 0.3.0 was submitted to VirusTotal on **24 October 2026**. **4 of 71 scanners flagged it**. The detections are unresolved; they have **not** been established as false positives. We are withholding the ready-to-run ZIP while this is investigated.

[View the full VirusTotal report](https://www.virustotal.com/gui/file/68b9994f0e17c5e5a454b8ea97b5a3e926dc5498e361c8a751faf7c0f3b88f3d)

**Update:** a fresh GitHub-hosted build was also scanned and received the same four detections (4/70, one engine timed out). A byte comparison found that both builds are identical except for their compilation timestamp and module GUID. See [INVESTIGATION.md](INVESTIGATION.md) for both scan reports, the public build log and comparison data. The release remains on hold.

Source is published for inspection. Functional testing is not a security audit. This project is unsigned, has no independent security certification, and makes no promise that all controllers work. See [SECURITY.md](SECURITY.md) for the exact scan findings and review scope.

## What it does

The game rejects some valid joysticks because it classifies them by their Windows device names. Localized names such as `HID-spelbesturing` do not match its list.

The helper automatically finds Steam libraries, lists Windows Gaming Input controllers, and shows their native axis/button readings. When the player chooses **Start game**, it verifies the complete original game hash, creates a separate `game-joystickhelper.exe`, changes exactly two bytes in that copy's controller-name classifier, verifies the copy, backs up existing settings and launches the copy with the game's Steam identity. The original `game.exe` is preserved.

It does not emulate a T.16000M, install drivers or change USB identities. Left/right roles, bindings and calibration are assigned in the game. Other gamepads and wheels may appear in the joystick list too.

## Scope and tested hardware

- Windows 10/11 x64 with Windows Gaming Input and the installed .NET Framework. Windows 10 was physically tested; Windows 11 was not.
- Only the original supported game image is accepted. An updated or modified image is refused.
- Devices must expose usable input through Windows Gaming Input. DirectInput-only devices and driver faults are outside this workaround.
- **T.Flight HOTAS One and SOL-R [R] Flightstick:** automatic discovery and native input were checked; the user separately moved both and confirmed that both respond in this general version's in-game setup menu.
- Other physical models remain untested. See [VERIFICATION.txt](VERIFICATION.txt) for the 25 development assertions and the real launch/input test.

## Inspect and build

The complete program is in `source/Core.cs`, `source/Controllers.cs` and `source/App.cs`. `source/Tests.cs` contains the development tests; several require the actual installed game and physical controllers and do not run on a generic CI runner.

On Windows, `source/build.ps1` uses the installed Microsoft .NET Framework compiler. No third-party package dependency is downloaded. The GitHub Actions workflow compiles the same source on a fresh hosted Windows runner and exposes its log and artifact for review. It does not run the helper, the game or the hardware tests, and does not certify the output as safe. CI builds may differ byte-for-byte from the original local build; the old scan report applies only to its exact hash.

## Files and local effects

- Reads controller input and read-only Steam/joystick registry information.
- Writes the separate compatible game copy beside the original, requiring write access to that folder.
- Writes settings backups and a local log under `%LOCALAPPDATA%\GroundworkJoystickHelper`.
- Optional command-line development modes write a local diagnostic or rendered preview to the requested path.
- No network client, telemetry, driver installer, persistence mechanism, elevation request, credential access or security-setting change is implemented in the published source.

The compatible game uses the normal game assets, saves and settings. To return to the original, close it and launch through Steam. See [README.txt](README.txt) and [LEESMIJ.txt](LEESMIJ.txt) for full instructions; their ZIP references describe the earlier local test package, which is currently withheld from public release.

This repository contains no proprietary game executables, saves, personal settings, runtime logs or settings backups.

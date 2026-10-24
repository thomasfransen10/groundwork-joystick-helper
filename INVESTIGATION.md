# Investigation results - 24 October 2026

**The binary release remains on hold. Both builds have unresolved detections.**

## Fresh build from the public source

[GitHub Actions run 40106765842](https://github.com/thomasfransen10/groundwork-joystick-helper/actions/runs/40106765842) compiled commit `750d15468b19670c44b82e0a30a00c020eb7ab69` on a GitHub-hosted Windows 2022 runner. The job succeeded. It compiled the published source without running the helper or the game. The temporary artifact is explicitly named `investigation-build-not-a-release`; it is not an approved download.

The downloaded artifact archive hash was verified against the digest displayed by GitHub:

`1EEEDCA11D21EEF9D7B1B023281B5794959FAF41B31C609FE45C35DB5A2045C1`

| Build | Executable SHA256 | VirusTotal result observed |
| --- | --- | --- |
| Original local 0.3.0 | `68B9994F0E17C5E5A454B8EA97B5A3E926DC5498E361C8A751FAF7C0F3B88F3D` | 4/71 |
| Fresh GitHub build | `F0D57CDF028327E0DF124033C1184C2C0F2728FDED44069DBEBAB13616C9360D` | 4/70, with Google timing out |

[Original report](https://www.virustotal.com/gui/file/68b9994f0e17c5e5a454b8ea97b5a3e926dc5498e361c8a751faf7c0f3b88f3d) - [Fresh-build report](https://www.virustotal.com/gui/file/f0d57cdf028327e0df124033c1184c2c0f2728fded44069dbebab13616c9360d)

The same four vendors flagged both files: Elastic, MaxSecure, McAfee Scanner and SecureAge. McAfee's label includes each file's hash prefix. No finding has been established as a false positive. Results can change over time.

## Byte comparison

Both executables are 36,352 bytes. Seventeen byte values differ, all within:

- Four-byte PE compilation timestamp at file offset 136.
- Sixteen-byte single module GUID/MVID heap at file offset 31,832.

After normalizing only these two build-generated fields, **every other byte is identical**, including the executable code, resources and remaining metadata. The normalized SHA256 for both is:

`3EE3DAA415910F2183E1F7518AF505516EA997E4CF44BF533F8365F35B422801`

See [BINARY-COMPARISON.json](BINARY-COMPARISON.json). [compare-builds.py](compare-builds.py) repeats the read-only comparison; it requires Python and the `pefile` library. Run it with the original executable path followed by the fresh executable path. It does not execute either file.

This is evidence of correspondence between the public-source build and the original local executable. It does not establish software safety or explain the antivirus detections. It is not an independent audit or a claim of exact reproducible output.

## Disposition

The original helper has not been modified to reduce detection counts. The local ZIP was updated only to add the unresolved-scan notice; its executable is unchanged. No ready-to-run release has been published.

The next external step is vendor review of the existing files and their source, not renaming, packing or changing code to avoid scanners. Vendor findings or a game-developer-supported fix should be documented before revisiting public binary distribution.

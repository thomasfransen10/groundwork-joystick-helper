# Security and scan record

## Binary release is on hold

Do not describe the original 0.3.0 helper as clean or independently audited. Its VirusTotal findings are unresolved. Do not disable security software or add exclusions to run it. Source availability and a successful build do not prove safety.

## Exact original file submitted

- File: `Groundwork-Joystick-Helper.exe`
- Size: 36,352 bytes
- SHA256: `68B9994F0E17C5E5A454B8EA97B5A3E926DC5498E361C8A751FAF7C0F3B88F3D`
- Digital signature: absent
- Report observed: 24 October 2026, **4/71 detections**
- [Full VirusTotal report](https://www.virustotal.com/gui/file/68b9994f0e17c5e5a454b8ea97b5a3e926dc5498e361c8a751faf7c0f3b88f3d)

| Vendor | Reported detection |
| --- | --- |
| Elastic | Malicious (moderate Confidence) |
| MaxSecure | Trojan.Malware.300983.susgen |
| McAfee Scanner | Ti!68B9994F0E17 |
| SecureAge | Malicious |

Results may change. Neither the detection count nor this project's own review establishes whether any finding is a false positive. A scan report identifies one exact file; it does not cover modified or rebuilt executables.

## Review scope

The published source was read to identify file writes, process launches, registry operations and native calls. No network access, credential collection, autostart installation, downloaded payload, arbitrary command execution or security-setting changes were found in that source. This was a first-party review, not an independent audit, and it does not prove that the old executable exactly matches the source.

Expected operations are documented in README.md. The helper deliberately creates a locally modified copy of a supported game executable. That fact must remain visible; it must not be concealed as part of resolving detections.

The next comparison is a fresh build from the public source on a GitHub-hosted Windows runner. It is for provenance and investigation. It is not a claim of bit-for-bit reproducibility, an attempt to evade scanners, or a security approval. No ready-to-run release will be described as clean based solely on a rebuilt file receiving fewer detections.

## Report an issue

For non-sensitive technical findings, open a GitHub issue with the release version, file hash, vendor/report link and a minimal description. Do not include saves, device identifiers, private logs, credentials or other personal data in public issues.

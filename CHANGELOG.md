# Changes

## Unreleased

- Optional encrypted last-session previews with timestamps, clear control and computer illustration fallback.
- Shorter normal connection status; detailed warnings remain visible.
- Synthetic capture/privacy/cache and preview preference UI checks. Real RDP capture remains conditional on the installed renderer.


## 0.1.0-preview.2

First public testing preview. The goal is a Windows connection manager with a saved-PC workflow inspired by Windows App on Mac.

Includes the native PC library, groups/favorites/search, saved settings, credential profiles, integrated RDP session code, import/export, backups, and optional per-user installation. Adds a simple testing guide and GitHub forms for problems and suggestions.

This is not a complete feature-parity release. Real RDP sessions and host-dependent settings still need feedback from Windows testers. See [verification](docs/VERIFICATION.md) for observed results and [feature status](docs/CAPABILITIES.md) for unavailable features.

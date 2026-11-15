# Changelog

All notable changes to QuietFan are listed here. Versions follow [Semantic Versioning](https://semver.org/).

## [1.0.0] - 2026-11-15

### Added

- Tray icon with the board temperature, fan speed, CPU load, fan profile and boost state.
- Smart, Quiet and Full CPU boost policies; Smart turns boost on only under sustained load.
- ASUS fan profile switching (Whisper, Standard, Performance) through the ASUS ATK ACPI interface.
- Start with Windows option.
- Restores the original boost setting on exit, after a crash or shutdown, and on uninstall.
- `--exit` and `--restore` command-line options.
- Per-user installer with uninstaller, portable zip and SHA-256 checksums.
- Simplified Chinese and English user interface.

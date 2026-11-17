# Changelog

## Custom Games — development preview (2026-11-17)

### Added
- Local custom game registration via **Add custom game**.
- Persistent custom entries in `CustomGames.json`.
- Integration with existing game screens and UEVR profile/injection workflow.

### Fixed in source
- Removed conflicting helper PreBuildEvents.
- Added helper project build dependency and automatic staging of command helper runtime files.
- Updated ignore rules to exclude generated `bin` and `obj` folders.

### Status
The original Windows Debug build and a manually staged helper were tested successfully. The automatic copy target has not yet been independently tested on Windows. No Release build was created.

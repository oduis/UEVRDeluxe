# UEVR Easy Injector — WoozyFace Custom Games fork

Unofficial source fork of [UEVRDeluxe](https://github.com/oduis/UEVRDeluxe), adding **Add custom game** for local Windows Unreal Engine games and development builds.

## Features added in this fork

- Manually register any existing local Windows game executable (`.exe`) without store detection.
- Store registered games in `%APPDATA%\UEVRDeluxe\CustomGames.json` for the next start or rescan.
- Reuse the existing game pages, local profiles and UEVR injection workflow.
- Launch custom games using the executable's own directory as the working directory.
- Stage complete `UEVRDeluxeCmd` runtime files automatically after a future Windows build.

## Usage

1. Start the app normally and select **Add custom game**.
2. Choose the correct game executable. Some Unreal Engine games launch a second executable from `Binaries\Win64`.
3. Open the entry, configure a local profile, then start the game and inject using the existing flow.

The picker can fail when the application is running elevated, while injection into certain processes may need administrator permissions.

## Current build status

- **Windows Debug/developer build:** previously tested, including local game registration and UEVR injection.
- **Backend updates:** tested after manually placing the complete command helper files into `Cmd`.
- **New automatic helper packaging in source:** prepared but not yet verified with a new Windows build.
- **Release build:** not yet available; the public upstream repository lacks `CompiledSecret_RELEASE.cs`.

The existing Windows developer build is separate and is not included in this source branch. You can commit source from macOS using GitHub without rebuilding Windows binaries.

## Project information

See [BUILDING.md](BUILDING.md), [CHANGELOG.md](CHANGELOG.md), [RELEASE_NOTES.md](RELEASE_NOTES.md), and [PUBLISHING.md](PUBLISHING.md).

Original project: [Oliver Duis / UEVRDeluxe](https://github.com/oduis/UEVRDeluxe). UEVR engine by Praydog. The original license and attribution are retained.

# Windows development build

This project uses .NET 10 and WinUI 3 on Windows x64. The existing Windows **Debug** build has already been tested. You do **not** need to build it again to commit source changes from macOS.

For a future clean local build:

```powershell
dotnet restore ".\UEVRDeluxe.slnx"
dotnet build ".\UEVRDeluxe.slnx" -c Debug -p:Platform=x64 --no-restore
```

The source now includes an automatic helper dependency and build/publish output copy target. **This new MSBuild automation is not yet Windows-verified.**

Expected files under the app output's `Cmd` folder: `UEVRDeluxeCmd.exe`, `UEVRDeluxeCmd.dll`, `UEVRDeluxeCmd.deps.json` and `UEVRDeluxeCmd.runtimeconfig.json`.

The upstream source is missing `CompiledSecret_RELEASE.cs`, so the public code alone does not currently produce a full Release build.

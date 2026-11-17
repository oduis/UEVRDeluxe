# GitHub source sync and future release

The GitHub repository is synchronized independently of your Windows developer build. The Windows EXE does **not** need to be rebuilt just to push the source.

The original Windows Debug binary is separate and can be packaged later. No GitHub Release or tag is created by this source sync.

For a future binary package, include the complete WinUI app output and the helper's EXE, DLL, deps JSON and runtimeconfig JSON under `Cmd`; test the archive on Windows first.

Never commit generated `bin` or `obj` output to the source branch. Existing tracked build artifacts are removed from the branch separately.

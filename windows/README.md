# Sprint Dock for Windows

Sprint Dock for Windows is a native WPF desktop application targeting Windows 10 and Windows 11 on x64 PCs.

## Build

On Windows with the .NET 8 SDK installed:

```powershell
dotnet run --project SprintDock.Windows.Checks/SprintDock.Windows.Checks.csproj --configuration Release
dotnet publish SprintDock.Windows/SprintDock.Windows.csproj --configuration Release --runtime win-x64 --self-contained true --output artifacts/publish
```

The app is configured as a self-contained, single-file executable. It stores sprint state locally in `%LOCALAPPDATA%\SprintDock\state.json`.

The repository workflow `.github/workflows/windows-build.yml` performs the build on a real Windows runner and packages a ZIP containing only `SprintDock.exe`.

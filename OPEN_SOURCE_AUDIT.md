# Open-Source Preparation Audit

Date: 26 August 2026
Scope: SlicerLauncher 1.0.0 source snapshot prior to MSIX/Microsoft Store migration.

## Result

No hard-coded passwords, API keys, authentication tokens, private keys, connection strings, or other obvious secrets were found in the reviewed text source files.

No external NuGet `PackageReference` entries are present in `SlicerLauncher.csproj`. The application uses the .NET/Windows platform libraries and embedded project assets included in the source snapshot.

The review did not identify code invoking command shells or scripting hosts such as `cmd.exe`, `powershell.exe`, `pwsh.exe`, `wscript.exe`, `cscript.exe`, `mshta.exe`, or `schtasks.exe`.

## Network behaviour identified

The previous silent update metadata check and its `HttpClient` request have been removed from the reviewed source.

No automatic 3DPrintKings or GitHub update endpoint is contacted by SlicerLauncher.

The About window can open `https://www.3dprintkings.ch` in the user's default browser only after a user clicks the website link. No source code for uploading model files, settings, analytics, or telemetry was identified.

## Process launching identified

SlicerLauncher starts configured slicer executables with the selected STL/3MF file path. This is the application's core function.

The application can also ask Windows to open the Default Apps settings page and can open the project website through the default shell handler after a user action.

No arbitrary command-shell construction was identified in the reviewed source.

## Registry behaviour identified

The current 1.0.0 source can create and remove SlicerLauncher's own per-user registry entries for STL/3MF file associations under `HKEY_CURRENT_USER`.

The reviewed removal operations target SlicerLauncher-specific keys created for this purpose.

This registry-based association mechanism is planned to be replaced by MSIX manifest declarations for the Microsoft Store build.

## Local file behaviour identified

The application creates its AppData configuration directory and reads/writes:

`%APPDATA%\SlicerLauncher\settings.json`

It can read a legacy `config.xml` for migration. Recent model-file paths are stored in local settings; model-file contents are not intentionally copied or uploaded by SlicerLauncher.

## Build scripts

The PowerShell build scripts remove and recreate only project build-output folders/files under `dist`.

The installer build checks whether Inno Setup 6 is installed. It does not automatically download or install Inno Setup.

## Assets

The source snapshot contains the following embedded/project assets:

- `SlicerLauncher.ico`
- `circle_icon.png`
- `logo_about.png`

They are treated as project/3DPrintKings branding assets. Branding considerations are documented in `TRADEMARKS.md`.

## Cleanup performed

- preserved the application source code and behaviour unchanged
- replaced the duplicated/stale README release notes with a concise project README
- added GPL-3.0 license text
- added privacy, security, contribution, and branding documentation
- added a repository-safe `.gitignore`
- adjusted legacy build-script console wording so it does not encourage direct website distribution during the Store migration
- retained the existing installer, portable build tooling, and registry association code for reproducibility until the later MSIX migration steps
- removed the custom version.json update check and its update UI; the version display is now static

## Review limitations

This was a static source review. The current execution environment did not contain the Windows/.NET SDK toolchain required to compile and run the WinForms project here. Functional Windows testing and MSIX-specific testing remain separate later steps.

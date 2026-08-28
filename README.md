# SlicerLauncher

**One file. Any slicer.**

SlicerLauncher is a free and open-source Windows utility for makers who use multiple 3D-printing slicers.

Open STL and 3MF files with SlicerLauncher, or send models directly from Fusion 360. Then choose the slicer you want to use, or let your preferred slicer launch automatically after a countdown you set.

## Get SlicerLauncher

The official version of SlicerLauncher is distributed through the Microsoft Store.

**[Get SlicerLauncher on the Microsoft Store](https://apps.microsoft.com/detail/9mz8zlfmxrrl)**

Official pre-built binaries are distributed through the Microsoft Store only.

## Status

- Version: **1.0.0**
- Framework: **.NET 8 / Windows Forms**
- Platform: **Windows x64**
- License: **GNU General Public License v3.0**
- Free to use
- Open source
- No advertising
- No analytics
- No tracking
- No telemetry
- Updates distributed through the Microsoft Store

## Why SlicerLauncher?

Many makers use different slicers for different printers and projects.

STL and 3MF files are normally associated with one application in Windows, while workflows such as Fusion 360 may also require choosing a specific Print Utility.

SlicerLauncher provides one simple entry point.

You can:

- open an STL or 3MF file with SlicerLauncher
- send a model directly from Fusion 360
- choose which configured slicer should open the file
- configure a preferred slicer
- optionally launch that slicer automatically after a countdown you set
- stop the countdown at any time and choose another slicer

## Features

- Open STL and 3MF files with the slicer of your choice
- Fusion 360 Print Utility integration
- Automatic detection of popular slicers
- Add custom slicers manually
- Sort and configure your slicer list
- Configurable default slicer
- Optional automatic launch with adjustable countdown
- Stop the countdown and choose another slicer
- Recent files list
- Windows integration for STL and 3MF files
- Local per-user settings
- Microsoft Store installation and updates

## Automatically detected slicers

SlicerLauncher currently detects common installations of:

- Bambu Studio
- ELEGOO Slicer
- OrcaSlicer
- PrusaSlicer
- Creality Print

Other slicers can be added manually by selecting their executable.

Third-party product names are used only to describe compatibility.

SlicerLauncher is an independent project and is not affiliated with, endorsed by, or sponsored by Autodesk, Bambu Lab, Prusa Research, ELEGOO, Creality, or other third-party software vendors unless explicitly stated otherwise.

See [TRADEMARKS.md](TRADEMARKS.md) for details.

## Fusion 360

SlicerLauncher can be configured as a custom Print Utility in Fusion 360.

The Microsoft Store installation provides the execution alias:

```text
SlicerLauncher.exe
```

The corresponding WindowsApps path is typically:

```text
%LOCALAPPDATA%\Microsoft\WindowsApps\SlicerLauncher.exe
```

This allows Fusion 360 to send exported mesh files to SlicerLauncher.

SlicerLauncher then lets you select the slicer you want to use, or automatically launches your configured default slicer after the countdown you have set.

## Windows file integration

The Microsoft Store package registers SlicerLauncher as an application capable of opening:

- `.stl`
- `.3mf`

Windows users remain in control of their default application choices.

SlicerLauncher does not force itself to become the Windows default application.

## Settings

Settings are stored locally in:

```text
%APPDATA%\SlicerLauncher\settings.json
```

Stored information can include:

- configured slicer names
- slicer executable paths
- slicer order
- default slicer selection
- automatic-launch preference
- countdown duration
- recent model-file paths

The application can also migrate the older `config.xml` format if present.

## Privacy

SlicerLauncher does not contain:

- advertising
- analytics
- tracking
- telemetry

Configuration and recent-file information are stored locally on the user's computer.

SlicerLauncher does not perform a custom automatic update check and does not contact a 3dprintkings or GitHub update endpoint for updates.

Official binary updates are distributed through the Microsoft Store.

See [PRIVACY.md](PRIVACY.md) for details.

## Open source

The complete source code is available in this repository.

The source repository is provided for transparency, development, review, modification and contributions.

Official pre-built SlicerLauncher binaries are distributed through the Microsoft Store.

## Build from source

### Requirements

- Windows 10 or Windows 11
- .NET 8 SDK

Clone the repository and build the application from the repository root.

For example:

```powershell
dotnet restore .\SlicerLauncher\SlicerLauncher.csproj
dotnet build .\SlicerLauncher\SlicerLauncher.csproj -c Release -r win-x64
```

The repository also contains the Windows packaging project used for the Microsoft Store version:

```text
SlicerLauncher.Package
```

Generated build and packaging artifacts are intentionally excluded from the repository.

## Security

Please do not disclose suspected security vulnerabilities in a public issue before they have been reviewed.

See [SECURITY.md](SECURITY.md).

## Contributing

Contributions are welcome.

See [CONTRIBUTING.md](CONTRIBUTING.md).

## License

Copyright © 2026 Nino King · 3dprintkings

SlicerLauncher is licensed under the **GNU General Public License v3.0**.

See [LICENSE](LICENSE).

The GPL grants copyright permissions, but it does not grant trademark or endorsement rights.

The names **3dprintkings** and **SlicerLauncher**, the associated branding, and the SlicerLauncher rocket logo are identifiers of the official project. Forks and modified builds must not present themselves in a way that is likely to be confused with an official 3dprintkings release.

See [TRADEMARKS.md](TRADEMARKS.md).

## Logo and brand assets

The repository contains the approved SlicerLauncher visual assets used by the project, including PNG and Windows icon artwork.

Use of the source code under the GPL does not automatically grant permission to represent a fork or modified version as an official 3dprintkings product.

See [TRADEMARKS.md](TRADEMARKS.md) before using the project names or logos for redistribution or modified builds.

## Official links

- **Microsoft Store:** https://apps.microsoft.com/detail/9mz8zlfmxrrl
- **Website:** https://www.3dprintkings.ch/
- **Source code:** https://github.com/3dprintkings/SlicerLauncher

# SlicerLauncher

**One file. Any slicer.**

SlicerLauncher is a free, open-source Windows utility by 3dprintkings for makers who use multiple 3D-printing slicers. Open an STL or 3MF file with SlicerLauncher, then choose the slicer you want to use. SlicerLauncher can also receive mesh exports from Autodesk Fusion 360.

## Current release

- Version: **1.1.0**
- Framework: **.NET 8 / Windows Forms**
- Target platform: **Windows x64**
- License: **GNU General Public License v3.0**
- Official binary distribution: **Microsoft Store**
- No advertising
- No analytics, tracking, or telemetry

Microsoft Store:
https://apps.microsoft.com/detail/9mz8zlfmxrrl

## What's new in 1.1.0

- Redesigned **Manage Slicers** workflow
- Drag-and-drop slicer ordering
- The first Slicer in the list is the default
- Optional automatic launch of the default Slicer after a configurable countdown
- Duplicate slicer names and executable paths are prevented
- Improved Add/Edit Slicer dialogs
- Improved Fusion 360 setup help
- Updated application and About branding
- Flashforge **Flash Studio** automatic detection added
- UI and layout refinements throughout the application

## Features

- Open STL and 3MF files with a Slicer of your choice
- Receive mesh files from Fusion 360
- Automatic detection of supported Slicers
- Add custom Slicers manually
- Drag and drop to reorder configured Slicers
- First Slicer in the list acts as the default
- Optional automatic launch with adjustable countdown
- Stop the countdown and choose another Slicer
- Recent files list
- Microsoft Store / MSIX file association support
- Settings stored locally in the current user's AppData profile

## Automatically detected Slicers

The current version detects common installations of:

- Bambu Studio
- ELEGOO Slicer
- OrcaSlicer
- PrusaSlicer
- Creality Print
- Flashforge Flash Studio

Other Slicers can be added manually by selecting their executable.

Third-party product names are used only to describe compatibility. SlicerLauncher is not affiliated with or endorsed by those vendors unless explicitly stated otherwise. See [TRADEMARKS.md](TRADEMARKS.md).

## Build from source

### Requirements

- Windows 10 or Windows 11 x64
- .NET 8 SDK

From PowerShell:

```powershell
cd .\SlicerLauncher
dotnet build -c Release -r win-x64
```

The compiled application is created under:

```text
SlicerLauncher\bin\Release\net8.0-windows\win-x64\
```

The official distributed binary is the Microsoft Store version.

## Fusion 360

The Microsoft Store package registers the execution alias:

```text
%LOCALAPPDATA%\Microsoft\WindowsApps\SlicerLauncher.exe
```

In Fusion 360:

1. Choose **Save as Mesh**.
2. Set **Preparation Type** to **Print Utility**.
3. Set **Application** to **Custom**.
4. Select the SlicerLauncher execution alias shown above.
5. Choose STL or 3MF and continue the export.

## Settings

Settings are stored in:

```text
%APPDATA%\SlicerLauncher\settings.json
```

The application can also migrate the older `config.xml` format if present.

Stored information can include configured Slicer names and executable paths, default/automatic-launch settings, countdown duration, and recent model-file paths.

## Privacy

SlicerLauncher works locally and contains no advertising, analytics, tracking, or telemetry. It does not upload model files or settings.

See [PRIVACY.md](PRIVACY.md).

## Security

Please report suspected security vulnerabilities privately before opening a public issue. See [SECURITY.md](SECURITY.md).

## Contributing

Contributions are welcome. See [CONTRIBUTING.md](CONTRIBUTING.md).

## License

SlicerLauncher source code is licensed under the **GNU General Public License v3.0**. See [LICENSE](LICENSE).

The GPL does not grant trademark rights or permission to represent unofficial builds as official 3dprintkings releases. See [TRADEMARKS.md](TRADEMARKS.md).

## Official project

Website: https://www.3dprintkings.ch
GitHub: https://github.com/3dprintkings/SlicerLauncher

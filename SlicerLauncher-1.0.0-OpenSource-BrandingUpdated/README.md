# SlicerLauncher

**One file. Any slicer.**

SlicerLauncher is a free Windows utility published by Nino King under the 3dprintkings brand for makers who use multiple 3D-printing slicers. Open an STL or 3MF file once, then choose the slicer you want to use. SlicerLauncher can also receive mesh exports from Fusion 360 and pass the file to a configured slicer.

## Status

This repository is being prepared for an open-source, Microsoft Store based distribution model.

- Version: **1.0.0**
- Framework: **.NET 8 / Windows Forms**
- Target platform: **Windows x64**
- License: **GNU General Public License v3.0**
- No advertising
- No analytics or telemetry
- Official Microsoft Store packaging is not part of this source snapshot yet

The current 1.0.0 source still contains the previous direct-distribution build scripts and Windows file-association registration so the known working application remains reproducible while the Store/MSIX migration is completed. The previous custom update check has already been removed; official binary updates are intended to be handled by the Microsoft Store.

## Features

- Open STL and 3MF files with a slicer of your choice
- Receive mesh files from Fusion 360
- Automatic detection of supported slicers
- Add custom slicers manually
- Reorder configured slicers
- Configurable default slicer
- Optional automatic launch with adjustable countdown
- Stop the countdown and choose another slicer
- Recent files list with up to 10 entries
- Per-user Windows file-association registration for STL and 3MF
- Settings stored in the current user's AppData profile

## Automatically detected slicers

The current version detects common installations of:

- Bambu Studio
- ELEGOO Slicer
- OrcaSlicer
- PrusaSlicer
- Creality Print

Other slicers can be added manually by selecting their executable.

Third-party product names are used only to describe compatibility. SlicerLauncher is not affiliated with or endorsed by those vendors unless explicitly stated otherwise. See [TRADEMARKS.md](TRADEMARKS.md).

## Privacy

SlicerLauncher does not contain advertising, analytics, tracking, or telemetry. It stores configuration, slicer executable paths, and recent model-file paths locally in the user's AppData profile.

SlicerLauncher does not perform an automatic update check and does not contact a 3dprintkings or GitHub update endpoint. Official binary updates are intended to be distributed through the Microsoft Store.

See [PRIVACY.md](PRIVACY.md) for details.

## Build from source

### Requirements

- Windows 10 or Windows 11 x64
- .NET 8 SDK

From PowerShell in the repository root:

```powershell
Set-ExecutionPolicy -Scope Process -ExecutionPolicy Bypass
.\Build-Portable.ps1
```

The current legacy build script creates a self-contained executable under:

```text
dist\Portable\SlicerLauncher.exe
```

The .NET runtime does not need to be installed separately on a machine running that self-contained build.

### Legacy installer build

The source snapshot also retains the previous Inno Setup based installer tooling during the Store migration.

Requirements:

- Inno Setup 6 installed manually

Run:

```powershell
.\Build-Installer.ps1
```

No build script downloads or installs Inno Setup automatically.

These installer/portable scripts are retained for reproducibility and local testing. The intended official binary distribution is the Microsoft Store after the MSIX migration is complete.

## Fusion 360

The current direct-build version can be selected as a custom Print Utility in Fusion 360 by pointing Fusion 360 to `SlicerLauncher.exe`.

The future Store version will use an MSIX-compatible execution alias. That packaging work is intentionally not included in this open-source preparation snapshot.

## Settings

Settings are stored in:

```text
%APPDATA%\SlicerLauncher\settings.json
```

The application can also migrate the older `config.xml` format if present.

Stored information can include:

- configured slicer names and executable paths
- default slicer selection
- automatic-launch preference and countdown
- recent model-file paths

## File associations

The current 1.0.0 source can register SlicerLauncher as an available Windows application for:

- `.stl`
- `.3mf`

Registration is performed per user under `HKEY_CURRENT_USER` and does not set SlicerLauncher as the Windows default automatically.

This mechanism is planned to move into the MSIX package manifest for the Microsoft Store version.

## Security

Please do not disclose suspected security vulnerabilities in a public issue before they have been reviewed. See [SECURITY.md](SECURITY.md).

## Contributing

Contributions are welcome. See [CONTRIBUTING.md](CONTRIBUTING.md).

## License

Copyright © 2026 Nino King · 3dprintkings.

SlicerLauncher source code is licensed under the **GNU General Public License v3.0**. See [LICENSE](LICENSE).

The GPL license does not grant rights to use 3dprintkings branding in a way that suggests an unofficial build or fork is an official 3dprintkings release. See [TRADEMARKS.md](TRADEMARKS.md).

## Official project

Website: https://www.3dprintkings.ch

# Contributing to SlicerLauncher

Thank you for considering a contribution to SlicerLauncher.

## Development principles

SlicerLauncher aims to remain:

- small and easy to understand
- useful without an account
- free of advertising
- free of analytics and telemetry
- conservative about file-system and system changes
- compatible with common 3D-printing Slicers without bundling them

Changes that add network communication, telemetry, advertising, background services, privileged operations, or new external dependencies should be clearly justified and documented.

## Build requirements

- Windows 10 or Windows 11 x64
- .NET 8 SDK

```powershell
cd .\SlicerLauncher
dotnet build -c Release -r win-x64
```

The official binary distribution is through the Microsoft Store.

## Code contributions

By submitting a contribution, you confirm that you have the right to submit it and agree that it will be distributed under the GNU General Public License v3.0.

For suspected security vulnerabilities, use [SECURITY.md](SECURITY.md) rather than a public issue.

## Third-party names and branding

Do not add third-party logos or other protected assets without confirming redistribution rights. Product names may be used where reasonably necessary to describe compatibility.

Do not use 3dprintkings branding in a way that suggests an unofficial fork is an official release. See [TRADEMARKS.md](TRADEMARKS.md).

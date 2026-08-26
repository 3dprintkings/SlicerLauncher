# Contributing to SlicerLauncher

Thank you for considering a contribution to SlicerLauncher.

## Before contributing

Please keep changes focused and explain the user problem they solve. Bug reports and compatibility reports for additional slicers are welcome.

For suspected security vulnerabilities, follow [SECURITY.md](SECURITY.md) instead of opening a public issue with exploit details.

## Development principles

SlicerLauncher aims to remain:

- small and easy to understand
- useful without an account
- free of advertising
- free of analytics and telemetry
- transparent about network communication
- conservative about registry and file-system changes
- compatible with common 3D-printing slicers without bundling them

Changes that add network communication, telemetry, advertising, background services, privileged operations, or new external dependencies should be clearly justified and documented.

## Code contributions

By submitting a contribution, you confirm that:

1. you have the right to submit the contribution;
2. the contribution does not knowingly include code, assets, or other material that you are not permitted to redistribute; and
3. you agree that your contribution will be distributed under the GNU General Public License v3.0, the same license as the project.

## Build requirements

The current source targets .NET 8 Windows Forms on Windows x64.

For the legacy local self-contained build:

```powershell
.\Build-Portable.ps1
```

The project is being migrated to MSIX for official Microsoft Store distribution. Packaging changes should preserve the ability to receive an STL/3MF path and pass it to a selected slicer.

## Style

Prefer straightforward C# and small, reviewable changes. Avoid adding a dependency when the required behaviour can reasonably be implemented with the .NET platform libraries already used by the project.

## Third-party names and branding

Do not add third-party logos or other protected assets without confirming that redistribution is permitted. Product names may be used where reasonably necessary to describe compatibility.

Do not use 3DPrintKings branding in a way that suggests an unofficial fork is an official release. See [TRADEMARKS.md](TRADEMARKS.md).

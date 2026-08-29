# Security Policy

## Reporting a vulnerability

If you believe you have found a security vulnerability in SlicerLauncher, please report it privately before opening a public issue.

Email: `hello@3dprintkings.ch`

Suggested subject: `SlicerLauncher security report`

Please include, where possible, the affected SlicerLauncher version, Windows version, a clear description, reproduction steps, potential impact, and safe proof-of-concept material.

## Supported versions

Security fixes are intended for the latest actively maintained release.

## Security design notes

SlicerLauncher 1.1.0:

- stores configuration locally in the user's AppData profile
- launches only Slicer executables configured or detected on the local system
- does not contain an automatic update checker
- does not contain advertising, analytics, tracking, or telemetry SDKs
- relies on the Microsoft Store / MSIX package for installation, updates, execution alias, and STL/3MF registration in the official release

The About window can open the official 3dprintkings website and the project's public GitHub repository when the user explicitly clicks those links.

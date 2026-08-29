# Open-Source Audit

Date: 29 August 2026
Scope: SlicerLauncher 1.1.0 source release.

## Result

No hard-coded passwords, API keys, authentication tokens, private keys, connection strings, or other obvious secrets are intentionally included in the release source.

The application project contains no external NuGet `PackageReference` entries and uses the .NET / Windows platform libraries plus project branding assets.

## Network behaviour

SlicerLauncher 1.1.0 contains no automatic update check or telemetry.

The About window can open:

- https://www.3dprintkings.ch
- https://github.com/3dprintkings/SlicerLauncher

These links are opened only when the user explicitly clicks them.

## Process launching

SlicerLauncher starts a configured local Slicer executable. If an STL or 3MF file was supplied, the file path can be passed to that executable.

## Local file behaviour

Settings are stored under:

`%APPDATA%\SlicerLauncher\settings.json`

A legacy `config.xml` can be read for migration. Recent file paths can be stored locally. Model-file contents are not intentionally copied or uploaded by SlicerLauncher.

## Microsoft Store distribution

The official release uses Microsoft Store / MSIX packaging for installation and update delivery. The package declares the execution alias and STL/3MF integration.

## Review limitations

This document is a source-level review. Final Microsoft Store package validation and certification are performed separately.

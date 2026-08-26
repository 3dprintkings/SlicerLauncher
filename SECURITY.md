# Security Policy

## Reporting a vulnerability

If you believe you have found a security vulnerability in SlicerLauncher, please report it privately before opening a public issue.

Email:

`hello@3dprintkings.ch`

Suggested subject:

`SlicerLauncher security report`

Please include, where possible:

- the affected SlicerLauncher version
- Windows version
- a clear description of the issue
- steps to reproduce it
- the potential security impact
- any proof-of-concept material that is safe to share

Please avoid including unrelated personal data or confidential information.

## Public disclosure

Please allow reasonable time for the issue to be reviewed and, where necessary, corrected before publishing technical details publicly.

## Supported versions

Security fixes are intended for the latest actively maintained release. Older source snapshots may remain available for transparency but may not receive security updates.

## Security design notes

The current 1.0.0 source:

- stores settings locally in the user's AppData profile
- launches only slicer executables configured or detected on the local system
- can register its own STL/3MF file associations under the current user's registry hive
- does not perform an automatic update check or contact an update endpoint
- contains no advertising, analytics, or telemetry SDKs

The planned Microsoft Store version will replace the remaining direct-distribution mechanisms with MSIX packaging. Official binary updates are intended to be Store-managed.

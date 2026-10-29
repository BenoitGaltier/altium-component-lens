Portable Windows x64 application for finding duplicate Altium 365 components using Manufacturer + Manufacturer Part Number.

- English, French and Spanish interface.
- Workspace API tokens kept in memory; read-only library access.
- Actual Altium component-category filters and cross-workspace / internal duplicates.
- Parameter and Part Choice evidence, missing-data reporting and explicit partial scans.
- Filtered CSV/JSON exports and an offline sample-data demo.

Download and extract the ZIP, then run **ComponentLens.exe**. No installation of .NET, Node.js or Altium Designer is required. Keep the console window open while using the app. Windows 10/11 x64 and a modern browser are required.

Automated tests and a portable HTTP smoke test run before release. Authenticated comparison against real Altium workspaces still needs validation with the intended API tokens. See `docs/VALIDATION.md` in the repository.

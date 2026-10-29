Fix for v1.0.0 aborting with "Inconsistent pagination" when retrieved rows do not match the API's advertised total.

- Follow `hasNextPage` / `endCursor` until the end of the connection; do not use `totalCount` as a hard limit.
- Deduplicate overlapping API rows by component ID before reading parameters and comparing identities.
- Keep all retrieved components and mark the report **PARTIAL** when counts differ, totals change or repeated components have conflicting values.
- Show inventory diagnostics (rows, distinct components, repeated rows, pages, totals) in FR/EN/ES and retain them in JSON exports.
- Keep blocking errors for missing/repeated cursors and pages without progress, with specific messages.
- Add regression tests for both 1,323/1,321 scenarios: overlapping rows and an under-reported total.

Download and extract the ZIP, then run **ComponentLens.exe**. No installation of .NET, Node.js or Altium Designer is required. Keep the console window open while using the app. Windows 10/11 x64 and a modern browser are required.

31 backend tests, 6 frontend tests and a portable HTTP smoke test run before release. The original failure's counters were observed in the user's existing session; an end-to-end scan with the corrected release still needs validation with the intended tokens. See `docs/VALIDATION.md`.

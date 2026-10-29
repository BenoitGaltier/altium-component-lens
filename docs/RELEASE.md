Save workspace connections and comparisons locally, show all duplicate groups on one page, and verify API counter discrepancies before declaring a partial report.

- URLs, tokens, language and the latest comparison are saved automatically, encrypted with Windows DPAPI for the current user. State lives under `%LOCALAPPDATA%\ComponentLens` and survives replacing the portable ZIP.
- Startup restores dated results and saved connections without contacting Altium. Tokens are never returned to the browser or included in exports. Local data can be cleared, including the encrypted backup.
- All matching duplicate groups appear on one page, without Next/Previous controls.
- An under-reported API counter (such as 1,323 distinct components against 1,321 advertised items) becomes informational only after a second independent complete traversal returns identical IDs and metadata. Real uncertainty and unread parameters remain partial.
- Exactly one PARTIAL banner is shown; reasons and verification outcomes remain in the diagnostics and JSON.
- Existing EN/FR/ES, category filters, Part Choice/parameter matching, CSV/JSON exports and read-only Altium access are preserved.

Download and extract the ZIP, then run **ComponentLens.exe**. No installation of .NET, Node.js or Altium Designer is required. Keep the console window open while using the app. Windows 10/11 x64 and a modern browser are required.

Upgrading from 1.0.x requires entering both tokens once because older releases did not persist them. Subsequent launches restore saved state automatically. Only one instance may use a data folder at a time.

Before publication: 46 backend tests, 6 frontend tests, 17 portable HTTP checks and 29 cross-process persistence checks. Browser regression checks cover 37 groups on one page, cached startup, saved-token reuse, search/details, FR/EN/ES and the single warning banner. API regression data is fictitious; the next real scan performs the new inventory verification. See `docs/VALIDATION.md`.

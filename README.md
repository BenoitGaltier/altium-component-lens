# Component Lens

A portable, read-only Windows app for finding duplicate components across two Altium 365 workspaces. Compare **Manufacturer + Manufacturer Part Number**, filter by actual Altium component categories, and use the interface in **English, French or Spanish**.

[Français](docs/README.fr.md) · [Español](docs/README.es.md)

![French interface displaying fictitious demo results](docs/screenshots/demo-fr.png)

## Run without installing anything

Download `ComponentLens-1.0.1-win-x64.zip` from this repository's Releases, extract it, then double-click **ComponentLens.exe**. The executable includes its .NET runtime and opens your default browser. Keep its console window open while using the app. Choose **Quit** or close the console to stop it.

Requires Windows 10/11 x64, a modern browser, network access to Altium 365, and an API access token with library/component read access for each workspace. No Altium Designer, Node.js or .NET installation is needed. The embedded runtime may unpack native files into the user's temporary directory on first launch. The release is unsigned.

1. Enter the URL and API access token for workspace A. Select **Connect**.
2. Repeat for workspace B. A pasted `Bearer …` prefix is accepted.
3. Select **Find duplicates**. The app reads all pages and each component's parameters.
4. Filter by category, search by manufacturer/MPN/component, and expand a result to inspect every occurrence and its evidence.
5. Export the filtered groups to CSV or JSON. Select the missing-data card to review and export data issues.

**Explore demo** works without Altium tokens and uses clearly marked fictitious data. It does not contact Altium.

## What counts as a duplicate

- A match requires the **same manufacturer and the same MPN**. Every manufacturer part choice is considered. The app also reads revision parameters and inherited item parameters; revision values take precedence for the same field.
- Recognized parameter names include `Manufacturer`, `Manufacturer Name`, `Mfr`, `Mfr Name`, `Manufacturer Part Number`, `MPN`, `Mfr Part Number`, and `Manufacturer PN`. Numbered pairs such as `Manufacturer 1` / `Manufacturer Part Number 1` are supported. Fields with different suffixes are never paired.
- Default normalization applies Unicode NFKC, ignores case and outer whitespace, and collapses repeated whitespace in manufacturer names. MPN punctuation and internal whitespace remain significant. Disable normalization for exact, case-sensitive raw-value matching. Manufacturer aliases, spelling variations, or electrically equivalent parts are not automatically merged.
- A component with several matching part choices can belong to several groups. A repeated identity on the same component counts once, with its evidence sources combined.
- Only the **latest revision** of each library component is read (`allComponentRevisions: false`). Older revisions of the same component are not counted as independent duplicates. No lifecycle-state filter is applied.
- **Across A and B** lists identities present in both workspaces. **Within A** and **Within B** show identities used by multiple components in that workspace, including identities also present in the other workspace. **All** is their union.

## Categories and report coverage

Categories come from `desLibrary.componentTypes` and each component's `componentType`, not keywords in descriptions. Full paths are used across workspaces; local category IDs may differ. Original category names remain unchanged in all interface languages.

The scan reads the entire visible library. Category and text filters apply afterwards. A group stays visible if **any occurrence** belongs to a selected category (including descendants). This preserves duplicates that have been categorized differently in A and B. Use the two category entries if equivalent categories have different paths/names across workspaces. Summary cards describe the full scan; the table count and exports reflect the active filters.

Components without a usable manufacturer/MPN pair are explicitly listed as data issues and excluded from comparison. Empty values and placeholders such as `N/A`, `Unknown` or `-` are not identities. A parameter-read failure marks the entire report **PARTIAL**; known Part Choices are retained but matches may be missing. Network, authentication or broken-cursor failures stop the scan rather than produce an empty success report. Rate limiting and transient HTTP 502/503/504 errors are retried up to four times, with cancellation support.

Pagination follows `hasNextPage` / `endCursor` until the API ends the connection, rather than treating `totalCount` as a hard limit. Overlapping rows are deduplicated by component ID before reading parameters and comparing components. If the distinct count matches a stable API total, harmless repeated rows are reported without inventing duplicates. A different/changing total or conflicting repeated-component data produces a **PARTIAL** report with all retrieved components. The **Inventory diagnostics** panel shows rows, distinct components, repeated rows, pages and advertised totals; JSON exports retain this evidence. Missing/repeated cursors or intermediate pages that add no components still stop the scan with a specific error.

Results cover only data visible to each token. The API does not provide an atomic snapshot across both workspaces: avoid library edits during a scan. There is no deletion, merging or modification of Altium data.

## Credentials and local operation

Tokens stay in process memory for the current session. A connected token is removed from the form and is never put in browser storage, reports, URLs or application logs. Only display names, workspace URLs and the language preference persist in browser local storage; the local session key uses session storage. Close the executable to discard its credentials.

Refreshing the browser restores the connected workspace metadata, completed results or the running scan from that same local process; tokens are never returned to the browser.

The backend listens only on `127.0.0.1` on a random port. API requests require a random per-process session key. A host/origin check and a Content Security Policy protect the local interface. HTTPS Altium hosts ending in `.altium.com` or `.altium365.com` are supported; redirects are not followed when sending API tokens. TLS uses the operating system's certificate validation. No telemetry or external UI assets are loaded. Optional API documentation links open the official Altium site.

CSV files use UTF-8 with BOM, quote fields and neutralize spreadsheet-formula prefixes. JSON exports include the selected filters, full-scan summary and issues, completeness, timestamp, normalization mode, and filtered groups. CSV rows carry the scan timestamp, completeness, normalization mode and demo flag.

## Development and validation

Development requires the .NET 10 SDK and Node.js 22 or later for frontend tests. There are no third-party application dependencies. `NuGet.Config` uses the public NuGet feed for Microsoft's self-contained runtime packs.

```powershell
./scripts/test.ps1
dotnet run --project src/ComponentLens
./scripts/publish.ps1
```

The portable executable and ZIP are written to `dist/win-x64/` and `dist/`. The adjacent `.sha256` file validates the archive. CLI options: `--headless` suppresses browser launch, `--port 4177` chooses a fixed loopback port, `--help` shows usage. The console displays a complete session URL; use it when reopening a tab or testing with another browser.

Tests exercise identity grouping, normalization, indexed fields, evidence deduplication, category filters, complete cursor pagination, authentication/GraphQL errors, partial detail reads, throttling, cancellation, CSV escaping and translation coverage. `scripts/smoke.ps1` additionally launches the built portable executable and checks its real HTTP interface. See [validation notes](docs/VALIDATION.md) for what has and has not been exercised.

GitHub Actions runs tests on pushes/PRs. A `v*` tag tests and builds the portable package, then creates a GitHub release with the ZIP and SHA-256 checksum. The workflow needs the repository's Actions feature enabled.

## API references

- [Altium 365 API: endpoints and Bearer authentication](https://www.altium.com/documentation/altium-developer-center/altium-365/api)
- [Library Management API reference](https://docs.developer.altium.com/platform-api-docs/reference/library-management/overview/)
- [Official library schema](https://docs.developer.altium.com/platform-api-docs/reference/library-management/schema.graphql)
- [Cursor pagination](https://docs.developer.altium.com/platform-api-docs/guides/pagination/)

The implementation uses only GraphQL `query` operations: `desLibrary`, `componentTypes`, `components` and `desComponentById`. The schema was checked on 29 October 2026. The app is an independent tool and is not an Altium product.

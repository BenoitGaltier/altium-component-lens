# Validation and remaining checks

Validation date: 29 October 2026. Results below distinguish automated software checks from authenticated Altium use.

## Automated checks

- Backend: 31 executable tests, including duplicate grouping, both identity sources, indexed parameters, exact/normalized matching, Unicode, missing data, category/component pagination, overlapping rows, partial reports for inconsistent/changing totals and changed repeated components, missing/repeated cursors, pagination without progress, mismatched detail IDs, GraphQL errors, HTTP authentication/token expiry, redirects, throttling, partial detail reads, cancellation and session/result restoration without token disclosure.
- Regression cases cover both explanations of 1,323 rows against an advertised total of 1,321: 1,321 distinct components plus 2 repeated rows, and 1,323 distinct components with an under-reported total. The first is deduplicated; the second remains available in an explicitly partial report. Neither case truncates the inventory to match the advertised total.
- Frontend: 6 Node.js tests covering category-path matching, differently categorized duplicates, scopes/search, CSV quoting/formula protection, filtered export metadata and FR/EN/ES string/placeholder parity.
- Portable HTTP smoke test: 17 assertions pass against the released self-contained executable, launched with an invalid external `DOTNET_ROOT`, checking embedded assets, report output, session metadata, local-session authentication, host/origin validation, invalid inputs and clean shutdown.
- Browser visual/interaction checks: FR/EN/ES switching, demo comparison (4 cross-workspace groups), Resistors category filtering (1 group), expanded component occurrences and Part Choice/parameter evidence were exercised in the in-app browser on the development Windows host.

The release workflow repeats software tests and the portable smoke test before attaching a binary.

## Authenticated Altium validation still required

The GraphQL operations were built against the official Altium Library Management schema available on the validation date. During investigation of the first real v1.0.0 use, the existing browser UI retained a failed scan's progress of 1,323 rows against an API total of 1,321 for one workspace; the other had reached parameter reading at 1,866/1,866. The v1.0.0 error did not preserve distinct-ID counts, so overlapping rows versus an inaccurate API total could not be distinguished from that evidence. Existing workspace credentials were not extracted or copied. This observation motivated the v1.0.1 regression cases and explicit inventory diagnostics.

This is not evidence of a successful real-workspace scan with v1.0.1. End-to-end validation of the corrected release with the intended tokens, large real libraries, proxy/VPN behavior and real category naming remains pending. The demo contains fictitious components and is explicitly labeled in the UI and exports. Automated API tests use controlled mock HTTP responses.

The published binary has been tested on the development Windows host with external runtime lookup disabled. A separate clean Windows 10/11 VM and Windows ARM64 have not been qualified. Only Windows x64 is shipped. No signed installer or code-signing certificate is included.

## Coverage boundaries

Only the latest component revisions visible to the API tokens are compared. Missing or placeholder identities are reported rather than matched. A parameter read failure, inconsistent/changing inventory total or conflicting repeated-component data results in a partial report; network, cursor or authentication failures stop the scan. Reports cannot establish completeness beyond the tokens' visibility, and the two libraries are not an atomic snapshot.

Category filters preserve a group when any occurrence belongs to a selected category. They do not limit API reading. Alias matching, electrical equivalence, footprint/symbol comparison, Altium data mutation and duplicate deletion/merging are outside this version's functionality.

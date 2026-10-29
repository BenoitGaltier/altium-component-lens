# Validation and remaining checks

Validation date: 29 October 2026. Results below distinguish automated software checks from authenticated Altium use.

## Automated checks

- Backend: 26 executable tests, including duplicate grouping, both identity sources, indexed parameters, exact/normalized matching, Unicode, missing data, category pagination, component pagination, repeated cursors, truncated/changing inventories, mismatched detail IDs, GraphQL errors, HTTP authentication/token expiry, redirects, throttling, partial detail reads, cancellation and session/result restoration without token disclosure.
- Frontend: 6 Node.js tests covering category-path matching, differently categorized duplicates, scopes/search, CSV quoting/formula protection, filtered export metadata and FR/EN/ES string/placeholder parity.
- Portable HTTP smoke test: 17 assertions pass against the released self-contained executable, launched with an invalid external `DOTNET_ROOT`, checking embedded assets, report output, session metadata, local-session authentication, host/origin validation, invalid inputs and clean shutdown.
- Browser visual/interaction checks: FR/EN/ES switching, demo comparison (4 cross-workspace groups), Resistors category filtering (1 group), expanded component occurrences and Part Choice/parameter evidence were exercised in the in-app browser on the development Windows host.

The release workflow repeats software tests and the portable smoke test before attaching a binary.

## Authenticated Altium validation still required

The GraphQL operations were built against the official Altium Library Management schema available on the validation date. No real workspace token has been supplied to this new application during development. Production API access, the intended token permissions, large real libraries, proxy/VPN behavior, token expiry during a long scan and real category naming must still be exercised with the intended workspaces.

This is not evidence of a successful real-workspace scan. The app's demo contains fictitious components and is explicitly labeled in the UI and exports. Automated API tests use controlled mock HTTP responses.

The published binary has been tested on the development Windows host with external runtime lookup disabled. A separate clean Windows 10/11 VM and Windows ARM64 have not been qualified. Only Windows x64 is shipped. No signed installer or code-signing certificate is included.

## Coverage boundaries

Only the latest component revisions visible to the API tokens are compared. Missing or placeholder identities are reported rather than matched. A parameter read failure results in a partial report; an inventory or authentication failure stops the scan. Reports cannot establish completeness beyond the tokens' visibility, and the two libraries are not an atomic snapshot.

Category filters preserve a group when any occurrence belongs to a selected category. They do not limit API reading. Alias matching, electrical equivalence, footprint/symbol comparison, Altium data mutation and duplicate deletion/merging are outside this version's functionality.

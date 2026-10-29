# Validation and remaining checks

Validation date: 29 October 2026. Results below distinguish automated software checks from authenticated Altium use.

## Automated checks

- Backend: 46 executable tests, including the previous identity, pagination, auth, error, cancellation and session tests, plus real Windows DPAPI round-trips/tampering, atomic save failure, single-instance locking, cache restoration, token reuse/rotation/expiry, workspace-change invalidation, partial-report preservation, forget, corrupt storage and inventory verification failure/mismatch cases.
- Regressions cover 1,323 rows containing 1,321 distinct components plus 2 repetitions, and 1,323 distinct components against an advertised total of 1,321. Overlaps are deduplicated. A stable undercount is informational only after two independent complete traversals return identical IDs and metadata with the same counter. Changed IDs, metadata, counters, missing components, failed verification and unread parameters preserve uncertainty. Neither case truncates the inventory to match the counter.
- Frontend: 6 Node.js tests covering category-path matching, differently categorized duplicates, scopes/search, CSV quoting/formula protection, filtered export metadata and FR/EN/ES string/placeholder parity.
- Portable HTTP smoke test: 17 assertions pass against the released self-contained executable, launched with an invalid external `DOTNET_ROOT`, checking embedded assets, report output, session metadata, local-session authentication, host/origin validation, invalid inputs and clean shutdown.
- Cross-process persistence smoke test: 29 assertions pass. An actual HTTP fixture host scans 1,323/1,866 fictitious components into 37 duplicate groups, saves encrypted data, restarts with zero mock API requests, reuses a saved token and restores the result in the published self-contained executable. Forget removes both encrypted files and a subsequent restart remains empty. Scripts use isolated `.artifacts/` folders.
- Browser checks: all 37 groups are present in one page without Next/Previous controls; search, internal-duplicate tabs and expanded occurrences work. Restart restores connections and the dated comparison with no API calls. Empty token fields reuse saved credentials; connecting one workspace preserves unfinished edits in the other. A deliberately changing inventory displays exactly one PARTIAL banner, with the transient notification hidden. FR/EN/ES messages were checked. The screenshot in `screenshots/local-persistence-fr.png` uses fictitious TEST workspaces.

The release workflow repeats the 52 software tests, 17 portable checks and 29 persistence checks before attaching the binary. Failed checks block publication.

## Authenticated Altium validation still required

The GraphQL operations were built against the official Altium Library Management schema available on the validation date. During investigation of the first real v1.0.0 use, the existing browser UI retained a failed scan's progress of 1,323 rows against an API total of 1,321 for one workspace; the other had reached parameter reading at 1,866/1,866. The v1.0.0 error did not preserve distinct-ID counts, so overlapping rows versus an inaccurate API total could not be distinguished from that evidence. Existing workspace credentials were not extracted or copied. This observation motivated the v1.0.1 regression cases and explicit inventory diagnostics.

The user's v1.0.1 screenshot confirmed 1,323 distinct components (not repeated rows) against 1,321 advertised items, and showed a duplicated PARTIAL banner. The v1.1.0 verification and single-banner changes address those observations. This is not evidence that the two real inventories have already passed the new independent verification. That confirmation happens on the next scan with the intended tokens. Automated API tests and browser regression checks use fictitious data; current real-workspace credentials were not extracted from the running older application.

The published binary has been tested on the development Windows host with external runtime lookup disabled. A separate clean Windows 10/11 VM and Windows ARM64 have not been qualified. Only Windows x64 is shipped. No signed installer or code-signing certificate is included.

## Coverage boundaries

Only the latest component revisions visible to the API tokens are compared. Missing identities are reported rather than matched. A counter undercount can be independently verified as described above; other unresolved discrepancies or detail-read failures remain partial. Network/cursor/authentication failures stop a fresh scan and preserve the last dated comparison. Even two identical traversals do not establish visibility beyond the token's rights or an atomic snapshot across workspaces.

Saved data is encrypted for the current Windows user with DPAPI. Cross-process restoration and ciphertext tampering are tested; recovery after a Windows-account/password reset and migration to another computer are not qualified. Corrupt/undecryptable state is preserved and reported. A saved comparison is historical data, and restoration never silently re-scans or revalidates token expiry. The first launch after a 1.0.x upgrade requires reconnecting once because older versions did not persist credentials.

Category filters preserve a group when any occurrence belongs to a selected category. They do not limit API reading. Alias matching, electrical equivalence, footprint/symbol comparison, Altium data mutation and duplicate deletion/merging are outside this version's functionality.

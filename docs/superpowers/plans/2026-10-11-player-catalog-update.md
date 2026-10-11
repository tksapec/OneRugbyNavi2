# Player Catalog Update Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Let users explicitly refresh and persist the latest player catalog from the repository's current GitHub `main` branch, even when the data JSON filename changes.

**Architecture:** A fixed packaged and remotely hosted manifest names the active catalog by repository-relative path. A testable update service downloads only allowlisted raw GitHub paths, validates the manifest and player document, then atomically persists valid JSON; the player page loads cached JSON before its bundled fallback and exposes a separate update control.

**Tech Stack:** .NET 9, C#, MAUI, `HttpClient`, `System.Text.Json`, xUnit.

**Spec:** `docs/superpowers/specs/2026-10-11-player-catalog-update-design.md`

## Global Constraints

- Work on `main` only and preserve all pre-existing uncommitted work.
- Do not add or trigger GitHub Actions.
- Network refresh is user initiated; keep bundled data available offline.
- Accept only HTTPS content from `tksapec/OneRugbyNavi2` under `Resources/Raw/*.json`.
- If the remote manifest returns 404, use the validated packaged manifest as a compatibility pointer; other errors do not fall back.
- Validate before replacing the last good catalog; no failed request may erase it.
- Keep the existing home-screen schedule refresh behavior unchanged.

## Review Focus

- Manifest paths with traversal, URL authority, query, fragment, or wrong extension: reject before requesting catalog content; test each class.
- Oversized or malformed manifest/catalog responses: fail safely and retain cache; test response-size and parse boundaries.
- Empty but otherwise valid catalog document: reject it and preserve cache; test zero players.
- Interrupted/failed cache replacement: preserve old cache; test the failure path around temporary file replacement.
- A downloaded catalog can invalidate a selected team filter: reset only invalid filter choices and preserve search and still-valid choices; cover by a focused page behavior check or manual UI verification if MAUI controls cannot be exercised in Core tests.

## File Map

- Create `PlayerCatalogUpdateModels.cs`: manifest DTO and update result type.
- Create `PlayerCatalogCache.cs`: testable cache read and atomic-write helpers.
- Create `PlayerCatalogUpdateService.cs`: manifest/catalog HTTP workflow and update result.
- Modify `PlayerCatalogLoader.cs`: use valid app cache first and bundled JSON fallback second.
- Modify `PlayersPage.cs`: explicit update button, progress and result/error status, and refreshed filters.
- Create `Resources/Raw/player-catalog-manifest.json`: stable package/repository pointer to current JSON.
- Modify `tests/OneRugbyNavi2.Core.Tests/OneRugbyNavi2.Core.Tests.csproj`: link new pure Core files and copy the manifest fixture.
- Create `tests/OneRugbyNavi2.Core.Tests/PlayerCatalogUpdateTests.cs`: manifest, HTTP, validation, cache and failure-retention tests.
- Modify `docs/player-catalog.md`: document the manifest update/publishing contract and validation rules.

## Task 1: Manifest contract and parser

**Files:** create `PlayerCatalogUpdateModels.cs`; create `Resources/Raw/player-catalog-manifest.json`; modify Core test project; create `PlayerCatalogUpdateTests.cs`.

**Interfaces:** `PlayerCatalogManifest.TryParse(string json, out PlayerCatalogManifest manifest, out string error)`; manifest fields `FormatVersion` and `CatalogPath`. Version 1 path must be a normalized repository path below `Resources/Raw/`, ending in `.json`, with no URL components, backslashes, empty segments, `.` or `..` segments. Manifest JSON starts with version `1` and `Resources/Raw/league-one-players-2026-27.json`.

- [ ] Write tests for valid manifest, unsupported version, malformed JSON, absolute/external path, traversal, query/fragment, backslash, and wrong directory/extension.
- [ ] Run `dotnet test tests/OneRugbyNavi2.Core.Tests/OneRugbyNavi2.Core.Tests.csproj --no-restore --filter FullyQualifiedName~PlayerCatalogManifest -v:minimal`; confirm expected failures because the parser is missing.
- [ ] Implement manifest parser and DTO with `System.Text.Json` web defaults; reject all invalid paths before URL construction.
- [ ] Re-run the filtered test command; confirm all manifest cases pass.

## Task 2: Safe cache and update service

**Files:** create `PlayerCatalogCache.cs`; create `PlayerCatalogUpdateService.cs`; link both in the Core test project; extend `PlayerCatalogUpdateTests.cs`.

**Interfaces:** `PlayerCatalogCache.TryLoadAsync(string cachePath, CancellationToken)` returns a parsed document or null for absent/invalid cache. `PlayerCatalogCache.SaveAtomicallyAsync(string cachePath, string json, CancellationToken)` writes a sibling temporary file then replaces the target. `PlayerCatalogUpdateService(HttpClient httpClient, Uri manifestUri, string cachePath)` exposes `UpdateAsync(CancellationToken)` returning `PlayerCatalogUpdateResult(Catalog, CheckedAtUtc, Changed)`.

- [ ] Write tests using a local fake `HttpMessageHandler` for manifest then data retrieval, path-derived raw URL, changed content and unchanged content.
- [ ] Write tests for HTTP status/timeout, first-publish manifest 404 fallback, malformed/unsupported/empty catalog, oversized responses, and existing-cache preservation after every failure.
- [ ] Run the targeted test filter and confirm expected failures before implementation.
- [ ] Implement bounded HTTP retrieval, fixed raw GitHub manifest URL, safe same-repository catalog URL construction, parser validation, content comparison, and atomic persistence.
- [ ] Run targeted tests and the complete Core suite; confirm all pass.

## Task 3: Cached-first load and player search update UI

**Files:** modify `PlayerCatalogLoader.cs`, `PlayersPage.cs`, and `PlayerCatalogUpdateTests.cs` (plus test project links only if new pure helpers are required).

**Interfaces:** `PlayerCatalogLoader.LoadAsync(CancellationToken)` loads and validates the app-private cache first, then the packaged asset; `PlayersPage` owns a `PlayerCatalogUpdateService` configured with `FileSystem.AppDataDirectory` and the fixed raw manifest URI.

- [ ] Add a focused test for cached-catalog preference and invalid/missing-cache fallback using an extracted pure selection helper if needed; run it and confirm failure.
- [ ] Implement cache-first loader behavior without changing package fallback.
- [ ] Add `選手データを更新` on the player search page, separate from the schedule update control; disable during requests and show a busy indicator/status.
- [ ] On success replace the displayed collection, preserve search text and valid filters, clear only selections absent from the new data, and report season/count/check time or unchanged status.
- [ ] On failure retain displayed data and present an actionable Japanese error; verify the update button becomes usable again.
- [ ] Run the page-related Core tests and `git diff --check`.

## Task 4: Data maintainer documentation and full verification

**Files:** modify `docs/player-catalog.md` and any implementation files adjusted by review.

- [ ] Document that catalog filename changes require updating `Resources/Raw/player-catalog-manifest.json` on `main`; describe required fields, path restrictions, and validation expectations.
- [ ] Run `dotnet test tests/OneRugbyNavi2.Core.Tests/OneRugbyNavi2.Core.Tests.csproj --no-restore -v:minimal`.
- [ ] Build Android Release with the configured SDK path: `dotnet publish -f net9.0-android -c Release /p:AndroidSdkDirectory="C:\Users\e1399\AppData\Local\Android\Sdk"`.
- [ ] Run `git diff --check`, inspect the complete diff, and review changed files for regressions, unsafe URL handling, cache-loss paths, and unrelated edits.
- [ ] If the USB device is available, install the produced APK and launch the app; report build/install/launch separately from manual update-button interaction and live GitHub refresh.

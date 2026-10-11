# Player Catalog Update Design

## Goal

Let users refresh the player catalog from the current OneRugbyNavi2 GitHub repository without requiring an app release whenever the catalog data changes. The catalog filename may change in the future, so the app must discover the current JSON path from a stable manifest.

## User experience

- Add a `選手データを更新` action to the player search page. Keep the home page's existing `更新` action for schedule refresh.
- On page entry, load the last successfully downloaded catalog when available; otherwise load the bundled catalog.
- When the user taps the action, show a busy state and fetch a fixed manifest from the repository's `main` branch over HTTPS.
- The manifest identifies the current catalog path and schema version. The app downloads that catalog, validates the manifest, validates the catalog with `PlayerCatalogParser`, and only then replaces its app-private cached copy and visible player collection.
- Show the catalog season/count and last successful refresh time. Report unchanged data as a successful check. On network or validation errors, keep the currently displayed and cached catalog and show a readable failure message.

## Repository delivery contract

- Add a stable manifest at `Resources/Raw/player-catalog-manifest.json` and include it in the app package as a bootstrap/fallback resource.
- The manifest contains a format version and a repository-relative path to the active catalog JSON. Updating the manifest and JSON on `main` is the publishing operation; the JSON filename can then change without an app update.
- The client accepts only HTTPS raw content from the configured `tksapec/OneRugbyNavi2` repository and only repository paths under `Resources/Raw/` that end in `.json`. It does not follow arbitrary URLs supplied by the manifest.
- If the remote manifest returns HTTP 404 because it has not yet been published, use the validated manifest packaged with the app as a compatibility pointer. Other HTTP failures and invalid remote manifests fail without changing the cache. This fallback cannot discover a renamed remote JSON until the manifest is published.
- The initial manifest points to `Resources/Raw/league-one-players-2026-27.json`.

## Components

- `PlayerCatalogUpdateService`: owns HTTP retrieval, manifest parsing, path validation, catalog parsing, content comparison, and safe cache replacement. Dependencies (HTTP client and storage paths) must be injectable for deterministic tests.
- `PlayerCatalogLoader`: loads a validated persistent app-private catalog first, then the packaged catalog fallback.
- `PlayersPage`: exposes the explicit update action and busy/status state; after successful update, refreshes filters/results while preserving valid search and filter selections.
- `Resources/Raw/player-catalog-manifest.json`: stable repository entry point and bundled bootstrap metadata.

## Data safety and error handling

- Download to memory or a temporary file and validate fully before replacing the cache. Use an atomic move/replace where available so interruption cannot corrupt the last good catalog.
- A failed manifest or catalog fetch must never erase or overwrite the last valid cached catalog.
- If no cached catalog exists, the packaged JSON remains available offline.
- Bound HTTP timeouts and response sizes; reject malformed JSON, unsupported manifest versions, invalid paths, unsupported catalog schemas, and empty catalogs.
- A successful response with identical catalog content updates the last-check timestamp but need not rewrite the cache.

## Verification

- Unit tests cover manifest validation, repository/path restrictions, successful refresh, changed and unchanged content, invalid catalog, HTTP failure, and preserving the prior cache after failure.
- Page-level checks cover loading the cached catalog before the package fallback, update status/busy behavior, and retaining valid search/filter choices after refresh.
- Run the Core test suite and Android Release build. Install and launch on the connected USB device if it remains available; UI taps and live update from GitHub are reported separately from automated evidence.

## Out of scope

- No GitHub Actions or automated publishing workflow.
- No GitHub API authentication, schedule database updates, portrait updates, or general-purpose remote URL support.
- No automatic network download on startup; the user initiates refresh explicitly.

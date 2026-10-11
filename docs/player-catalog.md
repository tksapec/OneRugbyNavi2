# Player catalog data policy

The bundled catalog is `Resources/Raw/league-one-players-2026-27.json`. Each player should have a stable `playerId`, current club and division, positions, and at least one dated source URL. Record supported club, university, and high-school affiliations in `teamHistory` with a `type` value of `club`, `university`, or `highSchool`. Older rows without `type` are treated as clubs. Keep the concise education summary in `schools` for compatibility. Use separate sources for roster facts and historical transfers where possible; do not infer a transfer date from a season roster alone. When only an education institution is known, leave its seasons blank.

RugbyDB player profiles and annual transfer lists (`/list/transfer26.html` through `/list/transfer21.html`, including the Div.2 and Div.3 pages) are discovery sources for club histories. Check the season-level rows because a profile header can differ from a past season. Treat each transfer list as evidence for its named season only. When a list gives a previous club but not the start of that stint, leave `fromSeason` blank and record the last supported season in `toSeason`. Use official team or League One announcements to corroborate a move and tenure boundaries. Record the supporting URL, publisher, checked date, and fields on the player. Do not infer season ranges from missing seasons or map older Top League calendar-year headings without corroborating evidence.

When a roster source introduces a second record for a player already present from another source, verify identity against the team, profile URL, name, and available player facts. Add only reviewed pairs to `tools/deduplicate_player_catalog.py`; do not merge on fuzzy name similarity alone. The tool combines positions, aliases, history, and sources into the retained player record.

After editing or enriching team history, deduplicate reviewed player records and normalize/sort the catalog before using it:

```sh
python tools/deduplicate_player_catalog.py --apply
python tools/normalize_player_history.py --apply
```

The normalizer merges duplicate and overlapping/adjacent records for the same named institution, retains separate non-adjacent stints, removes duplicate undated rows, and orders high school, university, then clubs. RugbyDB enrichment applies this normalization before saving automatically. Run without `--apply` to preview the number of affected players and rows.

Only include facts supported by source URLs. Store the checked date and list the supported fields in each source's `checkedOn` and `fields`.

`nationalities` records explicitly sourced nationality statements. Do not infer nationality from birthplace, name, representative history, club, or League One registration category. Wikipedia and independent databases may be used as secondary sources when the article directly supports the stated nationality; record the exact page and publisher.

`registrationCategories` is a list of `{ "season": "YYYY-YY", "category": "A|A-1|A-2|B|C" }`. Categories are season-specific and depend on league eligibility rules, not nationality. Preserve the season shown by the source; never carry an earlier category into the current season. Current-season categories require a current-season source.

Before merging suspected duplicate people, compare official IDs, names and aliases, position, schools, birth date, physical measurements, club announcements, and reliable independent records. The duplicate detector reports candidates for review; it does not decide identity. Keep the correct person as one record and retain proven alternate spellings as aliases with their source. Never infer a prior club stint or its duration from a current roster listing alone.

Player portraits are outside the catalog schema and must not be added to this database.

## Publishing catalog updates

The app reads `Resources/Raw/player-catalog-manifest.json` from the repository's `main` branch when the user chooses `選手データを更新`. Keep this manifest at the same path. It currently uses format version `1` and a `catalogPath` such as `Resources/Raw/league-one-players-2026-27.json`. The path must be a repository-relative `.json` file under `Resources/Raw/`; do not use a URL, query, fragment, encoded path, or `.`/`..` segments.

To rename the catalog JSON, commit the renamed file and update `catalogPath` in the manifest in the same `main` branch update. The app downloads only over HTTPS from this repository's raw GitHub host, validates the manifest and full player catalog, rejects an empty catalog, and saves a valid response to its private app cache. A failed request leaves the last valid catalog in place. The bundled manifest and catalog remain the first-install and offline fallback.

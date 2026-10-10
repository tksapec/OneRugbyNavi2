# Player catalog data policy

The bundled catalog is `Resources/Raw/league-one-players-2026-27.json`. Each player should have a stable `playerId`, current club and division, positions, and at least one dated source URL. Record each known club stint in `teamHistory`, including the current stint where its start season is known. Use separate sources for roster facts and historical transfers where possible; do not infer a transfer date from a season roster alone.

Only include facts supported by source URLs. Store the checked date and list the supported fields in each source's `checkedOn` and `fields`.

`nationalities` records explicitly sourced nationality statements. Do not infer nationality from birthplace, name, representative history, club, or League One registration category. Wikipedia and independent databases may be used as secondary sources when the article directly supports the stated nationality; record the exact page and publisher.

`registrationCategories` is a list of `{ "season": "YYYY-YY", "category": "A|A-1|A-2|B|C" }`. Categories are season-specific and depend on league eligibility rules, not nationality. Preserve the season shown by the source; never carry an earlier category into the current season. Current-season categories require a current-season source.

Before merging suspected duplicate people, compare official IDs, names and aliases, position, schools, birth date, physical measurements, club announcements, and reliable independent records. The duplicate detector reports candidates for review; it does not decide identity. Keep the correct person as one record and retain proven alternate spellings as aliases with their source. Never infer a prior club stint or its duration from a current roster listing alone.

Player portraits are outside the catalog schema and must not be added to this database.

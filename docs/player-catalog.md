# Player catalog and portrait workflow

The bundled catalog is `Resources/Raw/league-one-players-2026-27.json`. Each player should have a stable `playerId`, current club and division, positions, and at least one dated source URL. Record each known club stint in `teamHistory`, including the current stint where its start season is known. Use separate sources for roster facts and historical transfers where possible; do not infer a transfer date from a season roster alone.

Portraits are optional. Add a crop manifest row with `player_id,input,crop_x,crop_y,crop_width,crop_height` and normalized coordinates (0–1) around the head and upper torso, then run:

```sh
python tools/normalize_player_portraits.py portraits.csv --input-dir source-photos
```

The tool writes 480×640 JPEGs into `Resources/Raw/player-portraits/`. Add a catalog portrait object with the relative path (for example, `player-portraits/player-001.jpg`) and the same normalized crop coordinates. Portrait files are git-ignored by default so personal local photos are not pushed to the public repository. The app supports optional photos and displays an empty placeholder when absent.

Only include facts supported by source URLs. Store the checked date and list the supported fields in each source's `checkedOn` and `fields`.

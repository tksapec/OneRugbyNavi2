# 2026-27 Div1 Player Catalog Audit

Date: 2026-11-15\
Catalog: `Resources/Raw/league-one-players-2026-27.json`

## Scope and result

Audited all 12 current Division 1 club rosters and official player profile pages against the bundled 2026-27 catalog, one club at a time. The catalog now contains 660 Div1 records, matching the combined official roster counts below. All 660 records have non-empty position values. Official roster and individual profile sources were refreshed with a check date of 2026-11-15 where reviewed.

| Team | Official roster | Catalog count |
|---|---|---:|
| 浦安D-Rocks | https://urayasu-d-rocks.com/team/player/ | 57 |
| クボタスピアーズ船橋・東京ベイ | https://www.kubota-spears.com/player/ | 63 |
| コベルコ神戸スティーラーズ | https://www.kobesteelers.com/member/ | 50 |
| 埼玉パナソニックワイルドナイツ | https://panasonic.co.jp/sports/wildknights/team/player/ | 56 |
| 静岡ブルーレヴズ | https://www.shizuoka-bluerevs.com/member | 56 |
| 東京サントリーサンゴリアス | https://www.suntory.co.jp/culture-sports/sungoliath/member/ | 50 |
| 東芝ブレイブルーパス武蔵 | https://www.bravelupus.com/member/?tab=player | 54 |
| 栃木ホンダヒート | https://www.honda-heat.jp/member/ | 56 |
| トヨタヴェルブリッツ | https://verblitz.toyotatimes-sports.toyota/player/ | 59 |
| 三菱重工相模原ダイナボアーズ | https://www.mhi.com/jp/company/sports/dynaboars/players-staff | 54 |
| 横浜キヤノンイーグルス | https://www.canon-eagles.jp/member/ | 51 |
| リコーブラックラムズ東京 | https://blackrams-tokyo.com/members/ | 54 |
| **Total** |  | **660** |

## Material updates

- **Toshiba, Tochigi, Toyota, Shizuoka:** removed current-team assignments for confirmed 2025-26 leavers; moved 田中元珠 to 栃木ホンダヒート; added current 2026-27 entries supported by official club announcements. Removed outdated records for ヴェア・タモエフォラウ, クワッガ・スミス, ピーター=ステフ・デュトイ, and チャーリー・ティトコム after official departure announcements.
- **Tochigi / Toyota:** added テビタ・ロフランド and 土永 雷; refreshed current profiles and alias spellings for the team rosters.
- **Sagamihara:** refreshed all 54 official player profiles, including 2026 arrivals. Corrected Jack Bowen's Japanese spelling to the club's official form and removed Charlie Titicomb, whose loan return is confirmed by League One.
- **Yokohama Canon:** refreshed all 51 player profiles; added Nathaniel Pole and removed Kurt Eklund after the club said he could not join.
- **BlackRams:** reconciled 54 listed player profiles, added Jeandre Labuschagne, corrected Tamati Tua's official name, and merged the duplicate 高橋敏也 record after confirming the same official profile. Kept 山本 秀 distinct from 山本 嶺二郎 and 山村 知也 distinct from 山村 勝悟 using their separate player IDs and profiles.
- **Panasonic / Toyota:** normalized 52 Panasonic position labels to the catalog's position codes and filled 27 empty Toyota positions from the club's official position-group roster.
- Other audited teams had roster/profile facts and provenance refreshed where direct official data supported an update. No eligibility category was inferred or carried forward across seasons.

## Unresolved primary-source differences

- **Jeandre Labuschagne:** the BlackRams signing notice gives birth date 1999-07-17 and 106 kg; the current individual profile gives 1999-06-17 and 107 kg. The catalog uses the current individual profile values and keeps the notice as a separate source for the transfer/history facts.
- **Tomoya Adachi:** the current BlackRams player profile shows 185.5 cm, while the official signing notice lists 187 cm. The catalog schema and app model store integer centimeters, so the catalog retains the notice's integer 187 cm. This difference remains visible in the cited sources.
- **Doei Azuma:** the Toyota player page gives July 23, while the League One season profiles and signing notice consistently give July 24. The catalog uses July 24, supported by multiple League One player-season pages and the signing notice.
- **Asaeli Lausii:** Toshiba roster grouping and his individual page show different primary positions. The catalog retains both ロック and プロップ rather than collapsing the club's conflict.

## Verification

- `python tools/validate_player_catalog.py`: 0 errors, 0 review warnings; 1,449 total records and 27 teams.
- `python tools/deduplicate_player_catalog.py`: preview found 0 additional merges.
- `python tools/normalize_player_history.py`: preview found 0 players needing normalization.
- `python -m unittest tests.test_deduplicate_player_catalog -v`: 7 passed, including a regression check that identical canonical names do not create false alias provenance.
- `dotnet test tests/OneRugbyNavi2.Core.Tests/OneRugbyNavi2.Core.Tests.csproj --no-restore`: 203 passed.
- `git diff --check` passed for the catalog and deduplication utility/test files.
- No application publish, commit, or GitHub Actions run was performed.

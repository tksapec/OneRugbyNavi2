# One Rugby Navi2 full data builder package

## 目的

このパッケージは、`One Rugby Navi2` 用のSQLite DBをリーグワン公式ページから生成するためのものです。

対象リポジトリ:

```text
https://github.com/tksapec/OneRugbyNavi2.git
```

## このパッケージでできること

- SQLiteスキーマを作成する
- 2025-26シーズンのチームマスターを作成する
- 各チームページから選手リンクを収集する
- 各選手個人ページから選手情報を取得する
- 出身校・チーム歴検索用文字列を生成する
- 出身校候補を生成する
- 顔写真・チーム画像をローカル保存するオプションを持つ
- source_pages / crawl_runs に取得ログを保存する

## 重要な注意

このチャットのファイル生成環境からは `league-one.jp` へ直接アクセスできなかったため、私の側で全ページを実際に取得して完全DBを生成することはできませんでした。

そのため、このパッケージは **CodexまたはローカルPCで実行して完全DBを生成するためのビルダー** です。

Codexで開発を始める場合は、このファイル群をリポジトリに入れて、まずビルダーを実行してください。

## 実行方法

```powershell
pip install -r requirements.txt
python build_full_database.py --output leagueone_full.db --download-images
```

画像を取得しない場合:

```powershell
python build_full_database.py --output leagueone_full.db
```

## 生成物

```text
leagueone_full.db
cache/
  teams/
  players/
```

## アプリへの組み込み案

生成されたDBを以下へ配置します。

```text
Resources/Raw/leagueone_seed.db
```

初回起動時に、アプリデータ領域へコピーします。

```text
AppData/OneRugbyNavi2/leagueone.db
```

## Codexへの指示案

```text
この build_full_database.py と schema.sql を使用して、One Rugby Navi2 用の初期SQLite DBを生成してください。
生成された leagueone_full.db を Resources/Raw/leagueone_seed.db としてアプリに同梱してください。
アプリ初回起動時には Resources/Raw/leagueone_seed.db を AppData/OneRugbyNavi2/leagueone.db へコピーし、以後はローカルDBを優先表示してください。
起動時に全選手ページを再取得しないでください。
全データ更新は手動更新画面から実行してください。
```

## 補足

このビルダーは初期実装用です。リーグワン公式ページのHTML構造に合わせて、Codexでパーサーを調整してください。
特に以下は実ページでの検証が必要です。

- チームロゴの正確な画像選択
- 選手顔写真の正確な画像選択
- 選手スタッツ表の完全パース
- プレーオフ/入替戦の詳細分類


## v1.0.1 修正内容

初版では、チームページ内の選手リンクが `player/123` のような相対パスで出力される場合に、抽出条件が厳しすぎて `0 players` になる可能性がありました。

v1.0.1では、以下の形式をすべて抽出対象にしました。

```text
/player/483707
player/483707
https://league-one.jp/player/483707
```

実行時に各チームで検出した選手リンク数を `[INFO]` として表示します。


## v1.0.2 修正内容

v1.0.1でも `0 players` になる場合に対応するため、以下を追加しました。

1. 選手個人ページの氏名抽出を強化しました。
   - `h1` が取れない場合でも、ページタイトル、本文中の `氏名 (PR)` 形式、パンくず相当の行から氏名を抽出します。
2. `--use-browser` オプションを追加しました。
   - 静的HTMLで選手リンクが取れないチームは、Playwright/Chromiumでページ描画後のHTMLから抽出できます。
3. 選手ページの解析に失敗した場合、`[WARN]` を表示します。

### 推奨再実行手順

まずは通常実行:

```powershell
python build_full_database.py --output leagueone_full.db --download-images
```

まだ多くのチームで `0 players` になる場合は、ブラウザ描画モードで実行してください。

```powershell
pip install -r requirements.txt
python -m playwright install chromium
python build_full_database.py --output leagueone_full.db --download-images --use-browser
```


## v1.0.3 修正内容

v1.0.2で `--use-browser` が引数として登録されていない問題を修正しました。

確認:

```powershell
python build_full_database.py --help
```

で `--use-browser` が表示されることを確認してください。

実行例:

```powershell
python build_full_database.py --output leagueone_full.db --download-images --use-browser
```


## v1.0.4 修正内容

v1.0.4では、`--use-browser` 実行時のタイムアウトと、選手リンク抽出失敗の調査性を改善しました。

主な変更:

- Playwrightの待機条件を `networkidle` から `domcontentloaded` に変更しました。
- デフォルトタイムアウトを60秒にしました。
- `--timeout` を追加しました。
- `--debug-dump-html` を追加しました。
- `--only-team-id` を追加しました。
- HTML内の `player/123` をhref構造に依存せず広く抽出します。
- `debug_html` にHTMLとリンク一覧を保存できるようにしました。

まずは1チームだけ確認してください。

```powershell
python build_full_database.py --output test.db --download-images --use-browser --only-team-id 110 --debug-dump-html --timeout 90
```

それでも `0 players` の場合は、`debug_html/team_110.html` と `debug_html/team_110_links.txt` を確認してください。


## v1.0.5 修正内容

v1.0.4では、`--only-team-id` のフィルタがチーム取得対象ではなく、チームマスター作成側に誤って適用される場合がありました。

その結果、`--only-team-id 110` を指定しても全チーム取得に進み、DBに登録されていないチームで `KeyError('100')` などが発生していました。

v1.0.5では以下のように修正しました。

- チームマスターは常に全26チームをDBへ登録する。
- `--only-team-id` は取得対象チームの絞り込みにのみ使う。
- 実行時に `[INFO] Fetch target teams: 1` のように取得対象チーム数を表示する。

確認コマンド:

```powershell
python build_full_database.py --output test.db --download-images --use-browser --only-team-id 110 --debug-dump-html --timeout 90
```

期待される表示:

```text
[INFO] Fetch target teams: 1
[DEBUG] dumped debug_html\team_110.html ...
```


## v1.0.6 修正内容

v1.0.5では、チームページから選手リンク取得までは成功しましたが、選手個人ページから氏名を抽出できず、`[WARN] Could not parse player id/name` が出る問題がありました。

v1.0.6では以下を修正しました。

- 選手ページの `<title>` / `og:title` から `徳永 一斗（2025-26）` 形式を優先抽出します。
- 本文中の `徳永 一斗 (PR)` 形式から氏名とポジションを抽出します。
- チームページのリンク文字列を氏名抽出の補助情報として保持します。
- 解析失敗した選手ページを最大5件 `debug_html/player_failed_*.html` として保存します。
- `[WARN]` にチームページ側のリンク文字列ヒントを表示します。

確認コマンド:

```powershell
python build_full_database.py --output test.db --download-images --use-browser --only-team-id 110 --debug-dump-html --timeout 90
```


## v1.0.7 修正内容

v1.0.6では、九州電力の選手リンク57件中44名を登録できましたが、外国籍選手や特殊表記の選手名で失敗していました。

失敗例:

```text
レイ ・タタフ
ショーン ・ロビンソン
アーロン ・キャロル
中づる(雨冠に隹・鳥の順) 憲章
```

v1.0.7では以下を修正しました。

- 中黒 `・` の前後スペースを正規化します。
- `レイ・タタフ` のようなカタカナ名を氏名として許容します。
- `ラーボニ・ウォーレンーボスアヤコ` のような長いカタカナ名を許容します。
- `中づる(雨冠に隹・鳥の順) 憲章` のような公式特殊表記を許容します。
- チームページのリンク文字列は公式表示名として扱い、最終フォールバック名として使用します。

確認コマンド:

```powershell
python build_full_database.py --output test.db --download-images --use-browser --only-team-id 110 --debug-dump-html --timeout 90
```

期待値:

```text
[OK] 九州電力キューデンヴォルテクス: 57 players
```


## v1.0.8 修正内容

v1.0.7では選手データ取得は改善しましたが、顔写真としてページ共通の `og:image` を拾うため、すべて同じ League One / NTT の画像になる問題がありました。

v1.0.8では以下を修正しました。

- 選手写真の取得では `og:image` を使用しないようにしました。
- `player_id`、`player`、`players`、`member`、`profile`、`photo` などを含む画像URLを優先します。
- `logo`、`sponsor`、`ntt`、`league-one`、`ogp`、`banner` などの共通画像を除外します。
- `--debug-dump-html` 有効時に、`debug_html/player_<id>_image_candidates.txt` を出力します。
- 選手写真候補が見つからない場合は、共通画像を誤登録せず空欄にします。

重要:
既に `cache` フォルダに誤った画像が保存されている場合があるため、再実行前に古い `test.db` / `leagueone_full.db` と `cache` を削除することを推奨します。

PowerShell例:

```powershell
Remove-Item -Force .\test.db -ErrorAction SilentlyContinue
Remove-Item -Recurse -Force .\cache -ErrorAction SilentlyContinue
python build_full_database.py --output test.db --download-images --use-browser --only-team-id 110 --debug-dump-html --timeout 90
```


## v1.0.9 修正内容

v1.0.8でも、選手写真として `This image is unavailable...` のような画像や、1x1 GIFプレースホルダーが保存される場合がありました。

原因:
- 公式ページの遅延読み込み画像で、`src` に1x1 GIFなどのプレースホルダーが入り、実画像はブラウザ描画後の `currentSrc` 側に入る場合があるため。

v1.0.9では以下を修正しました。

- Playwright描画後に、各 `img` へ `data-rendered-current-src`、`data-rendered-natural-width`、`data-rendered-natural-height` を付与します。
- `data-rendered-current-src` を画像候補として優先します。
- 1x1、2x2などのプレースホルダー画像を除外します。
- ダウンロードした画像の実体をバイト列から判定し、拡張子を実体に合わせます。
- GIF/PNG/JPEG/WebP以外の画像や、極小画像を保存しません。
- 選手写真として小さすぎる画像を除外します。

再実行前に古いDBとcacheを削除してください。

```powershell
Remove-Item -Force .\test.db -ErrorAction SilentlyContinue
Remove-Item -Recurse -Force .\cache -ErrorAction SilentlyContinue
python build_full_database.py --output test.db --download-images --use-browser --only-team-id 110 --debug-dump-html --timeout 90
```


## v1.0.10 修正内容

v1.0.9では、選手写真候補自体は `debug_html/player_<id>_image_candidates.txt` に出ていましたが、`assets.via-cloudflare.site` の実選手画像のスコアが `0` のため採用されませんでした。

実例:

```text
https://assets.via-cloudflare.site/1763962984298-_-KK_10_Samuel_Nozomu_Faialaga_01.jpg
```

また、チームロゴも `og:image` ではなく、チーム詳細ヘッダーの `figure.emblem img` から取得する必要がありました。

実例:

```text
https://league-one.s3.ap-northeast-1.amazonaws.com/image/team_info/11196_200x200_670e778dd09b0.png
```

v1.0.10では以下を修正しました。

- `assets.via-cloudflare.site` の画像を選手写真の強い候補として採用します。
- 画像の `naturalWidth` / `naturalHeight` が十分大きい場合に加点します。
- チームロゴは `.c-team-detail-ttl figure.emblem img` / `.emblem img` を優先して取得します。
- `image/team_info/*_200x200_*.png` をチームロゴ候補として扱います。
- `site_info` や `logo-header-leagueone` はチームロゴ候補から除外します。
- `--debug-dump-html` 時に `team_<id>_logo_candidates.txt` を出力します。

再実行前に古いDBとcacheを削除してください。

```powershell
Remove-Item -Force .\test.db -ErrorAction SilentlyContinue
Remove-Item -Recurse -Force .\cache -ErrorAction SilentlyContinue
python build_full_database.py --output test.db --download-images --use-browser --only-team-id 110 --debug-dump-html --timeout 90
```


## v1.0.11 修正内容

v1.0.10で `AttributeError: 'DatabaseBuilder' object has no attribute 'is_via_cloudflare_asset'` が発生する問題を修正しました。

また、チームロゴ候補としてメニュー内の別チームロゴを拾う可能性があったため、チーム詳細ヘッダー `.c-team-detail-ttl` 内の `figure.emblem img` を最優先するように修正しました。

主な変更:

- `is_via_cloudflare_asset()` を `DatabaseBuilder` 内に確実に定義。
- 選手写真では `assets.via-cloudflare.site` を強い候補として採用。
- チームロゴは `.c-team-detail-ttl` 内のエンブレム画像を最優先。
- `testing_image/team_info` や `image/menu` のロゴは減点。
- `team_<id>_logo_candidates.txt` でチームロゴ候補を確認可能。

再実行前に古いDBとcacheを削除してください。

```powershell
Remove-Item -Force .\test.db -ErrorAction SilentlyContinue
Remove-Item -Recurse -Force .\cache -ErrorAction SilentlyContinue
python build_full_database.py --output test.db --download-images --use-browser --only-team-id 110 --debug-dump-html --timeout 90
```


## v1.0.12 修正内容

v1.0.11では選手情報57名の取得は成功しましたが、選手写真・チームロゴのダウンロード時に `unsupported image content` で保存されませんでした。

v1.0.12では以下を修正しました。

- 画像ダウンロード時に `Referer: https://league-one.jp/` と画像向け `Accept` ヘッダーを付けます。
- 画像取得に通常HTML用の `Fetcher.get()` ではなく、画像専用 `download_image_bytes()` を使います。
- AVIF / HEIC / SVG の実体判定を追加しました。
- `unsupported image content` の場合、`content_type` と先頭32バイトをデバッグ出力します。
- WebP/AVIFのように寸法が取れない形式は、プレースホルダー判定で過剰に除外しないようにしました。

再実行前に古いDBとcacheを削除してください。

```powershell
Remove-Item -Force .\test.db -ErrorAction SilentlyContinue
Remove-Item -Recurse -Force .\cache -ErrorAction SilentlyContinue
python build_full_database.py --output test.db --download-images --use-browser --only-team-id 110 --debug-dump-html --timeout 90
```

それでも `unsupported image content` が出る場合は、出力される `content_type` と `first32` を確認してください。


## v1.0.13 修正内容

画像取得が概ね問題なさそうな段階のため、プロフィール項目の取得状況を確認しやすくしました。

主な変更:

- `身長／体重` のような全角スラッシュ表記に対応しました。
- ラベルと値が別行になる場合に対応しました。
- `身長/体重`、`生年月日`、`出身校・チーム歴`、`登録区分`、`リーグワンキャップ数` の取得確認用にサマリーを出力します。
- 実行後に以下のようなデータ品質サマリーを表示します。

```text
[SUMMARY] Data quality
[SUMMARY] players=57, registrations=57
[SUMMARY] height=57, weight=57, birth_date=57, school_history=57
[SUMMARY] registration_category=57, caps=57
[SUMMARY] player_photos=57, team_logos=1
```

再実行前に古いDBとcacheを削除してください。

```powershell
Remove-Item -Force .\test.db -ErrorAction SilentlyContinue
Remove-Item -Recurse -Force .\cache -ErrorAction SilentlyContinue
python build_full_database.py --output test.db --download-images --use-browser --only-team-id 110 --debug-dump-html --timeout 90
```

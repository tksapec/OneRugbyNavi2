# One Rugby Navi2 仕様書

> 更新: 2026-05-24 - ローカル生成済みDBをCodexが使用する方針を追加。


## 1. 文書の目的

この文書は、既存の `.NET MAUI Android` アプリ `LeagueOneScheduleViewerForAndroid` をベースに、新アプリ **One Rugby Navi2** を開発するための仕様書である。

Codexには、この文書を開発仕様として渡すことを想定する。

---

## 2. リポジトリ情報

### 2.1 開発対象リポジトリ

```text
https://github.com/tksapec/OneRugbyNavi2.git
```

### 2.2 ベースとする既存リポジトリ

```text
https://github.com/tksapec/LeagueOneScheduleViewerForAndroid.git
```

### 2.3 開発方針

既存アプリを直接改造するのではなく、`OneRugbyNavi2` として分岐し、別アプリとして共存できるようにする。

---

## 3. アプリ基本情報

### 3.1 アプリ名

```text
One Rugby Navi2
```

### 3.2 ApplicationId

既存アプリとAndroid上で共存できるように、ApplicationIdは変更する。

```xml
<ApplicationTitle>One Rugby Navi2</ApplicationTitle>
<ApplicationId>com.tksapec.onerugbynavi2</ApplicationId>
<ApplicationDisplayVersion>1.0.0</ApplicationDisplayVersion>
<ApplicationVersion>10000</ApplicationVersion>
```

### 3.3 基本方針

- 既存アプリの試合日程表示機能を維持する。
- リーグワン公式ページを情報源とする。
- 推測でデータを補完しない。
- 公式ページにない情報は `NULL` または空欄にする。
- 顔写真、チームロゴは個人利用前提でローカルキャッシュして表示する。
- 通信失敗時でも、前回取得済みデータを表示できるようにする。
- 既存アプリのカード型HMIデザインを踏襲する。
- 最終的には、試合、チーム、選手、出身校、ランキング、比較を扱える個人用リーグワンDBアプリとする。

---

## 4. アプリ全体構成

One Rugby Navi2は、次の機能を持つ。

```text
One Rugby Navi2
  ├─ 試合日程・結果
  ├─ プレーオフ・入替戦・順位決定戦対応
  ├─ チーム一覧
  ├─ チーム詳細
  ├─ 選手名鑑
  ├─ 出身校・チーム歴検索
  ├─ 出身校別集計
  ├─ 選手比較
  ├─ ランキング
  ├─ データ更新
  ├─ 差分確認
  └─ 情報
```

---

## 5. 情報源仕様

情報源はリーグワン公式ページのみとする。

| データ | 取得元 |
|---|---|
| チーム一覧 | リーグワン公式チーム一覧 |
| チーム詳細 | リーグワン公式チームページ |
| 試合日程 | `schedule_table` 系ページ |
| 試合結果 | `schedule/?year=YYYY` |
| プレーオフ | 日程ページ、結果ページ |
| 入替戦 | 日程ページ、結果ページ |
| 選手一覧 | 各チームページの選手一覧 |
| 選手詳細 | 各選手個人ページ |
| 顔写真 | 選手ページ上の画像 |
| チームロゴ | チームページ上の画像 |
| 選手スタッツ | 選手個人ページ |

### 5.1 データ補完ルール

- 公式ページに記載がない情報は推測しない。
- 公式ページ上の表記を正とする。
- 表示や検索のために正規化した値を作成してもよいが、公式原文は必ず保持する。
- 公式ページの構造変更に備え、取得元URL、HTMLハッシュ、解析結果を記録する。

---

## 6. 日程機能仕様

### 6.1 現状の課題

既存アプリは、主に以下の3分類を前提としている。

```text
Div1
Div2
Div3
```

ただし、One Rugby Navi2では、通常リーグ戦に加えて以下を扱う必要がある。

```text
通常リーグ戦
プレーオフ
入替戦
順位決定戦
その他特殊試合
```

既存実装が `Div1`、`Div2`、`Div3` の3分類に強く依存している場合は、試合モデルを汎用化する。

### 6.2 MatchCategory

次の列挙型を追加する。

```csharp
public enum MatchCategory
{
    RegularSeason,
    Playoff,
    Replacement,
    Placement,
    Other,
    Unknown
}
```

### 6.3 日程データモデル

既存の `ScheduleFetcher.Item` または同等のモデルに、以下を追加する。

```csharp
public string Season { get; set; } = "";
public string Division { get; set; } = "";
public MatchCategory MatchCategory { get; set; } = MatchCategory.Unknown;
public string StageName { get; set; } = "";
public string Round { get; set; } = "";
public bool HasUndeterminedTeam { get; set; }
public string HomeSeedText { get; set; } = "";
public string AwaySeedText { get; set; } = "";
public string SourceUrl { get; set; } = "";
public string RawJson { get; set; } = "";
```

### 6.4 表示例

通常試合:

```text
第12節 / DIV1
横浜キヤノンイーグルス vs コベルコ神戸スティーラーズ
```

プレーオフ:

```text
PO1 / 準々決勝①
D1 リーグ戦4位 vs D1 リーグ戦5位
```

入替戦:

```text
D1/D2入替戦 第1戦
D1 11位 vs D2 2位
```

### 6.5 未定チーム表示

以下のような表記を通常のチーム名として無理に解釈しない。

```text
D1 リーグ戦4位
D1 リーグ戦5位
準々決勝① 勝者
準決勝① 敗者
```

チーム未定の場合:

- チームロゴは表示しない。
- 順位/ステージバッジを表示する。
- `HasUndeterminedTeam = true` とする。
- `HomeSeedText`、`AwaySeedText` に原文を保持する。

### 6.6 日程取得元

`content/schedule_table/YYYY/div1`、`div2`、`div3` だけに依存しない。

以下を併用する。

```text
https://league-one.jp/content/schedule_table/YYYY/div1
https://league-one.jp/content/schedule_table/YYYY/div2
https://league-one.jp/content/schedule_table/YYYY/div3
https://league-one.jp/schedule/?year=YYYY
```

`schedule_table` に存在しない試合でも、`schedule/?year=YYYY` に表示される場合は取得できるようにする。

---

## 7. 選手名鑑仕様

### 7.1 目的

リーグワン公式ページから全チームの所属選手情報を取得し、ローカルSQLite DBに保存する。

### 7.2 表示項目

| 項目 | 内容 |
|---|---|
| 顔写真 | ローカル保存画像 |
| 名前 | 日本語名 |
| 英字名 | 掲載がある場合 |
| チーム名 | 現所属 |
| ポジション | PR/HO/LOなど |
| 身長 | cm |
| 体重 | kg |
| 生年月日 | yyyy-MM-dd |
| 年齢 | 生年月日から計算 |
| 公式表示年齢 | 公式ページ掲載値 |
| 登録区分 | カテゴリA/B/Cなど |
| キャップ数 | リーグワンキャップ数 |
| 出身校・チーム歴 | 公式原文 |
| 選手スタッツ | 試合別成績 |
| 公式ページURL | 選手ページ |

### 7.3 選手カード表示

```text
[顔写真] 徳永 一斗
         九州電力キューデンヴォルテクス / PR
         181cm / 117kg / 1993-04-08 / 33歳
         カテゴリA / Caps 42
         佐賀工業高校 帝京大学
```

### 7.4 選手詳細表示

```text
[顔写真大]
徳永 一斗
Kazuto Tokunaga

チーム: 九州電力キューデンヴォルテクス
ポジション: PR
身長/体重: 181cm / 117kg
生年月日: 1993-04-08
年齢: 33歳
登録区分: カテゴリA
リーグワンキャップ数: 42

出身校・チーム歴:
佐賀工業高校 帝京大学

選手スタッツ:
第1節 ...
```

---

## 8. 出身校・チーム歴検索仕様

### 8.1 目的

全チームを横断して、出身校・チーム歴に特定の文字列を含む選手を検索できるようにする。

### 8.2 検索例

```text
佐賀工業
```

この検索により、`出身校・チーム歴` に `佐賀工業` を含む選手を全チーム横断で一覧表示する。

### 8.3 検索結果表示項目

| 項目 | 表示 |
|---|---|
| 顔写真 | 名前の横 |
| 名前 | 日本語名 |
| チーム名 | 現所属チーム |
| ポジション | PR/HOなど |
| 身長 | cm |
| 体重 | kg |
| 生年月日 | yyyy-MM-dd |
| 年齢 | 生年月日から計算 |
| 登録区分 | カテゴリ |
| キャップ数 | リーグワンキャップ数 |
| 出身校・チーム歴 | 公式原文 |

### 8.4 検索用カラム

`player_season_registrations` に以下を持たせる。

| カラム | 内容 |
|---|---|
| school_team_history_text | 公式原文 |
| school_team_history_search_text | 検索用正規化文字列 |

例:

```text
school_team_history_text:
佐賀工業高校 帝京大学

school_team_history_search_text:
佐賀工業高校帝京大学
```

このため、`佐賀工業` で `佐賀工業高校` に部分一致する。

### 8.5 正規化ルール

| 処理 | 例 |
|---|---|
| 前後空白削除 | ` 佐賀工業 ` → `佐賀工業` |
| Unicode正規化 | 全角英数字 → 半角英数字 |
| 空白除去 | `佐賀 工業` → `佐賀工業` |
| 中黒除去 | `佐賀・工業` → `佐賀工業` |
| 英字大文字化 | `meiji` → `MEIJI` |
| 原文保持 | 表示は公式原文 |

### 8.6 ソート項目

検索結果は以下でソートできるようにする。

| ソート項目 | 昇順/降順 |
|---|---|
| 名前 | 可 |
| チーム名 | 可 |
| ポジション | 可 |
| 身長 | 可 |
| 体重 | 可 |
| 生年月日 | 可 |
| 年齢 | 可 |
| 登録区分 | 可 |
| キャップ数 | 可 |
| 出身校・チーム歴 | 可 |

### 8.7 UI

スマホ画面では、列ヘッダー型の表ではなく、カード型一覧 + ソートPickerを基本とする。

```text
検索: [佐賀工業__________]

検索対象: [出身校・チーム歴 v]
並び替え: [チーム名 v] [昇順 v]

検索結果: 8人
```

---

## 9. 出身校候補・検索履歴仕様

### 9.1 出身校候補

DB内の `出身校・チーム歴` から候補語を生成する。

候補例:

```text
[佐賀工業] [東福岡] [桐蔭学園] [帝京大学] [明治大学]
```

完全な学校名分解は誤判定の可能性があるため、以下の方針にする。

| 項目 | 方針 |
|---|---|
| 表示 | 公式原文を優先 |
| 候補 | 補助機能 |
| 分解結果 | 信頼度付き |
| 不明確な語句 | `unknown` |

### 9.2 検索履歴

検索履歴を保存し、出身校検索画面に表示する。

```text
最近の検索:
佐賀工業
東福岡
帝京大学
明治大学
```

---

## 10. 選手比較・集計・ランキング仕様

### 10.1 選手比較

複数選手を選択し、比較できるようにする。

比較項目:

| 項目 |
|---|
| チーム |
| ポジション |
| 身長 |
| 体重 |
| 年齢 |
| 登録区分 |
| キャップ数 |
| 出身校・チーム歴 |

### 10.2 出身校別集計

例:

```text
佐賀工業出身選手: 8人
平均身長: 181.5cm
平均体重: 103.2kg
最多ポジション: PR
平均キャップ数: 24.1
```

### 10.3 ランキング

| ランキング | 内容 |
|---|---|
| 身長順 | 高い順/低い順 |
| 体重順 | 重い順/軽い順 |
| 年齢順 | 年上順/若い順 |
| キャップ数順 | 多い順 |
| 出身校別人数 | 多い順 |
| チーム別出身校人数 | 多い順 |

### 10.4 ランキング例

```text
帝京大学出身選手 キャップ数ランキング
1. 選手A / チームA / Caps 80
2. 選手B / チームB / Caps 64
3. 選手C / チームC / Caps 51
```

---

## 11. DB仕様

### 11.1 テーブル一覧

```text
seasons
divisions
teams
team_profiles
team_stadiums
matches
players
player_season_registrations
player_school_team_histories
player_match_stats
asset_files
source_pages
crawl_runs
search_history
school_candidates
app_settings
```

### 11.2 seasons

| カラム | 内容 |
|---|---|
| id | 内部ID |
| season_code | `2025-26` など |
| season_year | `2025` など |
| source_url | 取得元URL |
| fetched_at | 取得日時 |

### 11.3 divisions

| カラム | 内容 |
|---|---|
| id | 内部ID |
| season_id | シーズンID |
| division_name | `DIVISION 1` / `DIVISION 2` / `DIVISION 3` |
| division_code | `DIV1` / `DIV2` / `DIV3` |
| conference | `A` / `B` / NULL |

### 11.4 teams

| カラム | 内容 |
|---|---|
| id | 内部ID |
| league_one_team_id | リーグワン公式チームID |
| season_id | シーズンID |
| division_id | Division ID |
| team_name | チーム名 |
| short_name | 略称 |
| area_text | 地域 |
| team_url | リーグワン公式チームページ |
| logo_asset_id | チームロゴ |
| raw_json | 元情報 |
| fetched_at | 取得日時 |

### 11.5 matches

| カラム | 内容 |
|---|---|
| id | 内部ID |
| season_id | シーズンID |
| division_id | Division ID |
| match_category | `RegularSeason` / `Playoff` / `Replacement` / `Placement` / `Other` / `Unknown` |
| stage_name | `第1節` / `PO1` / `D1/D2入替戦 第1戦` など |
| round_name | 表示用ラウンド名 |
| match_date | 開催日 |
| kickoff_time | キックオフ |
| conference | カンファレンス |
| home_team_id | ホームチームID |
| away_team_id | アウェーチームID |
| home_team_text | 公式表記 |
| away_team_text | 公式表記 |
| home_seed_text | 未定表記 |
| away_seed_text | 未定表記 |
| has_undetermined_team | 未定チームフラグ |
| pref | 都道府県 |
| venue | 会場 |
| home_score | ホーム得点 |
| away_score | アウェー得点 |
| status | 試合状態 |
| match_info_url | Match Info URL |
| report_url | Report URL |
| source_url | 取得元 |
| raw_json | 元情報 |
| fetched_at | 取得日時 |

### 11.6 players

| カラム | 内容 |
|---|---|
| id | 内部ID |
| league_one_player_id | 選手ID |
| name_ja | 日本語名 |
| name_en | 英字名 |
| birth_date | 生年月日 |
| profile_url | 公式選手ページURL |
| raw_json | 個人ページ情報 |
| created_at | 初回登録日時 |
| updated_at | 更新日時 |

### 11.7 player_season_registrations

| カラム | 内容 |
|---|---|
| id | 内部ID |
| player_id | 選手ID |
| season_id | シーズンID |
| team_id | 所属チームID |
| division_id | Division ID |
| position_code | ポジション |
| position_name | ポジション名 |
| height_cm | 身長 |
| weight_kg | 体重 |
| age_displayed | 公式表示年齢 |
| age_calculated | 計算年齢 |
| registration_category | 登録区分 |
| league_one_caps | キャップ数 |
| school_team_history_text | 出身校・チーム歴原文 |
| school_team_history_search_text | 検索用文字列 |
| photo_asset_id | 顔写真 |
| raw_json | 元情報 |
| fetched_at | 取得日時 |

### 11.8 player_school_team_histories

| カラム | 内容 |
|---|---|
| id | 内部ID |
| player_id | 選手ID |
| season_id | シーズンID |
| raw_text | 原文 |
| item_order | 順序 |
| item_name | 分解名 |
| normalized_name | 正規化名 |
| item_type | `high_school` / `university` / `club` / `unknown` |
| confidence | 信頼度 |

### 11.9 player_match_stats

| カラム | 内容 |
|---|---|
| id | 内部ID |
| player_id | 選手ID |
| season_id | シーズンID |
| match_id | 試合ID。紐付けできない場合NULL |
| competition_name | 大会名 |
| division_name | ディビジョン |
| round_name | 節・ラウンド |
| match_date | 開催日 |
| opponent_team_name | 対戦チーム |
| score_text | スコア原文 |
| p | P |
| t | T |
| g | G |
| pg | PG |
| dg | DG |
| success_rate_text | 成功率原文 |
| success_rate | 数値化できる場合 |
| match_url | 試合ページ |
| raw_json | 元情報 |

### 11.10 school_candidates

| カラム | 内容 |
|---|---|
| id | 内部ID |
| season_id | シーズンID |
| candidate_text | 候補名 |
| normalized_text | 検索用文字列 |
| candidate_type | `high_school` / `university` / `club` / `unknown` |
| player_count | 該当人数 |
| confidence | 信頼度 |
| last_updated_at | 更新日時 |

### 11.11 search_history

| カラム | 内容 |
|---|---|
| id | 内部ID |
| search_type | `school` / `player` / `team` |
| keyword | 検索語 |
| normalized_keyword | 正規化検索語 |
| hit_count | ヒット数 |
| searched_at | 検索日時 |

### 11.12 asset_files

| カラム | 内容 |
|---|---|
| id | 内部ID |
| asset_type | `player_photo` / `team_logo` / `team_image` |
| related_type | `player` / `team` |
| related_id | 対象ID |
| source_url | 公式画像URL |
| local_path | 保存先 |
| file_name | ファイル名 |
| content_hash | 画像ハッシュ |
| downloaded_at | 保存日時 |
| last_checked_at | 最終確認日時 |
| is_available | 利用可能 |

### 11.13 source_pages

| カラム | 内容 |
|---|---|
| id | 内部ID |
| crawl_run_id | クロール実行ID |
| source_type | `team_index` / `team_detail` / `player_detail` / `schedule` |
| url | URL |
| http_status | HTTPステータス |
| content_hash | HTMLハッシュ |
| fetched_at | 取得日時 |
| parse_status | `success` / `partial` / `failed` |
| error_message | エラー内容 |
| raw_html_path | 必要に応じて保存 |

### 11.14 crawl_runs

| カラム | 内容 |
|---|---|
| id | 内部ID |
| started_at | 開始日時 |
| finished_at | 終了日時 |
| target_season | 対象シーズン |
| status | `running` / `success` / `partial` / `failed` |
| total_team_count | チーム数 |
| total_player_count | 選手数 |
| total_match_count | 試合数 |
| error_count | エラー数 |
| note | メモ |

---

## 12. ローカル保存仕様

### 12.1 保存構成

```text
AppData/
  OneRugbyNavi2/
    leagueone.db
    cache/
      teams/
        107/
          logo.png
          header.jpg
      players/
        483707/
          profile.jpg
      html/
        source_pages/
```

### 12.2 オフライン動作

| 状態 | 動作 |
|---|---|
| 通信可能 | 最新取得 |
| 通信失敗 | 前回DBを表示 |
| 画像取得失敗 | 前回画像を表示 |
| 一部パース失敗 | 取得できた範囲を保存 |
| 初回取得失敗 | 空画面 + エラー表示 |
| 更新成功 | 差分結果を表示 |

---

## 13. UI仕様

### 13.1 デザイン方針

既存アプリのカード型HMIデザインを踏襲する。

| 要素 | 仕様 |
|---|---|
| 背景色 | `#F5F7FB` |
| メインブルー | `#0057B8` |
| 濃紺 | `#10233F` |
| カード背景 | 白 |
| カード角丸 | 16〜18 |
| 重要カード | 濃紺背景 + 白文字 |
| 一覧 | `CollectionView` |
| 状態表示 | チップ表示 |
| 画像なし | イニシャルまたはポジションバッジ |

### 13.2 画面一覧

```text
MainPage
SchedulePage
PlayerDirectoryPage
SchoolSearchPage
TeamListPage
TeamDetailPage
PlayerDetailPage
PlayerComparePage
RankingPage
UpdateStatusPage
InfoPage
```

### 13.3 ナビゲーション

機能が増えるため、下部タブ方式を推奨する。

```text
[日程] [選手] [出身校] [ランキング] [情報]
```

ただし、既存アプリの画面構成との整合を優先し、実装負荷が高い場合は上部ナビゲーションでもよい。

```text
更新 | 順位表 | 選手名鑑 | 出身校 | 情報
```

---

## 14. データ更新・差分表示

### 14.1 更新処理

更新処理では以下を行う。

```text
1. チーム一覧ページを取得
2. 各チームページを取得
3. 選手一覧を取得
4. 各選手個人ページを取得
5. 試合日程・結果を取得
6. 顔写真・チームロゴを取得
7. SQLiteを更新
8. 差分を検出
9. 更新結果を表示
```

### 14.2 差分表示

更新完了後に、以下のような差分サマリーを表示する。

```text
更新結果:
新規選手: 3人
未掲載選手: 2人
所属変更: 1人
身長/体重変更: 4人
登録区分変更: 2人
キャップ数更新: 36人
スタッツ追加: 48件
顔写真更新: 12件
チームロゴ更新: 1件
取得失敗: 1ページ
```

### 14.3 差分検出対象

| 差分 | 内容 |
|---|---|
| 新規選手 | 前回DBになかった選手 |
| 未掲載選手 | 前回いたが今回いない選手 |
| 所属変更 | team_id変更 |
| ポジション変更 | position変更 |
| 身長/体重変更 | 数値変更 |
| 登録区分変更 | カテゴリ変更 |
| キャップ数更新 | caps変更 |
| 出身校表記変更 | school_team_history_text変更 |
| 顔写真変更 | 画像URL/ハッシュ変更 |
| スタッツ追加 | 新しい試合行 |
| パース失敗 | source_pagesに記録 |

---

## 15. 実装フェーズ

すべての仕様を採用するが、実装は段階的に行う。

### Phase 1: One Rugby Navi2として独立

```text
- 新リポジトリ作成
- アプリ名変更
- ApplicationId変更
- バージョン初期化
- 既存日程機能の動作確認
```

### Phase 2: 日程モデル汎用化

```text
- MatchCategory追加
- PO/入替/順位決定戦モデル追加
- チーム未定表記対応
- バッジ表示対応
```

### Phase 3: SQLite基盤

```text
- DB作成
- マイグレーション管理
- app_settings追加
- source_pages/crawl_runs追加
```

### Phase 4: チームDB

```text
- チーム一覧取得
- チーム詳細取得
- チームロゴ保存
- チーム詳細画面追加
```

### Phase 5: 選手DB

```text
- 選手一覧取得
- 選手個人ページ取得
- raw_json保存
- 顔写真保存
- 選手スタッツ保存
```

### Phase 6: 選手名鑑UI

```text
- 選手一覧画面
- 選手詳細画面
- Division/チーム/ポジション/登録区分フィルタ
- ソート機能
```

### Phase 7: 出身校検索

```text
- school_team_history_search_text追加
- 正規化検索
- チーム横断検索
- 検索履歴
- 候補表示
```

### Phase 8: 集計・ランキング

```text
- 出身校別人数
- 平均身長/体重/年齢
- キャップ数ランキング
- チーム別集計
```

### Phase 9: 選手比較

```text
- 比較対象選択
- 比較画面
- 体格/年齢/キャップ数比較
```

### Phase 10: 品質改善

```text
- 差分表示
- パース失敗ログ
- HTMLハッシュ保存
- 取得失敗時の復旧
- UI調整
```

---

## 16. Codex実装指示

以下をCodexへの実装指示として扱う。

```text
既存の .NET MAUI Android アプリ LeagueOneScheduleViewerForAndroid をベースに、新アプリ One Rugby Navi2 として分岐・拡張してください。

開発対象リポジトリ:
https://github.com/tksapec/OneRugbyNavi2.git

ベースリポジトリ:
https://github.com/tksapec/LeagueOneScheduleViewerForAndroid.git

基本方針:
- 既存アプリとは別アプリとして共存できるようにする
- ApplicationTitle は One Rugby Navi2 とする
- ApplicationId は com.tksapec.onerugbynavi2 とする
- バージョンは 1.0.0 / 10000 から開始する
- 既存の日程表示、フィルタ、お気に入り、キャッシュ機能を壊さない
- ローカルPCで生成されたSQLite DBを `Resources/Raw/leagueone_seed.db` として使用する
- 初回起動時に `Resources/Raw/leagueone_seed.db` を `AppData/OneRugbyNavi2/leagueone.db` へコピーする
- 起動時に全チーム・全選手・全画像を取得しに行かない
- 情報源はリーグワン公式ページのみとする
- 推測でデータを補完しない
- 公式ページにない情報は NULL または空欄にする

日程機能:
- 通常リーグ戦だけでなく、プレーオフ、入替戦、順位決定戦を扱えるモデルに変更する
- MatchCategory として RegularSeason, Playoff, Replacement, Placement, Other, Unknown を持つ
- D1スケジュールの PO1, PO2, PO3 を正しく表示する
- 「D1 リーグ戦4位」「準々決勝① 勝者」などの未定チーム表記を表示できるようにする
- チーム未定の場合はロゴではなく順位/ステージバッジを表示する
- schedule_table だけでなく schedule/?year=YYYY のカード情報も取得元として利用できるようにする

DB:
- SQLiteでローカルDBを作成する
- seasons, divisions, teams, team_profiles, team_stadiums, matches, players, player_season_registrations, player_school_team_histories, player_match_stats, asset_files, source_pages, crawl_runs, search_history, school_candidates, app_settings を作成する
- 取得元URL、取得日時、HTMLハッシュ、parse_status、error_message を保存する

選手名鑑:
- チーム一覧ページから全チームを取得する
- 各チームページから選手一覧を取得する
- 各選手個人ページから掲載情報をすべて取得し raw_json に保存する
- 表示・検索に使う主要項目は正規化カラムに保存する
- 顔写真とチームロゴはローカルキャッシュして表示する
- オフラインでも前回取得データを表示する
- 選手一覧画面と選手詳細画面を追加する

出身校検索:
- 出身校・チーム歴のチーム横断検索画面を追加する
- "佐賀工業" で検索すると、出身校・チーム歴に "佐賀工業" を含む選手を全チーム横断で一覧表示する
- school_team_history_text に公式原文を保存する
- school_team_history_search_text に検索用正規化文字列を保存する
- 検索時は入力値も同じ正規化を行う
- 正規化では、前後空白、全角/半角差、空白、中黒などを吸収する
- 部分一致検索にする
- 表示項目は、顔写真、名前、チーム名、ポジション、身長、体重、生年月日、年齢、登録区分、リーグワンキャップ数、出身校・チーム歴とする
- 検索結果は、名前、チーム名、ポジション、身長、体重、生年月日、年齢、登録区分、キャップ数、出身校・チーム歴でソートできるようにする

出身校候補・履歴:
- school_candidates テーブルを作成する
- DB内の出身校・チーム歴から候補語を生成する
- 検索履歴を search_history に保存する
- 出身校検索画面に候補と最近の検索を表示する

ランキング・集計:
- 身長順、体重順、年齢順、キャップ数順のランキング画面を追加する
- 出身校別人数ランキングを追加する
- 出身校別の平均身長、平均体重、平均年齢、平均キャップ数を表示する
- チーム別の出身校集計を表示する

選手比較:
- 複数選手を選択して比較できる画面を追加する
- 比較項目は、チーム、ポジション、身長、体重、年齢、登録区分、キャップ数、出身校・チーム歴とする

UI:
- 既存のカード型HMIデザインを踏襲する
- 背景色 #F5F7FB、メイン色 #0057B8 / #10233F を維持する
- 下部タブまたは上部ナビゲーションで、日程、選手、出身校、ランキング、情報へ移動できるようにする
- 顔写真がない選手はイニシャルまたはポジションバッジを表示する
- チームロゴがない場合は既存と同様に略称バッジを表示する

更新・差分:
- 更新後に、新規選手、未掲載選手、所属変更、体格変更、登録区分変更、キャップ数更新、出身校表記変更、画像更新、スタッツ追加、取得失敗件数を表示する
- 通信失敗時は前回キャッシュを表示する
- パース失敗時は parse_status と error_message を記録する
```

---


---

## 18. ローカル生成DBの使用方針

### 18.1 基本方針

One Rugby Navi2では、リーグワン公式ページから全データを取得して作成したSQLite DBを、開発者がローカルPCで事前生成する。

Codexは、リーグワン公式ページから全データを直接取得してDBを作成するのではなく、**ローカルで生成済みのDBファイルをアプリに組み込んで使用する** 方針とする。

### 18.2 ローカルで実行する処理

開発者は、別途提供されるDB生成スクリプトをローカルPCで実行する。

想定コマンド例:

```powershell
pip install -r requirements.txt
python build_full_database.py --output leagueone_full.db --download-images
```

画像を取得しない場合:

```powershell
python build_full_database.py --output leagueone_full.db
```

生成される主なファイル:

```text
leagueone_full.db
cache/
  teams/
  players/
```

### 18.3 Codexが使用するDB

Codexは、ローカルで生成済みのDBファイルを受け取り、アプリに同梱する。

アプリ同梱時の配置先:

```text
Resources/Raw/leagueone_seed.db
```

ローカルで生成したファイル名が `leagueone_full.db` の場合でも、アプリに同梱するときは以下のようにリネームしてよい。

```text
leagueone_full.db
→ Resources/Raw/leagueone_seed.db
```

### 18.4 アプリ初回起動時の処理

アプリ初回起動時に、`Resources/Raw/leagueone_seed.db` をアプリデータ領域へコピーする。

コピー先:

```text
AppData/OneRugbyNavi2/leagueone.db
```

初回起動処理の仕様:

```text
1. AppData/OneRugbyNavi2/leagueone.db が存在するか確認する。
2. 存在しない場合、Resources/Raw/leagueone_seed.db をコピーする。
3. コピー後、AppData側の leagueone.db を通常利用DBとして開く。
4. 以後の読み書きは Resources/Raw ではなく、AppData側のDBに対して行う。
```

### 18.5 通常起動時の処理

通常起動時は、必ずローカルDBを優先して表示する。

```text
1. AppData/OneRugbyNavi2/leagueone.db を開く。
2. DB内の最終更新日時を確認する。
3. 既存データを即時表示する。
4. 起動時に全チーム・全選手・全画像を再取得しない。
```

### 18.6 更新処理との関係

アプリ内の手動更新機能は将来的に実装してよいが、初期開発では **ローカル生成済みDBの表示を優先** する。

初期実装で必須とする範囲:

```text
- Resources/Raw/leagueone_seed.db の同梱
- 初回起動時のAppDataへのDBコピー
- AppData側DBの読み込み
- 選手名鑑、出身校検索、チーム一覧、ランキングのDB表示
```

初期実装で後回しにしてよい範囲:

```text
- アプリ内での全データ再取得
- アプリ内での全選手ページ再クロール
- アプリ内での顔写真一括再取得
- アプリ内でのDB再生成
```

### 18.7 手動更新を実装する場合の方針

将来的にアプリ内で更新機能を実装する場合は、既存DBを直接上書きしない。

推奨方式:

```text
1. 現在の AppData/OneRugbyNavi2/leagueone.db は表示用として維持する。
2. 更新データは AppData/OneRugbyNavi2/leagueone_update_temp.db に取得する。
3. 取得完了後に整合性確認と差分比較を行う。
4. 問題がなければ leagueone.db へ反映する。
5. 失敗した場合は temp DB を破棄し、既存DBをそのまま使う。
```

### 18.8 Codexへの明確な実装指示

Codexは以下を守ること。

```text
- build_full_database.py の実行はローカルPC側で行う前提とする。
- Codexは、ローカルで生成されたSQLite DBをアプリに組み込む処理を実装する。
- 生成済みDBは Resources/Raw/leagueone_seed.db として扱う。
- 初回起動時に Resources/Raw/leagueone_seed.db を AppData/OneRugbyNavi2/leagueone.db へコピーする。
- アプリは AppData側の leagueone.db を読み込む。
- 起動時に全選手ページ、全チームページ、全画像を取得しに行かない。
- 通常起動時はローカルDBの表示を最優先する。
- データ更新機能は、初期段階では必須ではない。
- 更新機能を実装する場合は一時DB方式を採用し、失敗時に既存DBを壊さない。
```

### 18.9 仕様上の位置づけ

この方式により、One Rugby Navi2の初期開発では以下を早期に確認できる。

```text
- SQLite DBの読み込み
- 初回DBコピー処理
- チーム一覧表示
- 選手名鑑表示
- 出身校検索
- ソート
- ランキング
- 選手比較
- 顔写真・チームロゴのローカル表示
```

全データ取得処理は、アプリ本体ではなくローカルDB生成スクリプト側で検証する。


## 17. 実装時の注意

- まずはPhase 1を完了させ、既存日程機能がOne Rugby Navi2として動作することを確認する。
- 一度にすべてを実装しない。
- 日程モデル汎用化とSQLite基盤を先に作る。
- 選手名鑑、出身校検索、ランキング、比較は段階的に追加する。
- 取得処理は失敗してもアプリ全体が落ちないようにする。
- 公式ページの構造変更に備えて、パース失敗時のログを必ず残す。
- 表示用データと公式原文を分離する。
- 正規化データは検索・ソート用、公式原文は表示・確認用とする。

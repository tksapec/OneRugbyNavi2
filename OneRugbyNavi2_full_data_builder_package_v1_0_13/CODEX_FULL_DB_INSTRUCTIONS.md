# Codexへの追加指示: 完全DB生成

One Rugby Navi2では、アプリ起動時に全データを取得しない。
あらかじめ生成したSQLite DBをアプリに同梱する。

## 実施内容

1. `build_full_database.py` をローカルまたはCodex環境で実行する。
2. 生成された `leagueone_full.db` を `Resources/Raw/leagueone_seed.db` として配置する。
3. アプリ初回起動時に `leagueone_seed.db` をアプリデータ領域へコピーする。
4. 通常起動時はローカルDBを優先表示する。
5. 手動更新時のみ、リーグワン公式ページから再取得する。
6. 更新時は一時DBに取得し、成功後に本DBへ反映する。

## 注意

- 公式ページにない情報は補完しない。
- 表示は公式原文を優先する。
- 検索とソートには正規化カラムを使う。
- 取得失敗時は既存DBを維持する。


## ローカルDB生成時の注意

通常実行で選手数が0になるチームが多い場合は、公式ページの選手一覧が静的HTMLだけでは取得できていない可能性があります。
その場合は以下で実行してください。

```powershell
pip install -r requirements.txt
python -m playwright install chromium
python build_full_database.py --output leagueone_full.db --download-images --use-browser
```

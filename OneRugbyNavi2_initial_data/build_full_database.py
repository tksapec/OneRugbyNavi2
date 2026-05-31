#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
One Rugby Navi2 full database builder.

Purpose:
    Build a local SQLite database for One Rugby Navi2 from official League One pages.

Target repository:
    https://github.com/tksapec/OneRugbyNavi2.git

Important:
    This script fetches data from league-one.jp.
    Run it on a PC or CI/Codex environment that has internet access.

Usage:
    pip install -r requirements.txt
    python build_full_database.py --output leagueone_full.db --download-images

Recommended app placement:
    Resources/Raw/leagueone_seed.db
"""

from __future__ import annotations

import argparse
import hashlib
import json
import re
import sqlite3
import time
import unicodedata
import sys
from dataclasses import dataclass
from datetime import date, datetime, timezone
from pathlib import Path
from typing import Iterable, Optional
from urllib.parse import urljoin, urlparse

import requests
from bs4 import BeautifulSoup


BASE_URL = "https://league-one.jp/"
TEAM_INDEX_URL = "https://league-one.jp/team/"
SCHEDULE_URL = "https://league-one.jp/schedule/?year={year}"
SCHEDULE_TABLE_URL = "https://league-one.jp/content/schedule_table/{year}/{division}"

USER_AGENT = (
    "OneRugbyNavi2DatabaseBuilder/1.0 "
    "(personal use; https://github.com/tksapec/OneRugbyNavi2)"
)

# 2025-26 official team index seed.
# This mapping is intentionally embedded so the builder can assign divisions
# even if the page layout changes slightly.
TEAM_MASTER_2025_26 = [
    # D1 Conference A
    {"league_one_team_id": "100", "division_code": "DIV1", "conference": "A", "team_name": "浦安D-Rocks", "short_name": "浦安ＤＲ", "area_text": "千葉県浦安市", "team_url": "https://league-one.jp/team/100"},
    {"league_one_team_id": "102", "division_code": "DIV1", "conference": "A", "team_name": "埼玉パナソニックワイルドナイツ", "short_name": "埼玉ＷＫ", "area_text": "埼玉県", "team_url": "https://league-one.jp/team/102"},
    {"league_one_team_id": "98", "division_code": "DIV1", "conference": "A", "team_name": "静岡ブルーレヴズ", "short_name": "静岡ＢＲ", "area_text": "静岡県", "team_url": "https://league-one.jp/team/98"},
    {"league_one_team_id": "103", "division_code": "DIV1", "conference": "A", "team_name": "東芝ブレイブルーパス東京", "short_name": "ＢＬ東京", "area_text": "東京都", "team_url": "https://league-one.jp/team/103"},
    {"league_one_team_id": "106", "division_code": "DIV1", "conference": "A", "team_name": "三菱重工相模原ダイナボアーズ", "short_name": "相模原ＤＢ", "area_text": "神奈川県相模原市", "team_url": "https://league-one.jp/team/106"},
    {"league_one_team_id": "107", "division_code": "DIV1", "conference": "A", "team_name": "横浜キヤノンイーグルス", "short_name": "横浜Ｅ", "area_text": "神奈川県横浜市", "team_url": "https://league-one.jp/team/107"},
    # D1 Conference B
    {"league_one_team_id": "97", "division_code": "DIV1", "conference": "B", "team_name": "クボタスピアーズ船橋・東京ベイ", "short_name": "Ｓ東京ベイ", "area_text": "千葉県船橋市", "team_url": "https://league-one.jp/team/97"},
    {"league_one_team_id": "101", "division_code": "DIV1", "conference": "B", "team_name": "コベルコ神戸スティーラーズ", "short_name": "神戸Ｓ", "area_text": "兵庫県神戸市", "team_url": "https://league-one.jp/team/101"},
    {"league_one_team_id": "99", "division_code": "DIV1", "conference": "B", "team_name": "東京サントリーサンゴリアス", "short_name": "東京ＳＧ", "area_text": "東京都府中市", "team_url": "https://league-one.jp/team/99"},
    {"league_one_team_id": "104", "division_code": "DIV1", "conference": "B", "team_name": "トヨタヴェルブリッツ", "short_name": "トヨタＶ", "area_text": "愛知県豊田市", "team_url": "https://league-one.jp/team/104"},
    {"league_one_team_id": "105", "division_code": "DIV1", "conference": "B", "team_name": "三重ホンダヒート", "short_name": "三重Ｈ", "area_text": "三重県鈴鹿市", "team_url": "https://league-one.jp/team/105"},
    {"league_one_team_id": "108", "division_code": "DIV1", "conference": "B", "team_name": "リコーブラックラムズ東京", "short_name": "ＢＲ東京", "area_text": "東京都世田谷区", "team_url": "https://league-one.jp/team/108"},
    # D2
    {"league_one_team_id": "109", "division_code": "DIV2", "conference": None, "team_name": "NECグリーンロケッツ東葛", "short_name": "ＧＲ東葛", "area_text": "千葉県東葛地域", "team_url": "https://league-one.jp/team/109"},
    {"league_one_team_id": "110", "division_code": "DIV2", "conference": None, "team_name": "九州電力キューデンヴォルテクス", "short_name": "九州ＫＶ", "area_text": "福岡市", "team_url": "https://league-one.jp/team/110"},
    {"league_one_team_id": "111", "division_code": "DIV2", "conference": None, "team_name": "清水建設江東ブルーシャークス", "short_name": "江東ＢＳ", "area_text": "東京都江東区", "team_url": "https://league-one.jp/team/111"},
    {"league_one_team_id": "112", "division_code": "DIV2", "conference": None, "team_name": "豊田自動織機シャトルズ愛知", "short_name": "Ｓ愛知", "area_text": "愛知県", "team_url": "https://league-one.jp/team/112"},
    {"league_one_team_id": "113", "division_code": "DIV2", "conference": None, "team_name": "日本製鉄釜石シーウェイブス", "short_name": "釜石ＳＷ", "area_text": "岩手県釜石市", "team_url": "https://league-one.jp/team/113"},
    {"league_one_team_id": "114", "division_code": "DIV2", "conference": None, "team_name": "花園近鉄ライナーズ", "short_name": "花園Ｌ", "area_text": "大阪府東大阪市", "team_url": "https://league-one.jp/team/114"},
    {"league_one_team_id": "115", "division_code": "DIV2", "conference": None, "team_name": "日野レッドドルフィンズ", "short_name": "日野ＲＤ", "area_text": "東京都日野市", "team_url": "https://league-one.jp/team/115"},
    {"league_one_team_id": "116", "division_code": "DIV2", "conference": None, "team_name": "レッドハリケーンズ大阪", "short_name": "ＲＨ大阪", "area_text": "大阪府大阪市", "team_url": "https://league-one.jp/team/116"},
    # D3
    {"league_one_team_id": "117", "division_code": "DIV3", "conference": None, "team_name": "クリタウォーターガッシュ昭島", "short_name": "ＷＧ昭島", "area_text": "東京都昭島市", "team_url": "https://league-one.jp/team/117"},
    {"league_one_team_id": "118", "division_code": "DIV3", "conference": None, "team_name": "狭山セコムラガッツ", "short_name": "狭山RG", "area_text": "埼玉県狭山市", "team_url": "https://league-one.jp/team/118"},
    {"league_one_team_id": "119", "division_code": "DIV3", "conference": None, "team_name": "中国電力レッドレグリオンズ", "short_name": "中国ＲＲ", "area_text": "広島県", "team_url": "https://league-one.jp/team/119"},
    {"league_one_team_id": "120", "division_code": "DIV3", "conference": None, "team_name": "マツダスカイアクティブズ広島", "short_name": "ＳＡ広島", "area_text": "広島県", "team_url": "https://league-one.jp/team/120"},
    {"league_one_team_id": "121", "division_code": "DIV3", "conference": None, "team_name": "ヤクルトレビンズ戸田", "short_name": "Ｌ戸田", "area_text": "埼玉県戸田市", "team_url": "https://league-one.jp/team/121"},
    {"league_one_team_id": "122", "division_code": "DIV3", "conference": None, "team_name": "ルリーロ福岡", "short_name": "ＬＲ福岡", "area_text": "福岡県うきは市", "team_url": "https://league-one.jp/team/122"},
]


def utc_now() -> str:
    return datetime.now(timezone.utc).isoformat()


def clean(text: Optional[str]) -> str:
    if not text:
        return ""
    text = unicodedata.normalize("NFKC", text)
    text = text.replace("\xa0", " ")
    text = re.sub(r"\s+", " ", text)
    return text.strip()


def normalize_search_text(text: Optional[str]) -> str:
    text = unicodedata.normalize("NFKC", text or "")
    text = text.strip().upper()
    text = re.sub(r"[\s\u3000・･/\\\-－―‐‑–—（）()\[\]【】「」『』,，.．]", "", text)
    return text


def hash_bytes(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest()


def to_absolute_url(href: Optional[str]) -> str:
    href = clean(href)
    if not href:
        return ""
    return urljoin(BASE_URL, href)


class Fetcher:
    def __init__(self, delay: float = 0.5, timeout: int = 60, use_browser: bool = False) -> None:
        self.session = requests.Session()
        self.session.headers.update({"User-Agent": USER_AGENT})
        self.delay = delay
        self.timeout = timeout
        self.use_browser = use_browser
        self._playwright = None
        self._browser = None
        self._page = None

    def close(self) -> None:
        if self._page is not None:
            self._page.close()
            self._page = None
        if self._browser is not None:
            self._browser.close()
            self._browser = None
        if self._playwright is not None:
            self._playwright.stop()
            self._playwright = None

    def _ensure_browser(self):
        if not self.use_browser:
            return None
        if self._page is not None:
            return self._page
        try:
            from playwright.sync_api import sync_playwright
        except Exception as ex:
            raise RuntimeError(
                "Playwright is required for --use-browser. "
                "Install it with: pip install playwright && python -m playwright install chromium"
            ) from ex
        self._playwright = sync_playwright().start()
        self._browser = self._playwright.chromium.launch(headless=True)
        self._page = self._browser.new_page(user_agent=USER_AGENT)
        return self._page

    def get(self, url: str) -> tuple[int, bytes]:
        time.sleep(self.delay)
        res = self.session.get(url, timeout=self.timeout)
        return res.status_code, res.content

    def get_soup(self, url: str) -> tuple[int, bytes, BeautifulSoup]:
        if self.use_browser:
            page = self._ensure_browser()
            time.sleep(self.delay)
            response = page.goto(url, wait_until="domcontentloaded", timeout=self.timeout * 1000)
            page.wait_for_timeout(2500)
            # Annotate rendered image data before reading HTML.
            # Lazy-loaded images often keep a 1x1 GIF in src while the real image is in currentSrc.
            try:
                page.evaluate("""() => {
                    for (const img of document.images) {
                        img.setAttribute('data-rendered-current-src', img.currentSrc || img.src || '');
                        img.setAttribute('data-rendered-natural-width', String(img.naturalWidth || 0));
                        img.setAttribute('data-rendered-natural-height', String(img.naturalHeight || 0));
                    }
                }""")
            except Exception:
                pass
            html = page.content()
            status = response.status if response is not None else 200
            content = html.encode("utf-8", errors="ignore")
            soup = BeautifulSoup(html, "lxml")
            return status, content, soup

        status, content = self.get(url)
        soup = BeautifulSoup(content, "lxml")
        return status, content, soup


class DatabaseBuilder:
    def __init__(self, output: Path, cache_dir: Path, download_images: bool, use_browser: bool = False, timeout: int = 60, debug_dump_html: bool = False, only_team_id: str = "") -> None:
        self.output = output
        self.cache_dir = cache_dir
        self.download_images = download_images
        self.conn = sqlite3.connect(output)
        self.conn.execute("PRAGMA foreign_keys = ON")
        schema = Path(__file__).with_name("schema.sql").read_text(encoding="utf-8")
        self.conn.executescript(schema)
        self.fetcher = Fetcher(use_browser=use_browser, timeout=timeout)
        self.debug_dump_html = debug_dump_html
        self.only_team_id = only_team_id
        self.debug_dir = Path('debug_html')
        self.player_name_hints: dict[str, str] = {}
        self.failed_player_debug_count = 0
        self.season_id = 0
        self.division_ids: dict[tuple[str, Optional[str]], int] = {}
        self.team_ids: dict[str, int] = {}
        self.crawl_run_id: Optional[int] = None

    def close(self) -> None:
        self.conn.commit()
        self.conn.close()
        self.fetcher.close()

    def start_run(self, season: str) -> None:
        cur = self.conn.cursor()
        cur.execute(
            "INSERT INTO crawl_runs (started_at, target_season, status, note) VALUES (?, ?, ?, ?)",
            (utc_now(), season, "running", "Full data build from official League One pages."),
        )
        self.crawl_run_id = cur.lastrowid
        self.conn.commit()

    def finish_run(self, status: str, team_count: int, player_count: int, match_count: int, error_count: int) -> None:
        self.conn.execute(
            """UPDATE crawl_runs
               SET finished_at=?, status=?, total_team_count=?, total_player_count=?, total_match_count=?, error_count=?
               WHERE id=?""",
            (utc_now(), status, team_count, player_count, match_count, error_count, self.crawl_run_id),
        )
        self.conn.commit()

    def log_source(self, source_type: str, url: str, http_status: int, content: bytes,
                   parse_status: str, error: Optional[str] = None) -> None:
        self.conn.execute(
            """INSERT INTO source_pages
               (crawl_run_id, source_type, url, http_status, content_hash, fetched_at, parse_status, error_message)
               VALUES (?, ?, ?, ?, ?, ?, ?, ?)""",
            (
                self.crawl_run_id,
                source_type,
                url,
                http_status,
                hash_bytes(content) if content else None,
                utc_now(),
                parse_status,
                error,
            ),
        )

    def dump_debug_html(self, label: str, url: str, content: bytes, soup: BeautifulSoup) -> None:
        if not self.debug_dump_html:
            return
        self.debug_dir.mkdir(parents=True, exist_ok=True)
        safe = re.sub(r"[^0-9A-Za-z_-]+", "_", label)[:80]
        html_path = self.debug_dir / f"{safe}.html"
        links_path = self.debug_dir / f"{safe}_links.txt"
        html_path.write_bytes(content)
        hrefs = []
        for a in soup.select("a[href]"):
            hrefs.append(f"{clean(a.get_text(' '))}\t{a.get('href')}")
        links_path.write_text("\n".join(hrefs), encoding="utf-8")
        player_occurrences = len(re.findall(r"player/\d+", content.decode("utf-8", errors="ignore")))
        print(f"[DEBUG] dumped {html_path} / {links_path}; player/ occurrences={player_occurrences}; url={url}")

    def initialize_master(self, season_code: str = "2025-26", season_year: int = 2025) -> None:
        cur = self.conn.cursor()
        cur.execute(
            "INSERT OR IGNORE INTO seasons (season_code, season_year, source_url, fetched_at) VALUES (?, ?, ?, ?)",
            (season_code, season_year, TEAM_INDEX_URL, utc_now()),
        )
        cur.execute("SELECT id FROM seasons WHERE season_code=?", (season_code,))
        self.season_id = int(cur.fetchone()[0])

        divisions = [
            ("DIV1", "DIVISION 1", "A"),
            ("DIV1", "DIVISION 1", "B"),
            ("DIV2", "DIVISION 2", None),
            ("DIV3", "DIVISION 3", None),
        ]
        for code, name, conf in divisions:
            cur.execute(
                """INSERT OR IGNORE INTO divisions
                   (season_id, division_name, division_code, conference)
                   VALUES (?, ?, ?, ?)""",
                (self.season_id, name, code, conf),
            )
            cur.execute(
                "SELECT id FROM divisions WHERE season_id=? AND division_code=? AND (conference IS ? OR conference=?)",
                (self.season_id, code, conf, conf),
            )
            self.division_ids[(code, conf)] = int(cur.fetchone()[0])

        for team in TEAM_MASTER_2025_26:
            div_id = self.division_ids[(team["division_code"], team["conference"])]
            cur.execute(
                """INSERT OR IGNORE INTO teams
                   (league_one_team_id, season_id, division_id, team_name, short_name, area_text, team_url, raw_json, fetched_at)
                   VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?)""",
                (
                    team["league_one_team_id"],
                    self.season_id,
                    div_id,
                    team["team_name"],
                    team["short_name"],
                    team["area_text"],
                    team["team_url"],
                    json.dumps(team, ensure_ascii=False),
                    utc_now(),
                ),
            )
            cur.execute(
                "SELECT id FROM teams WHERE season_id=? AND league_one_team_id=?",
                (self.season_id, team["league_one_team_id"]),
            )
            self.team_ids[team["league_one_team_id"]] = int(cur.fetchone()[0])

        self.conn.commit()

    def fetch_all(self) -> None:
        errors = 0
        player_count = 0
        match_count = 0

        # Team index is fetched for source tracking; embedded master is used for stable division assignment.
        try:
            status, content, _ = self.fetcher.get_soup(TEAM_INDEX_URL)
            self.log_source("team_index", TEAM_INDEX_URL, status, content, "success" if status == 200 else "failed")
        except Exception as ex:
            errors += 1
            self.log_source("team_index", TEAM_INDEX_URL, 0, b"", "failed", repr(ex))

        teams_to_fetch = TEAM_MASTER_2025_26
        if self.only_team_id:
            teams_to_fetch = [t for t in TEAM_MASTER_2025_26 if t["league_one_team_id"] == self.only_team_id]
            if not teams_to_fetch:
                raise ValueError(f"Unknown --only-team-id: {self.only_team_id}")

        print(f"[INFO] Fetch target teams: {len(teams_to_fetch)}")
        for team in teams_to_fetch:
            try:
                team_players = self.fetch_team_and_players(team)
                player_count += team_players
                self.conn.commit()
                print(f"[OK] {team['team_name']}: {team_players} players")
            except Exception as ex:
                errors += 1
                print(f"[ERROR] {team['team_name']}: {ex!r}")

        try:
            match_count = self.fetch_schedules(2025)
        except Exception as ex:
            errors += 1
            print(f"[ERROR] schedule: {ex!r}")

        self.generate_school_candidates()
        self.write_app_settings()
        self.print_data_quality_summary()
        self.finish_run(
            "success" if errors == 0 else "partial",
            team_count=len(teams_to_fetch),
            player_count=player_count,
            match_count=match_count,
            error_count=errors,
        )

    def fetch_team_and_players(self, team: dict) -> int:
        status, content, soup = self.fetcher.get_soup(team["team_url"])
        if status != 200:
            self.log_source("team_detail", team["team_url"], status, content, "failed", f"HTTP {status}")
            return 0

        self.log_source("team_detail", team["team_url"], status, content, "success")
        self.dump_debug_html(f"team_{team['league_one_team_id']}", team["team_url"], content, soup)
        self.save_team_images(team, soup)

        player_links = self.extract_player_links(soup)
        if not player_links:
            self.log_source(
                "team_detail",
                team["team_url"],
                status,
                content,
                "partial",
                "No player links were found. Check official page HTML/link pattern.",
            )
        else:
            print(f"[INFO] {team['team_name']}: found {len(player_links)} player links")

        count = 0
        for player_url in player_links:
            if self.fetch_player(player_url, team):
                count += 1
        return count

    def extract_japanese_name_from_text(self, value: str) -> str:
        value = clean(value)
        if not value:
            return ""

        # Normalize spaces around Japanese middle dot and remove obvious noise.
        value = re.sub(r"\s*・\s*", "・", value)
        value = re.sub(r"\s+", " ", value)
        value = value.lstrip("#").strip()

        # Remove common labels and position/physical values to reduce noise.
        value = re.sub(r"\b(PR|HO|LO|FL|NO8|SH|SO|CTB|WTB|FB|UTB)\b", " ", value)
        value = re.sub(r"\b\d{2,3}\s*cm\b|\b\d{2,3}\s*kg\b", " ", value, flags=re.I)
        value = re.sub(r"カテゴリ[ABC]", " ", value)
        value = clean(value)
        value = re.sub(r"\s*・\s*", "・", value)

        # Reject generic page/UI labels.
        reject_tokens = [
            "リーグワン", "チーム一覧", "選手一覧", "選手名鑑", "NTT", "JAPAN RUGBY",
            "DIVISION", "MATCH", "SCHEDULE", "NEWS", "STANDINGS"
        ]
        upper = value.upper()
        if any(token in upper for token in reject_tokens):
            return ""

        # 1) Typical Japanese name: surname + space + given name.
        m = re.search(r"([一-龥々〆ヵヶぁ-んァ-ヴー]{1,12}\s+[一-龥々〆ヵヶぁ-んァ-ヴー]{1,14})", value)
        if m:
            return clean(m.group(1))

        # 2) Foreign/Katakana names with middle dots or long vowels.
        # Examples:
        #   レイ・タタフ
        #   ショーン・ロビンソン
        #   ラーボニ・ウォーレンーボスアヤコ
        m = re.search(r"([ァ-ヴー]+(?:・[ァ-ヴー]+)+)", value)
        if m:
            return clean(m.group(1))

        # 3) Official special notation, e.g. 中づる(雨冠に隹・鳥の順) 憲章.
        if re.search(r"[一-龥ぁ-んァ-ヴ]", value) and len(value) <= 40:
            return value

        return ""


    def extract_position_from_text(self, value: str) -> str:
        m = re.search(r"\b(PR|HO|LO|FL|NO8|SH|SO|CTB|WTB|FB|UTB)\b", value or "")
        return m.group(1) if m else ""


    def extract_player_links(self, soup: BeautifulSoup) -> list[str]:
        """
        Extract player detail links from a team page.

        Supports:
            /player/483707
            player/483707
            https://league-one.jp/player/483707
            JSON/escaped HTML forms containing player/483707
        """
        links = []

        for a in soup.select("a[href]"):
            href = clean(a.get("href") or "")
            if re.search(r"(?:^|/)player/\d+", href):
                url = to_absolute_url(href)
                links.append(url)
                hint = clean(a.get_text(" "))
                if hint:
                    self.player_name_hints[url] = hint

        raw_html = str(soup)
        for player_id in re.findall(r"player/(\d+)", raw_html):
            links.append(f"https://league-one.jp/player/{player_id}")

        for player_id in re.findall(r"player\\/(\d+)", raw_html):
            links.append(f"https://league-one.jp/player/{player_id}")

        seen = set()
        uniq = []
        for link in links:
            link = link.split("#")[0]
            if link not in seen:
                uniq.append(link)
                seen.add(link)
        return uniq

    def save_team_images(self, team: dict, soup: BeautifulSoup) -> None:
        # Store the actual team logo, not generic og:image/menu logos.
        img_url = self.find_team_logo(soup, team["league_one_team_id"])
        if img_url:
            asset_id = self.insert_asset(
                "team_logo",
                "team",
                self.team_ids[team["league_one_team_id"]],
                img_url,
                local_subdir=f"teams/{team['league_one_team_id']}",
            )
            self.conn.execute(
                "UPDATE teams SET logo_asset_id=? WHERE id=?",
                (asset_id, self.team_ids[team["league_one_team_id"]]),
            )


    def find_relevant_image(self, soup: BeautifulSoup) -> str:
        # Best effort for team page: prefer og:image if present.
        # Do not use this for player photos because og:image may be a generic League One/NTT image.
        meta = soup.find("meta", attrs={"property": "og:image"})
        if meta and meta.get("content"):
            return to_absolute_url(meta.get("content"))

        for img in soup.select("img[src]"):
            src = to_absolute_url(img.get("src"))
            if src and "league-one.jp" in src:
                return src
        return ""

    def get_image_urls_from_tag(self, tag) -> list[str]:
        urls = []
        for attr in ["data-rendered-current-src", "src", "data-src", "data-original", "data-lazy-src", "data-srcset"]:
            value = tag.get(attr)
            if not value:
                continue
            value = str(value).strip()
            if attr.endswith("srcset"):
                for part in value.split(","):
                    url = part.strip().split(" ")[0]
                    if url:
                        urls.append(to_absolute_url(url))
            else:
                urls.append(to_absolute_url(value))

        srcset = tag.get("srcset")
        if srcset:
            for part in str(srcset).split(","):
                url = part.strip().split(" ")[0]
                if url:
                    urls.append(to_absolute_url(url))

        result = []
        seen = set()
        for url in urls:
            if not url or url.startswith("data:"):
                continue
            if url not in seen:
                result.append(url)
                seen.add(url)
        return result

    def find_player_photo(self, soup: BeautifulSoup, player_id: str = "", player_name: str = "") -> str:
        """
        Pick the actual player photo instead of common page images.

        The old logic used og:image first. On League One pages, og:image can be
        a common League One/NTT image, which caused every player photo to become
        the same image. This method ignores common OGP/logo/sponsor images and
        scores image candidates that look player-specific.
        """
        og_images = set()
        for meta in soup.find_all("meta"):
            prop = (meta.get("property") or meta.get("name") or "").lower()
            if prop in {"og:image", "twitter:image"} and meta.get("content"):
                og_images.add(to_absolute_url(meta.get("content")))

        candidates = []
        for tag in soup.select("img, source"):
            urls = self.get_image_urls_from_tag(tag)
            if not urls:
                continue

            attrs = " ".join([
                str(tag.get("alt") or ""),
                str(tag.get("class") or ""),
                str(tag.get("id") or ""),
                str(tag.get("src") or ""),
                str(tag.get("data-src") or ""),
                str(tag.get("srcset") or ""),
            ])
            attrs_norm = unicodedata.normalize("NFKC", attrs)
            attrs_lower = attrs_norm.lower()
            natural_width = int(tag.get("data-rendered-natural-width") or 0)
            natural_height = int(tag.get("data-rendered-natural-height") or 0)

            for url in urls:
                url_lower = url.lower()

                # Ignore obvious placeholder/lazy-loading images.
                if natural_width and natural_height and natural_width <= 2 and natural_height <= 2:
                    continue
                if "placeholder" in url_lower or "blank" in url_lower or "spacer" in url_lower:
                    continue

                score = 0

                if self.is_via_cloudflare_asset(url):
                    # Official player portraits are currently served from assets.via-cloudflare.site.
                    score += 220
                if player_id and player_id in url:
                    score += 120
                if natural_width >= 200 and natural_height >= 200:
                    score += 40
                elif natural_width >= 80 and natural_height >= 80:
                    score += 20
                if "player" in url_lower or "players" in url_lower:
                    score += 90
                if "player" in attrs_lower or "players" in attrs_lower:
                    score += 70
                if "member" in url_lower or "member" in attrs_lower:
                    score += 35
                if "profile" in url_lower or "profile" in attrs_lower:
                    score += 35
                if "photo" in url_lower or "photo" in attrs_lower:
                    score += 25
                if player_name and player_name.replace(" ", "") in attrs_norm.replace(" ", ""):
                    score += 60

                negative_tokens = [
                    "ogp", "logo", "sponsor", "partner", "ntt", "leagueone",
                    "league-one", "common", "header", "footer", "banner",
                    "bnr", "icon", "mark", "emblem", "sns", "twitter", "facebook"
                ]
                if any(token in url_lower for token in negative_tokens):
                    score -= 80
                if any(token in attrs_lower for token in negative_tokens):
                    score -= 60
                if url in og_images:
                    score -= 120

                candidates.append((score, url, attrs_norm[:200]))

        candidates.sort(key=lambda x: x[0], reverse=True)

        if self.debug_dump_html and player_id:
            self.debug_dir.mkdir(parents=True, exist_ok=True)
            out = self.debug_dir / f"player_{player_id}_image_candidates.txt"
            lines = [f"{score}\t{url}\t{attrs}" for score, url, attrs in candidates[:30]]
            out.write_text("\n".join(lines), encoding="utf-8")

        for score, url, _ in candidates:
            if score > 0:
                return url

        return ""


    def is_via_cloudflare_asset(self, url: str) -> bool:
        try:
            return (urlparse(url).netloc or "").lower() == "assets.via-cloudflare.site"
        except Exception:
            return False

    def find_team_logo(self, soup: BeautifulSoup, league_one_team_id: str = "") -> str:
        """
        Pick the team logo from the team detail header, not generic og:image or menu logos.

        Example for team/110:
        https://league-one.s3.ap-northeast-1.amazonaws.com/image/team_info/11196_200x200_670e778dd09b0.png
        """
        candidates = []

        def add_candidate(score: int, tag, reason: str) -> None:
            if not tag:
                return
            urls = self.get_image_urls_from_tag(tag) if hasattr(self, "get_image_urls_from_tag") else [to_absolute_url(tag.get("src"))]
            for url in urls:
                if not url:
                    continue
                lower = url.lower()
                attrs = " ".join([
                    str(tag.get("src") or ""),
                    str(tag.get("data-src") or ""),
                    str(tag.get("data-rendered-current-src") or ""),
                    str(tag.get("srcset") or ""),
                    str(tag.get("alt") or ""),
                    str(tag.get("class") or ""),
                ])
                s = score
                if "image/team_info/" in lower and "200x200" in lower:
                    s += 120
                if "testing_image/team_info/" in lower:
                    s -= 120
                if "image/menu/" in lower:
                    s -= 100
                if "site_info" in lower or "logo-header-leagueone" in lower or "ogp" in lower:
                    s -= 200
                candidates.append((s, url, reason + " " + attrs[:100]))

        detail = soup.select_one(".c-team-detail-ttl")
        if detail:
            for img in detail.select("figure.emblem img, .emblem img, img"):
                add_candidate(300, img, "team-detail-title")

        for img in soup.select("img, source"):
            urls = self.get_image_urls_from_tag(img) if hasattr(self, "get_image_urls_from_tag") else []
            for url in urls:
                lower = url.lower()
                if "image/team_info/" in lower and "200x200" in lower and "testing_image" not in lower:
                    candidates.append((180, url, "team-info-fallback"))

        candidates.sort(key=lambda x: x[0], reverse=True)

        if self.debug_dump_html and league_one_team_id:
            self.debug_dir.mkdir(parents=True, exist_ok=True)
            out = self.debug_dir / f"team_{league_one_team_id}_logo_candidates.txt"
            out.write_text("\\n".join(f"{s}\\t{u}\\t{a}" for s, u, a in candidates[:50]), encoding="utf-8")

        seen = set()
        for score, url, _ in candidates:
            if score > 0 and url not in seen:
                seen.add(url)
                return url

        return ""


    def fetch_player(self, player_url: str, team: dict) -> bool:
        status, content, soup = self.fetcher.get_soup(player_url)
        if status != 200:
            self.log_source("player_detail", player_url, status, content, "failed", f"HTTP {status}")
            return False
        self.log_source("player_detail", player_url, status, content, "success")

        data = self.parse_player_page(player_url, soup)
        if self.debug_dump_html and (not data.get('name_ja')) and self.failed_player_debug_count < 5:
            self.dump_debug_html(f"player_failed_{data.get('league_one_player_id') or 'unknown'}", player_url, content, soup)
            self.failed_player_debug_count += 1
        if not data["league_one_player_id"] or not data["name_ja"]:
            print(f"[WARN] Could not parse player id/name: {player_url} id={data.get('league_one_player_id')} name={data.get('name_ja')} hint={self.player_name_hints.get(player_url, '')}")
            self.log_source("player_detail", player_url, status, content, "partial", "Could not parse player id or name.")
            return False

        cur = self.conn.cursor()
        cur.execute(
            """INSERT OR IGNORE INTO players
               (league_one_player_id, name_ja, name_en, birth_date, profile_url, raw_json, created_at, updated_at)
               VALUES (?, ?, ?, ?, ?, ?, ?, ?)""",
            (
                data["league_one_player_id"],
                data["name_ja"],
                data["name_en"],
                data["birth_date"],
                player_url,
                json.dumps(data, ensure_ascii=False),
                utc_now(),
                utc_now(),
            ),
        )
        cur.execute(
            """UPDATE players
               SET name_ja=?, name_en=?, birth_date=?, profile_url=?, raw_json=?, updated_at=?
               WHERE league_one_player_id=?""",
            (
                data["name_ja"],
                data["name_en"],
                data["birth_date"],
                player_url,
                json.dumps(data, ensure_ascii=False),
                utc_now(),
                data["league_one_player_id"],
            ),
        )
        cur.execute("SELECT id FROM players WHERE league_one_player_id=?", (data["league_one_player_id"],))
        player_id = int(cur.fetchone()[0])

        photo_asset_id = None
        if data.get("photo_url"):
            photo_asset_id = self.insert_asset(
                "player_photo",
                "player",
                player_id,
                data["photo_url"],
                local_subdir=f"players/{data['league_one_player_id']}",
            )

        team_id = self.team_ids[team["league_one_team_id"]]
        div_id = self.division_ids[(team["division_code"], team["conference"])]
        age_calc = self.calculate_age(data["birth_date"])
        school_text = data.get("school_team_history_text") or ""
        cur.execute(
            """INSERT OR REPLACE INTO player_season_registrations
               (id, player_id, season_id, team_id, division_id, position_code, position_name, height_cm, weight_kg,
                age_displayed, age_calculated, registration_category, league_one_caps,
                school_team_history_text, school_team_history_search_text, photo_asset_id, raw_json, fetched_at)
               VALUES (
                 (SELECT id FROM player_season_registrations WHERE player_id=? AND season_id=?),
                 ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?
               )""",
            (
                player_id, self.season_id,
                player_id, self.season_id, team_id, div_id,
                data.get("position_code") or "",
                data.get("position_name") or "",
                data.get("height_cm"),
                data.get("weight_kg"),
                data.get("age_displayed"),
                age_calc,
                data.get("registration_category") or "",
                data.get("league_one_caps"),
                school_text,
                normalize_search_text(school_text),
                photo_asset_id,
                json.dumps(data, ensure_ascii=False),
                utc_now(),
            ),
        )

        self.insert_school_history(player_id, school_text)
        self.insert_player_stats(player_id, data)
        return True

    def parse_player_page(self, player_url: str, soup: BeautifulSoup) -> dict:
        text = soup.get_text("\n")
        lines = [clean(line) for line in text.splitlines()]
        lines = [line for line in lines if line]

        player_id_match = re.search(r"/player/(\d+)", player_url)
        player_id = player_id_match.group(1) if player_id_match else ""

        name_ja = ""
        position_code = ""

        # 1) Official title often contains: 徳永 一斗（2025-26） | ...
        title = clean(soup.find("title").get_text(" ")) if soup.find("title") else ""
        m = re.search(r"^(.+?)（20\d{2}-\d{2}）", title)
        if m:
            name_ja = clean(m.group(1))

        # 2) og:title fallback.
        if not name_ja:
            og = soup.find("meta", attrs={"property": "og:title"})
            og_title = clean(og.get("content")) if og and og.get("content") else ""
            m = re.search(r"^(.+?)（20\d{2}-\d{2}）", og_title)
            if m:
                name_ja = clean(m.group(1))

        # 3) h1/h2 and body line fallback. Official text normally has: # 徳永 一斗 (PR)
        candidates = []
        for h in soup.find_all(["h1", "h2", "h3"]):
            candidates.append(clean(h.get_text(" ")))
        candidates.extend(lines)

        for candidate in candidates:
            # Prefer line with position code.
            m = re.search(r"#?\s*(.+?)\s*[（(]\s*(PR|HO|LO|FL|NO8|SH|SO|CTB|WTB|FB|UTB)\s*[)）]", candidate)
            if m:
                n = self.extract_japanese_name_from_text(m.group(1))
                if n:
                    name_ja = name_ja or n
                    position_code = position_code or m.group(2)
                    break

        # 4) Use team-page anchor text as fallback if player page text is unexpectedly generic.
        hint = self.player_name_hints.get(player_url, "")
        if hint:
            if not name_ja:
                name_ja = self.extract_japanese_name_from_text(hint)
                if not name_ja:
                    # The team-page anchor text is official display text.
                    # Accept it as fallback for foreign names/special glyph notes.
                    fallback_hint = clean(re.sub(r"\s*・\s*", "・", hint))
                    if fallback_hint and len(fallback_hint) <= 60:
                        name_ja = fallback_hint
            if not position_code:
                position_code = self.extract_position_from_text(hint)

        # 5) Breadcrumb fallback: previous line before season is often the player name.
        if not name_ja:
            for i, line in enumerate(lines):
                if re.fullmatch(r"20\d{2}-\d{2}シーズン", line) and i > 0:
                    possible = self.extract_japanese_name_from_text(lines[i - 1])
                    if possible:
                        name_ja = possible
                        break

        # English name: Latin-only line near player header.
        name_en = ""
        for line in lines:
            if re.fullmatch(r"[A-Za-z][A-Za-z .'-]+", line) and "JAPAN" not in line.upper() and "RUGBY" not in line.upper():
                name_en = line
                break

        def line_after_label(label_pattern: str) -> str:
            """
            Extract value for a label from rendered page text.

            Handles:
                身長／体重：176cm / 113kg
                身長/体重: 176cm / 113kg
                身長／体重
                176cm / 113kg
            """
            pat = re.compile(label_pattern)
            for i, line in enumerate(lines):
                if pat.search(line):
                    parts = re.split(r"[:：]", line, maxsplit=1)
                    if len(parts) == 2 and clean(parts[1]):
                        return clean(parts[1])

                    removed = clean(pat.sub("", line))
                    removed = clean(re.sub(r"^[：:\-ー]+", "", removed))
                    if removed:
                        return removed

                    # Label-only line. Use next non-empty line as value.
                    for next_line in lines[i + 1:i + 4]:
                        if next_line and not re.search(r"身長|体重|生年月日|出身校|チーム歴|登録区分|キャップ", next_line):
                            return clean(next_line)

            # fallback from whole text, including multi-byte slash and whitespace
            m = re.search(label_pattern + r"\s*[：:]\s*([^\n]+)", text)
            return clean(m.group(1)) if m else ""

        height_cm = weight_kg = None
        hw_text = line_after_label(r"身長\s*[／/]\s*体重")
        m = re.search(r"(\d{2,3})\s*cm\s*[／/]\s*(\d{2,3})\s*kg", hw_text, re.I)
        if not m:
            m = re.search(r"身長\s*[／/]\s*体重\s*[：:]?\s*(\d{2,3})\s*cm\s*[／/]\s*(\d{2,3})\s*kg", text, re.I)
        if m:
            height_cm = int(m.group(1))
            weight_kg = int(m.group(2))

        birth_date = ""
        age_displayed = None
        birth_text = line_after_label(r"生年月日")
        m = re.search(r"(\d{4})[./年](\d{1,2})[./月](\d{1,2})日?(?:\s*[（(]\s*(\d+)\s*歳\s*[)）])?", birth_text)
        if not m:
            m = re.search(r"生年月日\s*[：:]?\s*(\d{4})[./年](\d{1,2})[./月](\d{1,2})日?(?:\s*[（(]\s*(\d+)\s*歳\s*[)）])?", text)
        if m:
            birth_date = f"{int(m.group(1)):04d}-{int(m.group(2)):02d}-{int(m.group(3)):02d}"
            if m.group(4):
                age_displayed = int(m.group(4))

        school = line_after_label(r"出身校・チーム歴")
        registration = line_after_label(r"登録区分")
        caps_text = line_after_label(r"リーグワンキャップ数")
        caps_match = re.search(r"\d+", caps_text or "")
        caps = int(caps_match.group(0)) if caps_match else None

        photo_url = self.find_player_photo(soup, player_id, name_ja)
        stats = self.parse_player_stats(soup)

        return {
            "league_one_player_id": player_id,
            "name_ja": name_ja,
            "name_en": name_en,
            "position_code": position_code,
            "position_name": "",
            "height_cm": height_cm,
            "weight_kg": weight_kg,
            "birth_date": birth_date,
            "age_displayed": age_displayed,
            "school_team_history_text": school,
            "registration_category": registration,
            "league_one_caps": caps,
            "photo_url": photo_url,
            "profile_url": player_url,
            "player_stats": stats,
        }


    def parse_player_stats(self, soup: BeautifulSoup) -> list[dict]:
        # Best effort: parse normal HTML tables if present.
        stats = []
        for table in soup.select("table"):
            headers = [clean(th.get_text(" ")) for th in table.select("th")]
            if not headers or not any(h in headers for h in ["開催日", "対戦チーム", "スコア"]):
                continue
            for tr in table.select("tbody tr"):
                cells = [clean(td.get_text(" ")) for td in tr.select("td")]
                if cells:
                    stats.append({"cells": cells, "headers": headers})
        return stats

    def insert_player_stats(self, player_id: int, data: dict) -> None:
        for stat in data.get("player_stats", []):
            self.conn.execute(
                """INSERT INTO player_match_stats
                   (player_id, season_id, raw_json)
                   VALUES (?, ?, ?)""",
                (player_id, self.season_id, json.dumps(stat, ensure_ascii=False)),
            )

    def insert_school_history(self, player_id: int, school_text: str) -> None:
        if not school_text:
            return
        # Split heuristically. Official raw text remains the primary display source.
        parts = [p for p in re.split(r"\s+", clean(school_text)) if p]
        for order, part in enumerate(parts, start=1):
            if "高校" in part or "工業" in part or "実業" in part:
                item_type = "high_school"
            elif "大学" in part:
                item_type = "university"
            else:
                item_type = "unknown"
            self.conn.execute(
                """INSERT INTO player_school_team_histories
                   (player_id, season_id, raw_text, item_order, item_name, normalized_name, item_type, confidence)
                   VALUES (?, ?, ?, ?, ?, ?, ?, ?)""",
                (player_id, self.season_id, school_text, order, part, normalize_search_text(part), item_type, 0.7),
            )

    def get_image_info(self, data: bytes) -> tuple[str, int, int]:
        """
        Return (extension, width, height) for common image formats.
        Unknown or unsupported formats return ("", 0, 0).
        """
        head = data[:64]

        if data.startswith(b"GIF87a") or data.startswith(b"GIF89a"):
            if len(data) >= 10:
                w = int.from_bytes(data[6:8], "little")
                h = int.from_bytes(data[8:10], "little")
                return ".gif", w, h
            return ".gif", 0, 0

        if data.startswith(b"\x89PNG\r\n\x1a\n"):
            if len(data) >= 24:
                w = int.from_bytes(data[16:20], "big")
                h = int.from_bytes(data[20:24], "big")
                return ".png", w, h
            return ".png", 0, 0

        if data.startswith(b"RIFF") and data[8:12] == b"WEBP":
            return ".webp", 0, 0

        # AVIF/HEIC family usually starts with: ....ftypavif / ....ftypheic
        if len(data) >= 12 and data[4:8] == b"ftyp":
            brand = data[8:12].lower()
            if brand in {b"avif", b"avis"}:
                return ".avif", 0, 0
            if brand in {b"heic", b"heix", b"hevc", b"hevx", b"mif1", b"msf1"}:
                return ".heic", 0, 0

        stripped = data.lstrip()[:100].lower()
        if stripped.startswith(b"<svg") or b"<svg" in stripped[:50]:
            return ".svg", 0, 0

        if data.startswith(b"\xff\xd8"):
            # JPEG size parser.
            i = 2
            while i + 9 < len(data):
                if data[i] != 0xFF:
                    i += 1
                    continue
                marker = data[i + 1]
                i += 2
                if marker in (0xD8, 0xD9):
                    continue
                if i + 2 > len(data):
                    break
                segment_len = int.from_bytes(data[i:i+2], "big")
                if segment_len < 2 or i + segment_len > len(data):
                    break
                if marker in (0xC0, 0xC1, 0xC2, 0xC3, 0xC5, 0xC6, 0xC7, 0xC9, 0xCA, 0xCB, 0xCD, 0xCE, 0xCF):
                    h = int.from_bytes(data[i+3:i+5], "big")
                    w = int.from_bytes(data[i+5:i+7], "big")
                    return ".jpg", w, h
                i += segment_len
            return ".jpg", 0, 0

        return "", 0, 0


    def is_valid_downloaded_image(self, data: bytes, asset_type: str) -> tuple[bool, str]:
        ext, w, h = self.get_image_info(data)
        if not ext:
            return False, "unsupported image content"

        # Reject known 1x1 tracking/placeholder images.
        if w and h and w <= 2 and h <= 2:
            return False, f"tiny placeholder image {w}x{h}"

        # For player photos, known dimensions are useful but not always present
        # for WebP/AVIF. Reject only when dimensions are known and too small.
        if asset_type == "player_photo" and w and h and (w < 80 or h < 80):
            return False, f"too small for player photo {w}x{h}"

        return True, ext


    def download_image_bytes(self, url: str) -> tuple[int, bytes, str]:
        """
        Download image bytes with headers that official asset/CDN endpoints expect.

        Some League One images are served by Cloudflare/S3 and may return HTML/XML
        unless Referer/Accept headers look like a browser request.
        """
        headers = {
            "User-Agent": USER_AGENT,
            "Referer": "https://league-one.jp/",
            "Accept": "image/jpeg,image/png,image/webp,image/gif,image/*,*/*;q=0.8",
            "Accept-Language": "ja,en-US;q=0.9,en;q=0.8",
            "Cache-Control": "no-cache",
        }
        time.sleep(self.fetcher.delay)
        res = self.fetcher.session.get(url, headers=headers, timeout=self.fetcher.timeout, allow_redirects=True)
        content_type = res.headers.get("Content-Type", "")
        return res.status_code, res.content, content_type


    def insert_asset(self, asset_type: str, related_type: str, related_id: int, source_url: str, local_subdir: str) -> int:
        local_path = ""
        file_name = ""
        content_hash = None
        is_available = 0

        if self.download_images and source_url:
            try:
                status, content, content_type = self.download_image_bytes(source_url)
                if status == 200 and content:
                    ok, ext_or_reason = self.is_valid_downloaded_image(content, asset_type)
                    if not ok:
                        if self.debug_dump_html:
                            preview = content[:32].hex(" ")
                            print(f"[DEBUG] skip image for {asset_type}: {ext_or_reason}; status={status}; content_type={content_type}; first32={preview}; url={source_url}")
                        raise ValueError(ext_or_reason)

                    suffix = ext_or_reason or Path(urlparse(source_url).path).suffix or ".jpg"
                    content_hash = hash_bytes(content)
                    file_name = f"{asset_type}_{content_hash[:12]}{suffix}"
                    out_dir = self.cache_dir / local_subdir
                    out_dir.mkdir(parents=True, exist_ok=True)
                    out_file = out_dir / file_name
                    out_file.write_bytes(content)
                    local_path = str(out_file)
                    is_available = 1
            except Exception:
                pass

        cur = self.conn.cursor()
        cur.execute(
            """INSERT INTO asset_files
               (asset_type, related_type, related_id, source_url, local_path, file_name, content_hash,
                downloaded_at, last_checked_at, is_available)
               VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?)""",
            (
                asset_type, related_type, related_id, source_url, local_path, file_name, content_hash,
                utc_now() if is_available else None, utc_now(), is_available,
            ),
        )
        return int(cur.lastrowid)

    def calculate_age(self, birth: str) -> Optional[int]:
        if not birth:
            return None
        try:
            y, m, d = [int(x) for x in birth.split("-")]
            today = date.today()
            return today.year - y - ((today.month, today.day) < (m, d))
        except Exception:
            return None

    def print_data_quality_summary(self) -> None:
        cur = self.conn.cursor()
        total_players = cur.execute("SELECT COUNT(*) FROM players").fetchone()[0]
        total_regs = cur.execute("SELECT COUNT(*) FROM player_season_registrations").fetchone()[0]
        with_height = cur.execute("SELECT COUNT(*) FROM player_season_registrations WHERE height_cm IS NOT NULL").fetchone()[0]
        with_weight = cur.execute("SELECT COUNT(*) FROM player_season_registrations WHERE weight_kg IS NOT NULL").fetchone()[0]
        with_birth = cur.execute("""
            SELECT COUNT(*)
            FROM players
            WHERE birth_date IS NOT NULL AND birth_date <> ''
        """).fetchone()[0]
        with_school = cur.execute("""
            SELECT COUNT(*)
            FROM player_season_registrations
            WHERE school_team_history_text IS NOT NULL AND school_team_history_text <> ''
        """).fetchone()[0]
        with_category = cur.execute("""
            SELECT COUNT(*)
            FROM player_season_registrations
            WHERE registration_category IS NOT NULL AND registration_category <> ''
        """).fetchone()[0]
        with_caps = cur.execute("SELECT COUNT(*) FROM player_season_registrations WHERE league_one_caps IS NOT NULL").fetchone()[0]
        with_photo = cur.execute("""
            SELECT COUNT(*)
            FROM player_season_registrations r
            JOIN asset_files a ON a.id = r.photo_asset_id
            WHERE a.is_available = 1
        """).fetchone()[0]
        with_team_logo = cur.execute("""
            SELECT COUNT(*)
            FROM teams t
            JOIN asset_files a ON a.id = t.logo_asset_id
            WHERE a.is_available = 1
        """).fetchone()[0]

        print("[SUMMARY] Data quality")
        print(f"[SUMMARY] players={total_players}, registrations={total_regs}")
        print(f"[SUMMARY] height={with_height}, weight={with_weight}, birth_date={with_birth}, school_history={with_school}")
        print(f"[SUMMARY] registration_category={with_category}, caps={with_caps}")
        print(f"[SUMMARY] player_photos={with_photo}, team_logos={with_team_logo}")


    def fetch_schedules(self, year: int) -> int:
        # Inserts raw schedule source rows for now. Codex/app can refine parser.
        count = 0
        for division in ["div1", "div2", "div3"]:
            url = SCHEDULE_TABLE_URL.format(year=year, division=division)
            try:
                status, content, soup = self.fetcher.get_soup(url)
                self.log_source("schedule", url, status, content, "success" if status == 200 else "failed")
                if status == 200:
                    # Minimal extraction from schedule table rows.
                    rows = soup.select("table.schedule-table tbody tr")
                    count += len(rows)
                    for row in rows:
                        raw = clean(row.get_text(" "))
                        self.conn.execute(
                            """INSERT INTO matches
                               (season_id, match_category, source_url, raw_json, fetched_at)
                               VALUES (?, ?, ?, ?, ?)""",
                            (self.season_id, self.classify_match_category(raw), url, json.dumps({"text": raw}, ensure_ascii=False), utc_now()),
                        )
            except Exception as ex:
                self.log_source("schedule", url, 0, b"", "failed", repr(ex))

        url = SCHEDULE_URL.format(year=year)
        try:
            status, content, soup = self.fetcher.get_soup(url)
            self.log_source("schedule", url, status, content, "success" if status == 200 else "failed")
        except Exception as ex:
            self.log_source("schedule", url, 0, b"", "failed", repr(ex))
        self.conn.commit()
        return count

    def classify_match_category(self, raw: str) -> str:
        if "入替" in raw:
            return "Replacement"
        if re.search(r"PO\d|準々決勝|準決勝|決勝|3位決定", raw):
            return "Playoff"
        if "順位決定" in raw:
            return "Placement"
        if raw:
            return "RegularSeason"
        return "Unknown"

    def generate_school_candidates(self) -> None:
        cur = self.conn.cursor()
        rows = cur.execute(
            "SELECT school_team_history_text FROM player_season_registrations WHERE school_team_history_text IS NOT NULL AND school_team_history_text <> ''"
        ).fetchall()
        counts: dict[str, int] = {}
        types: dict[str, str] = {}
        for (raw,) in rows:
            for part in re.split(r"\s+", clean(raw)):
                if not part:
                    continue
                candidates = [part]
                if part.endswith("高校"):
                    candidates.append(part[:-2])
                for candidate in candidates:
                    norm = normalize_search_text(candidate)
                    if not norm:
                        continue
                    counts[candidate] = counts.get(candidate, 0) + 1
                    if "高校" in candidate or "工業" in candidate or "実業" in candidate:
                        types[candidate] = "high_school"
                    elif "大学" in candidate:
                        types[candidate] = "university"
                    else:
                        types[candidate] = "unknown"

        for candidate, count in counts.items():
            self.conn.execute(
                """INSERT OR REPLACE INTO school_candidates
                   (season_id, candidate_text, normalized_text, candidate_type, player_count, confidence, last_updated_at)
                   VALUES (?, ?, ?, ?, ?, ?, ?)""",
                (self.season_id, candidate, normalize_search_text(candidate), types[candidate], count, 0.7, utc_now()),
            )
        self.conn.commit()

    def write_app_settings(self) -> None:
        settings = {
            "database_schema_version": "1",
            "builder_version": "1.0.13",
            "builder_version": "1.0.1",
            "database_scope": "full_builder_output",
            "target_repository": "https://github.com/tksapec/OneRugbyNavi2.git",
            "official_team_index_url": TEAM_INDEX_URL,
            "last_full_build_at": utc_now(),
        }
        for k, v in settings.items():
            self.conn.execute(
                "INSERT OR REPLACE INTO app_settings (key, value, updated_at) VALUES (?, ?, ?)",
                (k, v, utc_now()),
            )
        self.conn.commit()


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--output", default="leagueone_full.db", help="Output SQLite DB path.")
    parser.add_argument("--cache-dir", default="cache", help="Image/cache output directory.")
    parser.add_argument("--download-images", action="store_true", help="Download player photos and team images.")
    parser.add_argument("--use-browser", action="store_true", help="Use Playwright/Chromium to render pages before parsing.")
    parser.add_argument("--season-code", default="2025-26")
    parser.add_argument("--season-year", type=int, default=2025)
    parser.add_argument("--timeout", type=int, default=60, help="HTTP/browser timeout seconds.")
    parser.add_argument("--debug-dump-html", action="store_true", help="Dump team/player HTML snapshots and link lists to debug_html/.")
    parser.add_argument("--only-team-id", default="", help="Debug option: fetch only one league_one_team_id, e.g. 110.")
    args = parser.parse_args()

    output = Path(args.output)
    if output.exists():
        output.unlink()

    builder = DatabaseBuilder(output, Path(args.cache_dir), args.download_images, use_browser=args.use_browser, timeout=args.timeout, debug_dump_html=args.debug_dump_html, only_team_id=args.only_team_id)
    try:
        builder.start_run(args.season_code)
        builder.initialize_master(args.season_code, args.season_year)
        builder.fetch_all()
        print(f"Created: {output.resolve()}")
        return 0
    finally:
        builder.close()


if __name__ == "__main__":
    raise SystemExit(main())

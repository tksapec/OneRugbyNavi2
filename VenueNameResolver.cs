using System.Collections.Generic;
using System.Diagnostics;

namespace OneRugbyNavi2
{
    public static class VenueNameResolver
    {
        private static readonly HashSet<string> LoggedUnmappedKeys = new();

        private static readonly Dictionary<string, string> OfficialNames = new()
        {
            ["北海道|プレド"] = "大和ハウス プレミストドーム",
            ["佐賀|駅スタ"] = "駅前不動産スタジアム",
            ["愛知|パロ瑞穂ラ"] = "パロマ瑞穂ラグビー場",
            ["愛知|刈谷"] = "ウェーブスタジアム刈谷",
            ["愛知|豊田ス"] = "豊田スタジアム",
            ["岩手|いわスタ"] = "いわぎんスタジアム",
            ["岩手|釜石復興"] = "釜石鵜住居復興スタジアム",
            ["岩手|サンスタ"] = "サンディスクスタジアムきたかみ",
            ["岐阜|ヒマスタ"] = "ヒマラヤスタジアム岐阜",
            ["埼玉|川越陸"] = "川越運動公園陸上競技場",
            ["埼玉|熊谷"] = "熊谷スポーツ文化公園ラグビー場",
            ["三重|鈴鹿"] = "三重交通G スポーツの杜 鈴鹿",
            ["宮城|ユアスタ"] = "ユアテックスタジアム仙台",
            ["大阪|ヤンマーS"] = "ヤンマースタジアム長居",
            ["大阪|ハナサカ"] = "YANMAR HANASAKA STADIUM",
            ["大阪|花園"] = "東大阪市花園ラグビー場",
            ["大分|クラド"] = "クラサスドーム大分（旧レゾナックドーム）",
            ["宮崎|クロスタ"] = "KUROKIRI STADIUM",
            ["京都|たけびし"] = "たけびしスタジアム京都",
            ["山口|みらスタ"] = "維新みらいふスタジアム",
            ["山梨|JITス"] = "ＪＩＴ リサイクルインク スタジアム",
            ["広島|バルコムR"] = "Balcom BMW Rugby Stadium",
            ["広島|バルコムS"] = "Balcom BMW Stadium",
            ["広島|坂G"] = "中国電力坂スポーツ施設",
            ["広島|福通Rス"] = "福山通運ローズスタジアム",
            ["熊本|えがおS"] = "えがお健康スタジアム",
            ["千葉|柏の葉"] = "柏の葉公園総合競技場",
            ["千葉|オリプリ"] = "ゼットエーオリプリスタジアム",
            ["千葉|フクアリ"] = "フクダ電子アリーナ",
            ["東京|AGFフィ"] = "AGFフィールド",
            ["東京|AGFフィー"] = "AGFフィールド",
            ["東京|MUFG国立"] = "MUFGスタジアム（国立競技場）",
            ["東京|えどりく"] = "スピアーズえどりくフィールド（江戸川区陸上競技場）",
            ["東京|上柚木陸"] = "上柚木公園陸上競技場",
            ["東京|夢の島"] = "江東区夢の島競技場",
            ["東京|駒沢"] = "駒沢オリンピック公園総合運動場陸上競技場",
            ["東京|秩父宮"] = "秩父宮ラグビー場",
            ["東京|味スタ"] = "味の素スタジアム",
            ["栃木|足利陸"] = "足利ガスグラウンド（足利市総合運動場陸上競技場）",
            ["栃木|ホンスタ"] = "ホンダヒート・グリーンスタジアム",
            ["神奈川|大和"] = "大和なでしこスタジアム",
            ["神奈川|海老名"] = "海老名運動公園 陸上競技場",
            ["神奈川|荻野"] = "厚木市荻野運動公園競技場",
            ["神奈川|U等々力"] = "Uvanceとどろきスタジアム by Fujitsu",
            ["神奈川|ギオンス"] = "相模原ギオンスタジアム",
            ["神奈川|ニッパツ"] = "ニッパツ三ツ沢球技場",
            ["神奈川|日産ス"] = "日産スタジアム",
            ["福岡|久留米陸"] = "久留米総合スポーツセンター陸上競技場",
            ["福岡|博多陸"] = "東平尾公園博多の森陸上競技場",
            ["福岡|黒播陸"] = "黒崎播磨陸上競技場 in HONJO",
            ["福岡|小郡陸"] = "小郡市陸上競技場",
            ["福岡|ミクスタ"] = "ミクニワールドスタジアム北九州",
            ["福島|ハワスタ"] = "ハワイアンズスタジアムいわき",
            ["兵庫|ノエスタ"] = "ノエビアスタジアム神戸",
            ["兵庫|ユニバ"] = "神戸総合運動公園ユニバー記念競技場",
            ["群馬|太田市陸"] = "太田市運動公園陸上競技場",
            ["群馬|太田陸"] = "太田市運動公園陸上競技場",
            ["群馬|敷島"] = "アースケア敷島サッカー・ラグビー場",
            ["静岡|アイスタ"] = "IAIスタジアム日本平",
            ["静岡|エコパ"] = "エコパスタジアム",
            ["静岡|ヤマハ"] = "ヤマハスタジアム",
            ["長崎|かきどまり"] = "ベネックス総合運動公園 かきどまり陸上競技場",
            ["鹿児島|白波スタ"] = "白波スタジアム",
            ["未定|未定"] = "未定"
        };

        public static string Resolve(string? prefecture, string? venue)
        {
            var normalizedPrefecture = Normalize(prefecture);
            var normalizedVenue = Normalize(venue);
            if (string.IsNullOrWhiteSpace(normalizedVenue))
            {
                return "";
            }

            var key = $"{normalizedPrefecture}|{normalizedVenue}";
            if (OfficialNames.TryGetValue(key, out var officialName))
            {
                return officialName;
            }

#if DEBUG
            if (LoggedUnmappedKeys.Add(key))
            {
                Debug.WriteLine($"VenueNameResolver unresolved venue: pref='{normalizedPrefecture}', venue='{normalizedVenue}', key='{key}'");
            }
#endif

            return normalizedVenue;
        }

        private static string Normalize(string? value)
        {
            return value?.Trim() ?? "";
        }
    }
}


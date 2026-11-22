namespace OneRugbyNavi2;

public static class PlayerPositionMatcher
{
    public static bool HasPosition(PlayerRecord player, string position)
    {
        ArgumentNullException.ThrowIfNull(player);
        ArgumentException.ThrowIfNullOrWhiteSpace(position);

        var aliases = position switch
        {
            "PR" => new[] { "PR", "PROP", "プロップ" },
            "HO" => new[] { "HO", "HOOKER", "フッカー" },
            "LO" => new[] { "LO", "LOCK", "ロック" },
            "FL" => new[] { "FL", "FLANKER", "フランカー" },
            "No.8" => new[] { "NO8", "NO.8", "NUMBER8", "ナンバーエイト", "エイト" },
            "SH" => new[] { "SH", "SCRUMHALF", "スクラムハーフ" },
            "SO" => new[] { "SO", "STANDOFF", "スタンドオフ" },
            "CTB" => new[] { "CTB", "CENTER", "センター" },
            "WTB" => new[] { "WTB", "WING", "ウイング", "ウィング" },
            "FB" => new[] { "FB", "FULLBACK", "フルバック" },
            "UTB" => new[] { "UTB", "UBK", "UTILITYBACK", "ユーティリティバックス", "ユーティリティーバックス", "ユーティリティバック", "ユーティリティーバック" },
            _ => new[] { position }
        };

        return (player.Positions ?? []).Any(value =>
        {
            if (string.IsNullOrWhiteSpace(value)) return false;
            var normalized = Normalize(value);
            return aliases.Any(alias => normalized.Contains(Normalize(alias), StringComparison.Ordinal));
        });
    }

    private static string Normalize(string value)
        => new(value.Normalize().Where(char.IsLetterOrDigit).Select(char.ToUpperInvariant).ToArray());
}

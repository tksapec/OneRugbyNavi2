namespace OneRugbyNavi2;

public static class OfficialRankingUrl
{
    public static string ForSeason(int seasonStartYear)
    {
        if (seasonStartYear < 2000 || seasonStartYear > 2098)
        {
            throw new ArgumentOutOfRangeException(nameof(seasonStartYear));
        }

        return $"https://league-one.jp/ranking/?year={seasonStartYear}";
    }
}

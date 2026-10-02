namespace OneRugbyNavi2;

public enum SyncReadiness
{
    Ready,
    NotPublished,
    Partial,
    Failed
}

public sealed record TeamCatalogEntry(
    string TeamName,
    string DivisionCode,
    string ShortName = "",
    string AreaText = "",
    string LogoUrl = "");

public sealed record TeamIndexEntry(
    string LeagueOneTeamId,
    string TeamName,
    string DivisionCode,
    string TeamUrl,
    string LogoUrl);

public sealed record TeamIndexResult(
    SyncReadiness Readiness,
    int DetectedSeasonStartYear,
    IReadOnlyList<TeamIndexEntry> Teams);

public sealed record TeamIndexFetchResult(
    TeamIndexResult Snapshot,
    bool UsedFallback,
    DateTimeOffset UpdatedAt);

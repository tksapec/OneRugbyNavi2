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

public sealed record TeamCatalogSnapshot(
    int SeasonStartYear,
    DateTimeOffset FetchedAt,
    string SourceUrl,
    IReadOnlyList<TeamCatalogEntry> Teams);

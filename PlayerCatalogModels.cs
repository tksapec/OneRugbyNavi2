namespace OneRugbyNavi2;

public sealed class PlayerCatalogDocument
{
    public int SchemaVersion { get; init; }
    public string Season { get; init; } = "";
    public List<PlayerRecord> Players { get; init; } = [];
}

public sealed class PlayerRecord
{
    public string PlayerId { get; init; } = "";
    public string LeagueOnePlayerId { get; init; } = "";
    public string CurrentTeamId { get; init; } = "";
    public string CurrentTeamName { get; init; } = "";
    public string Division { get; init; } = "";
    public string NameJa { get; init; } = "";
    public string NameEn { get; init; } = "";
    public List<string> Aliases { get; init; } = [];
    public List<string> Positions { get; init; } = [];
    public DateOnly? BirthDate { get; init; }
    public int? HeightCm { get; init; }
    public int? WeightKg { get; init; }
    public List<string> Schools { get; init; } = [];
    public List<PlayerTeamHistory> TeamHistory { get; init; } = [];
    public List<PlayerRepresentativeHistory> RepresentativeHistory { get; init; } = [];
    public PlayerPortrait? Portrait { get; init; }
    public List<PlayerSource> Sources { get; init; } = [];
}

public sealed class PlayerTeamHistory
{
    public string TeamId { get; init; } = "";
    public string TeamName { get; init; } = "";
    public string FromSeason { get; init; } = "";
    public string ToSeason { get; init; } = "";
}

public sealed class PlayerRepresentativeHistory
{
    public string TeamName { get; init; } = "";
    public string Level { get; init; } = "";
    public int? Caps { get; init; }
    public List<string> Seasons { get; init; } = [];
}

public sealed class PlayerPortrait
{
    public string AssetPath { get; init; } = "";
    public string SourcePageUrl { get; init; } = "";
    public double CropX { get; init; } = 0.0;
    public double CropY { get; init; } = 0.0;
    public double CropWidth { get; init; } = 1.0;
    public double CropHeight { get; init; } = 1.0;
}

public sealed class PlayerSource
{
    public string Publisher { get; init; } = "";
    public string Url { get; init; } = "";
    public DateOnly? CheckedOn { get; init; }
    public List<string> Fields { get; init; } = [];
}

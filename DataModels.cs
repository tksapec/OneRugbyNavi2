using Microsoft.Maui.Controls;

namespace OneRugbyNavi2;

public sealed class TeamCard
{
    public int Id { get; init; }
    public string LeagueOneTeamId { get; init; } = "";
    public string TeamName { get; init; } = "";
    public string ShortName { get; init; } = "";
    public string DivisionCode { get; init; } = "";
    public string AreaText { get; init; } = "";
    public string TeamUrl { get; init; } = "";
    public string? LocalAssetPath { get; init; }
    public ImageSource? LogoSource => AssetImageResolver.CreateImageSource(LocalAssetPath);
    public string BadgeText => string.IsNullOrWhiteSpace(ShortName) ? Initials.FromText(TeamName) : ShortName;
    public string MetaText => $"{DivisionCode} / {AreaText}".Trim(' ', '/');
}

public sealed class PlayerCard
{
    public int Id { get; init; }
    public int? TeamId { get; init; }
    public string LeagueOnePlayerId { get; init; } = "";
    public string NameJa { get; init; } = "";
    public string NameEn { get; init; } = "";
    public string TeamName { get; init; } = "";
    public string PositionCode { get; init; } = "";
    public int? HeightCm { get; init; }
    public int? WeightKg { get; init; }
    public string BirthDate { get; init; } = "";
    public int? AgeCalculated { get; init; }
    public string RegistrationCategory { get; init; } = "";
    public int? LeagueOneCaps { get; init; }
    public string SchoolTeamHistoryText { get; init; } = "";
    public string ProfileUrl { get; init; } = "";
    public string? LocalAssetPath { get; init; }
    public ImageSource? PhotoSource => AssetImageResolver.CreateImageSource(LocalAssetPath);
    public string Initials => OneRugbyNavi2.Initials.FromText(NameJa);
    public string SizeText => $"{FormatNumber(HeightCm, "cm")} / {FormatNumber(WeightKg, "kg")}";
    public string AgeText => AgeCalculated.HasValue ? $"{AgeCalculated}歳" : "";
    public string CapsText => LeagueOneCaps.HasValue ? $"Caps {LeagueOneCaps}" : "Caps -";

    private static string FormatNumber(int? value, string unit) => value.HasValue ? $"{value}{unit}" : "-";
}

public sealed class RankingRow
{
    public int Rank { get; init; }
    public int? PlayerId { get; init; }
    public string Title { get; init; } = "";
    public string Subtitle { get; init; } = "";
    public string ValueText { get; init; } = "";
    public string? LocalAssetPath { get; init; }
    public ImageSource? PhotoSource => AssetImageResolver.CreateImageSource(LocalAssetPath);
    public string Initials => OneRugbyNavi2.Initials.FromText(Title);
    public bool IsPlayer => PlayerId.HasValue;
}

public sealed class TeamUpdateResult
{
    public string TeamName { get; init; } = "";
    public int UpdatedPlayers { get; init; }
    public int NewPlayers { get; init; }
    public int MissingCandidates { get; init; }
    public int UpdatedImages { get; init; }
    public int FailedPages { get; init; }
    public DateTime UpdatedAt { get; init; }
    public string Message { get; init; } = "";
}

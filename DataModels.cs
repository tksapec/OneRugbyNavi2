using Microsoft.Maui.Controls;

namespace OneRugbyNavi2;

public sealed class TeamCard
{
    public string LeagueOneTeamId { get; init; } = "";
    public string TeamName { get; init; } = "";
    public string DivisionCode { get; init; } = "";
    public string OfficialTeamPageUrl { get; init; } = "";
    public string LogoUrl { get; init; } = "";
    public string? LocalAssetPath { get; init; }
    public ImageSource? LogoSource => AssetImageResolver.CreateImageSource(LocalAssetPath)
        ?? (Uri.TryCreate(LogoUrl, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps
            ? new UriImageSource { Uri = uri, CachingEnabled = true, CacheValidity = TimeSpan.FromDays(30) }
            : null);
    public string BadgeText => Initials.FromText(TeamName);
    public string MetaText => DivisionCode;
}

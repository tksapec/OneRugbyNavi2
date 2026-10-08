using System.Text.Json;
using System.Text.RegularExpressions;

namespace OneRugbyNavi2;

public static partial class PlayerCatalogParser
{
    private const int SupportedSchemaVersion = 1;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    [GeneratedRegex(@"^\d{4}-\d{2}$", RegexOptions.CultureInvariant)]
    private static partial Regex SeasonRegex();

    public static bool TryParse(string? json, out PlayerCatalogDocument catalog, out IReadOnlyList<string> errors)
    {
        catalog = null!;
        if (string.IsNullOrWhiteSpace(json))
        {
            errors = ["Catalog JSON is empty."];
            return false;
        }

        try
        {
            var parsed = JsonSerializer.Deserialize<PlayerCatalogDocument>(json, JsonOptions);
            if (parsed is null)
            {
                errors = ["Catalog JSON has no document."];
                return false;
            }

            var found = new List<string>();
            if (parsed.SchemaVersion != SupportedSchemaVersion) found.Add($"Unsupported schema version: {parsed.SchemaVersion}.");
            if (parsed.Season is null || !SeasonRegex().IsMatch(parsed.Season)) found.Add("Season must use YYYY-YY format.");
            if (parsed.Players is null) found.Add("Players must be an array.");

            var ids = new HashSet<string>(StringComparer.Ordinal);
            if (parsed.Players is not null)
            {
                for (var i = 0; i < parsed.Players.Count; i++)
                {
                    var player = parsed.Players[i];
                    if (player is null) { found.Add($"Player at index {i} is null."); continue; }
                    ValidatePlayer(player, ids, found);
                }
            }

            errors = found;
            if (found.Count > 0) return false;
            catalog = parsed;
            return true;
        }
        catch (JsonException ex)
        {
            errors = [$"Invalid catalog JSON: {ex.Message}"];
            return false;
        }
    }

    private static void ValidatePlayer(PlayerRecord player, HashSet<string> ids, List<string> errors)
    {
        if (string.IsNullOrWhiteSpace(player.PlayerId)) errors.Add("Every player must have a stable playerId.");
        else if (!ids.Add(player.PlayerId.Trim())) errors.Add($"Duplicate playerId: {player.PlayerId}.");

        if (string.IsNullOrWhiteSpace(player.CurrentTeamId) || string.IsNullOrWhiteSpace(player.CurrentTeamName))
            errors.Add($"Player {player.PlayerId} must have a current team.");
        if (string.IsNullOrWhiteSpace(player.NameJa) && string.IsNullOrWhiteSpace(player.NameEn))
            errors.Add($"Player {player.PlayerId} must have a Japanese or English name.");
        if (player.HeightCm is <= 0 || player.WeightKg is <= 0) errors.Add($"Player {player.PlayerId} has an invalid height or weight.");

        if (player.Aliases is null || player.Positions is null || player.Schools is null)
            errors.Add($"Player {player.PlayerId} has a null aliases, positions, or schools list.");
        if (player.TeamHistory is null || player.TeamHistory.Any(item => item is null || string.IsNullOrWhiteSpace(item.TeamName)))
            errors.Add($"Player {player.PlayerId} has an invalid teamHistory list.");
        if (player.RepresentativeHistory is null || player.RepresentativeHistory.Any(item => item is null || string.IsNullOrWhiteSpace(item.TeamName)))
            errors.Add($"Player {player.PlayerId} has an invalid representativeHistory list.");

        if (player.Sources is null || player.Sources.Count == 0)
            errors.Add($"Player {player.PlayerId} must have at least one source.");
        else foreach (var source in player.Sources)
            if (source is null || string.IsNullOrWhiteSpace(source.Publisher) ||
                !Uri.TryCreate(source.Url, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https"))
                errors.Add($"Player {player.PlayerId} has an invalid source.");

        if (player.Portrait is not null)
        {
            if (!IsSafePortraitPath(player.Portrait.AssetPath)) errors.Add($"Player {player.PlayerId} has an unsafe portrait path.");
            if (player.Portrait.CropX < 0 || player.Portrait.CropY < 0 || player.Portrait.CropWidth <= 0 ||
                player.Portrait.CropHeight <= 0 || player.Portrait.CropX + player.Portrait.CropWidth > 1.000001 ||
                player.Portrait.CropY + player.Portrait.CropHeight > 1.000001)
                errors.Add($"Player {player.PlayerId} has invalid portrait crop coordinates.");
        }

        if (player.BirthDate is { } birthDate && birthDate > DateOnly.FromDateTime(DateTime.UtcNow))
            errors.Add($"Player {player.PlayerId} has a future birth date.");
    }

    private static bool IsSafePortraitPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || Path.IsPathRooted(path)) return false;
        var normalized = path.Replace('\\', '/');
        return normalized.StartsWith("player-portraits/", StringComparison.Ordinal) &&
               normalized.Split('/').All(part => part.Length > 0 && part is not ("." or "..")) &&
               !normalized.Contains(':');
    }
}
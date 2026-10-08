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

    [GeneratedRegex(@"^\\d{4}-\\d{2}$", RegexOptions.CultureInvariant)]
    private static partial Regex SeasonRegex();

    public static bool TryParse(
        string? json,
        out PlayerCatalogDocument catalog,
        out IReadOnlyList<string> errors)
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

            var validationErrors = new List<string>();
            if (parsed.SchemaVersion != SupportedSchemaVersion)
                validationErrors.Add($"Unsupported schema version: {parsed.SchemaVersion}.");
            if (parsed.Season is null || !SeasonRegex().IsMatch(parsed.Season))
                validationErrors.Add("Season must use YYYY-YY format.");
            if (parsed.Players is null)
                validationErrors.Add("Players must be an array.");

            var playerIds = new HashSet<string>(StringComparer.Ordinal);
            if (parsed.Players is not null)
            {
                for (var index = 0; index < parsed.Players.Count; index++)
                {
                    var player = parsed.Players[index];
                    if (player is null)
                    {
                        validationErrors.Add($"Player at index {index} is null.");
                        continue;
                    }
                    ValidatePlayer(player, playerIds, validationErrors);
                }
            }

            errors = validationErrors;
            if (validationErrors.Count > 0) return false;
            catalog = parsed;
            return true;
        }
        catch (JsonException ex)
        {
            errors = [$"Invalid catalog JSON: {ex.Message}"];
            return false;
        }
    }

    private static void ValidatePlayer(PlayerRecord player, HashSet<string> playerIds, List<string> errors)
    {
        if (string.IsNullOrWhiteSpace(player.PlayerId))
            errors.Add("Every player must have a stable playerId.");
        else if (!playerIds.Add(player.PlayerId.Trim()))
            errors.Add($"Duplicate playerId: {player.PlayerId}.");

        if (string.IsNullOrWhiteSpace(player.CurrentTeamId) || string.IsNullOrWhiteSpace(player.CurrentTeamName))
            errors.Add($"Player {player.PlayerId} must have a current team.");
        if (string.IsNullOrWhiteSpace(player.NameJa) && string.IsNullOrWhiteSpace(player.NameEn))
            errors.Add($"Player {player.PlayerId} must have a Japanese or English name.");
        if (player.HeightCm is <= 0 || player.WeightKg is <= 0)
            errors.Add($"Player {player.PlayerId} has an invalid height or weight.");

        ValidateStrings(player.Aliases, "aliases", player.PlayerId, errors);
        ValidateStrings(player.Positions, "positions", player.PlayerId, errors);
        ValidateStrings(player.Schools, "schools", player.PlayerId, errors);
        ValidateStrings(player.TeamHistory, "teamHistory", player.PlayerId, errors);
        ValidateStrings(player.RepresentativeHistory, "representativeHistory", player.PlayerId, errors);

        if (player.Sources is null || player.Sources.Count == 0)
        {
            errors.Add($"Player {player.PlayerId} must have at least one source.");
        }
        else
        {
            foreach (var source in player.Sources)
            {
                if (source is null || string.IsNullOrWhiteSpace(source.Publisher) ||
                    !Uri.TryCreate(source.Url, UriKind.Absolute, out var sourceUri) ||
                    sourceUri.Scheme is not ("http" or "https"))
                    errors.Add($"Player {player.PlayerId} has an invalid source.");
            }
        }

        if (player.Portrait is not null && !IsSafePortraitPath(player.Portrait.AssetPath))
            errors.Add($"Player {player.PlayerId} has an unsafe portrait path.");
        if (player.Portrait is not null &&
            (player.Portrait.CropX < 0 || player.Portrait.CropY < 0 ||
             player.Portrait.CropWidth <= 0 || player.Portrait.CropHeight <= 0 ||
             player.Portrait.CropX + player.Portrait.CropWidth > 1.000001 ||
             player.Portrait.CropY + player.Portrait.CropHeight > 1.000001))
            errors.Add($"Player {player.PlayerId} has invalid portrait crop coordinates.");
        if (player.BirthDate is { } birthDate && birthDate > DateOnly.FromDateTime(DateTime.UtcNow))
            errors.Add($"Player {player.PlayerId} has a future birth date.");
    }

    private static void ValidateStrings<T>(List<T>? values, string field, string playerId, List<string> errors)
    {
        if (values is null) errors.Add($"Player {playerId} has null {field}.");
    }

    private static void ValidateStrings(List<PlayerTeamHistory>? values, string field, string playerId, List<string> errors)
    {
        if (values is null) errors.Add($"Player {playerId} has null {field}.");
        else if (values.Any(item => item is null || string.IsNullOrWhiteSpace(item.TeamName)))
            errors.Add($"Player {playerId} has an invalid {field} entry.");
    }

    private static void ValidateStrings(List<PlayerRepresentativeHistory>? values, string field, string playerId, List<string> errors)
    {
        if (values is null) errors.Add($"Player {playerId} has null {field}.");
        else if (values.Any(item => item is null || string.IsNullOrWhiteSpace(item.TeamName)))
            errors.Add($"Player {playerId} has an invalid {field} entry.");
    }

    private static bool IsSafePortraitPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || Path.IsPathRooted(path)) return false;
        var normalized = path.Replace('\\\\', '/');
        return normalized.StartsWith("player-portraits/", StringComparison.Ordinal) &&
               normalized.Split('/').All(part => part.Length > 0 && part is not ("." or "..")) &&
               !normalized.Contains(':');
    }
}
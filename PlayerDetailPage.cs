using Microsoft.Maui.ApplicationModel;

namespace OneRugbyNavi2;

public sealed class PlayerDetailPage : ContentPage
{
    private static readonly IReadOnlyDictionary<string, string> FieldLabels = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["nameJa"] = "日本語名",
        ["nameEn"] = "英語名",
        ["aliases"] = "別表記",
        ["currentTeamName"] = "現所属",
        ["division"] = "ディビジョン",
        ["positions"] = "ポジション",
        ["birthDate"] = "生年月日",
        ["heightCm"] = "身長",
        ["weightKg"] = "体重",
        ["schools"] = "出身校",
        ["teamHistory"] = "チーム所属歴",
        ["representativeHistory"] = "代表歴",
        ["leagueOnePlayerId"] = "リーグワン選手ID"
    };

    public PlayerDetailPage(PlayerRecord player)
    {
        ArgumentNullException.ThrowIfNull(player);
        var title = string.IsNullOrWhiteSpace(player.NameJa) ? player.NameEn : player.NameJa;
        Title = title;
        BackgroundColor = PageStyles.Background;
        ToolbarItems.Add(new ToolbarItem("閉じる", null, async () => await Navigation.PopModalAsync()));

        var content = new VerticalStackLayout { Spacing = 12, Padding = new Thickness(16, 12) };
        AddIdentity(content, player, title);
        AddProfile(content, player);
        AddTeamHistory(content, player);
        AddRepresentativeHistory(content, player);
        AddSources(content, player);

        Content = new ScrollView { Content = content };
    }

    private static void AddIdentity(VerticalStackLayout content, PlayerRecord player, string title)
    {
        var name = new Label
        {
            Text = title,
            FontSize = 24,
            FontAttributes = FontAttributes.Bold,
            TextColor = PageStyles.Navy
        };
        var english = new Label
        {
            Text = player.NameEn,
            FontSize = 14,
            TextColor = PageStyles.Muted,
            IsVisible = !string.IsNullOrWhiteSpace(player.NameEn)
        };
        var team = new Label
        {
            Text = $"{player.CurrentTeamName} · {player.Division}",
            FontSize = 15,
            FontAttributes = FontAttributes.Bold,
            TextColor = PageStyles.Blue
        };
        content.Add(PageStyles.Card(new VerticalStackLayout { Spacing = 4, Children = { name, english, team } }));
    }

    private static void AddProfile(VerticalStackLayout content, PlayerRecord player)
    {
        var rows = new VerticalStackLayout { Spacing = 8 };
        AddRow(rows, "ポジション", player.Positions.Count == 0 ? "未登録" : string.Join(" / ", player.Positions));
        if (player.BirthDate is { } birthDate)
        {
            var age = GetAge(birthDate, DateOnly.FromDateTime(DateTime.Today));
            AddRow(rows, "生年月日", $"{birthDate:yyyy年M月d日}（{age}歳）");
        }
        if (player.HeightCm.HasValue || player.WeightKg.HasValue)
        {
            AddRow(rows, "身長・体重", $"{Format(player.HeightCm, "cm")} / {Format(player.WeightKg, "kg")}");
        }
        if (player.Schools.Count > 0) AddRow(rows, "出身校", string.Join(" / ", player.Schools));
        if (player.Aliases.Count > 0) AddRow(rows, "別表記・検索語", string.Join(" / ", player.Aliases));
        if (!string.IsNullOrWhiteSpace(player.LeagueOnePlayerId)) AddRow(rows, "リーグワン選手ID", player.LeagueOnePlayerId);
        content.Add(Section("プロフィール", rows));
    }

    private static void AddTeamHistory(VerticalStackLayout content, PlayerRecord player)
    {
        if (player.TeamHistory.Count == 0) return;
        var rows = new VerticalStackLayout { Spacing = 8 };
        foreach (var history in player.TeamHistory)
        {
            var period = GetPeriod(history.FromSeason, history.ToSeason);
            var row = new Grid
            {
                ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto) },
                ColumnSpacing = 8,
                Children =
                {
                    new Label { Text = history.TeamName, FontSize = 14, TextColor = PageStyles.Text, VerticalTextAlignment = TextAlignment.Center },
                    new Label { Text = period, FontSize = 12, TextColor = PageStyles.Muted, VerticalTextAlignment = TextAlignment.Center }.Column(1)
                }
            };
            rows.Add(row);
        }
        content.Add(Section("所属チームの経歴", rows));
    }

    private static void AddRepresentativeHistory(VerticalStackLayout content, PlayerRecord player)
    {
        if (player.RepresentativeHistory.Count == 0) return;
        var rows = new VerticalStackLayout { Spacing = 8 };
        foreach (var history in player.RepresentativeHistory)
        {
            var details = new List<string>();
            if (!string.IsNullOrWhiteSpace(history.Level)) details.Add(history.Level);
            if (history.Caps is { } caps) details.Add($"{caps}キャップ");
            if (history.Seasons.Count > 0) details.Add(string.Join(", ", history.Seasons));
            AddRow(rows, history.TeamName, string.Join(" · ", details));
        }
        content.Add(Section("代表歴", rows));
    }

    private void AddSources(VerticalStackLayout content, PlayerRecord player)
    {
        if (player.Sources.Count == 0) return;
        var rows = new VerticalStackLayout { Spacing = 10 };
        foreach (var source in player.Sources)
        {
            var publisher = new Label
            {
                Text = source.Publisher,
                FontSize = 14,
                FontAttributes = FontAttributes.Bold,
                TextColor = PageStyles.Navy
            };
            var checkedOn = new Label
            {
                Text = source.CheckedOn is { } date ? $"確認日: {date:yyyy-MM-dd}" : "確認日: 未記録",
                FontSize = 12,
                TextColor = PageStyles.Muted
            };
            var fields = source.Fields
                .Select(field => FieldLabels.TryGetValue(field, out var label) ? label : field)
                .Distinct(StringComparer.Ordinal);
            var supportedFacts = new Label
            {
                Text = $"確認した項目: {string.Join("・", fields)}",
                FontSize = 12,
                TextColor = PageStyles.Text
            };
            var open = PageStyles.SecondaryButton("出典ページを開く");
            open.HorizontalOptions = LayoutOptions.Start;
            open.Clicked += async (_, _) => await OpenSourceAsync(source.Url);
            rows.Add(new VerticalStackLayout { Spacing = 3, Children = { publisher, checkedOn, supportedFacts, open } });
        }
        content.Add(Section("出典", rows));
    }

    private static View Section(string title, View body)
    {
        var heading = new Label
        {
            Text = title,
            FontSize = 16,
            FontAttributes = FontAttributes.Bold,
            TextColor = PageStyles.Navy
        };
        return PageStyles.Card(new VerticalStackLayout { Spacing = 10, Children = { heading, body } });
    }

    private static void AddRow(VerticalStackLayout rows, string label, string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return;
        var title = new Label
        {
            Text = label,
            FontSize = 12,
            TextColor = PageStyles.Muted
        };
        var text = new Label
        {
            Text = value,
            FontSize = 14,
            TextColor = PageStyles.Text
        };
        rows.Add(new VerticalStackLayout { Spacing = 2, Children = { title, text } });
    }

    private async Task OpenSourceAsync(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https")) return;
        try
        {
            await Browser.Default.OpenAsync(uri, BrowserLaunchMode.SystemPreferred);
        }
        catch (Exception ex)
        {
            await DisplayAlert("出典", $"ページを開けませんでした。\n{ex.Message}", "閉じる");
        }
    }

    private static int GetAge(DateOnly birthDate, DateOnly today)
    {
        var age = today.Year - birthDate.Year;
        if (birthDate > today.AddYears(-age)) age--;
        return age;
    }

    private static string Format(int? value, string suffix) => value is { } number ? $"{number}{suffix}" : "未登録";

    private static string GetPeriod(string from, string to)
    {
        if (string.IsNullOrWhiteSpace(from)) return "時期未確認";
        if (string.IsNullOrWhiteSpace(to)) return $"{from}〜現在";
        return from == to ? from : $"{from}〜{to}";
    }
}

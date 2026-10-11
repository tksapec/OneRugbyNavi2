namespace OneRugbyNavi2;

public sealed record PlayerFilterSelection(string Division, string Team, string Position, string AffiliationType);

public sealed class PlayerFiltersPage : ContentPage
{
    private static readonly (string Label, string Value)[] Divisions =
    [
        ("すべて", ""), ("DIV.1", "DIV1"), ("DIV.2", "DIV2"), ("DIV.3", "DIV3")
    ];
    private static readonly string[] Positions = ["PR", "HO", "LO", "FL", "No.8", "SH", "SO", "CTB", "WTB", "FB", "UTB"];
    private static readonly (string Label, string Value)[] Types =
    [
        ("すべて", ""), ("クラブ", "club"), ("大学", "university"), ("高校", "highSchool")
    ];

    public PlayerFiltersPage(IEnumerable<PlayerRecord> players, PlayerFilterSelection current, Action<PlayerFilterSelection> apply)
    {
        Title = "絞り込み条件";
        BackgroundColor = PageStyles.Background;
        ToolbarItems.Add(new ToolbarItem("閉じる", null, async () => await Navigation.PopModalAsync()));

        var division = CreatePicker("ディビジョン", Divisions.Select(option => option.Label).ToArray(),
            Math.Max(0, Array.FindIndex(Divisions, option => option.Value == current.Division)));
        var teams = players.Where(player => current.Division.Length == 0 || player.Division == current.Division)
            .Select(player => player.CurrentTeamName).Distinct(StringComparer.Ordinal)
            .OrderBy(name => name, StringComparer.CurrentCulture).ToArray();
        var teamOptions = new[] { "すべて" }.Concat(teams).ToArray();
        var teamIndex = Array.IndexOf(teamOptions, current.Team);
        var team = CreatePicker("現所属チーム", teamOptions, Math.Max(teamIndex, 0));
        var positionOptions = new[] { "すべて" }.Concat(Positions).ToArray();
        var position = CreatePicker("ポジション", positionOptions,
            Math.Max(Array.IndexOf(positionOptions, current.Position), 0));
        var type = CreatePicker("所属区分", Types.Select(option => option.Label).ToArray(),
            Math.Max(Array.FindIndex(Types, option => option.Value == current.AffiliationType), 0));

        division.SelectedIndexChanged += (_, _) =>
        {
            var code = Divisions.ElementAtOrDefault(division.SelectedIndex).Value ?? "";
            var selected = team.SelectedItem as string;
            var filteredTeams = players.Where(player => code.Length == 0 || player.Division == code)
                .Select(player => player.CurrentTeamName).Distinct(StringComparer.Ordinal)
                .OrderBy(name => name, StringComparer.CurrentCulture).ToArray();
            team.ItemsSource = new[] { "すべて" }.Concat(filteredTeams).ToArray();
            team.SelectedIndex = selected is not null && filteredTeams.Contains(selected, StringComparer.Ordinal)
                ? Array.IndexOf(team.ItemsSource.Cast<string>().ToArray(), selected) : 0;
        };

        var reset = PageStyles.SecondaryButton("絞り込み条件を初期化");
        reset.Clicked += (_, _) =>
        {
            division.SelectedIndex = 0;
            team.SelectedIndex = 0;
            position.SelectedIndex = 0;
            type.SelectedIndex = 0;
        };
        var submit = new Button
        {
            Text = "この条件で検索", TextColor = Colors.White, BackgroundColor = PageStyles.Blue,
            FontAttributes = FontAttributes.Bold, CornerRadius = 12, MinimumHeightRequest = 48
        };
        submit.Clicked += async (_, _) =>
        {
            var divisionCode = Divisions.ElementAtOrDefault(division.SelectedIndex).Value ?? "";
            var typeValue = Types.ElementAtOrDefault(type.SelectedIndex).Value ?? "";
            apply(new PlayerFilterSelection(
                divisionCode,
                team.SelectedItem as string is { } selectedTeam && selectedTeam != "すべて" ? selectedTeam : "",
                position.SelectedItem as string is { } selectedPosition && selectedPosition != "すべて" ? selectedPosition : "",
                typeValue));
            await Navigation.PopModalAsync();
        };

        var stack = new VerticalStackLayout { Padding = new Thickness(16, 8), Spacing = 8 };
        stack.Add(new Label { Text = "条件を選んでください。項目を組み合わせると、すべてに一致する選手を表示します。", TextColor = PageStyles.Muted, FontSize = 13 });
        stack.Add(Labeled("ディビジョン", division));
        stack.Add(Labeled("現所属チーム", team));
        stack.Add(Labeled("ポジション", position));
        stack.Add(Labeled("所属区分", type));
        stack.Add(reset);
        stack.Add(submit);
        Content = new ScrollView { Content = stack };
    }

    private static Picker CreatePicker(string title, string[] items, int index)
    {
        var picker = PageStyles.Picker(title);
        picker.ItemsSource = items;
        picker.SelectedIndex = index;
        picker.MinimumHeightRequest = 48;
        return picker;
    }

    private static View Labeled(string label, View control)
        => new VerticalStackLayout { Spacing = 2, Children = { new Label { Text = label, FontSize = 13, FontAttributes = FontAttributes.Bold, TextColor = PageStyles.Navy }, control } };
}

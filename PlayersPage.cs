using System.Collections.ObjectModel;

namespace OneRugbyNavi2;

public sealed class PlayersPage : ContentPage
{
    private const string AllTeams = "すべてのチーム";
    private const string AllPositions = "すべてのポジション";
    private static readonly (string Label, string Code)[] Divisions =
    [
        ("すべて", ""),
        ("DIV.1", "DIV1"),
        ("DIV.2", "DIV2"),
        ("DIV.3", "DIV3")
    ];
    private static readonly string[] PositionFilters =
    ["PR", "HO", "LO", "FL", "No.8", "SH", "SO", "CTB", "WTB", "FB", "UTB"];

    private readonly PlayerCatalogLoader _loader = new();
    private readonly ObservableCollection<PlayerRecord> _results = [];
    private readonly List<Button> _divisionButtons = [];
    private readonly SearchBar _searchBar = new()
    {
        Placeholder = "選手名・チーム・出身校などを入力",
        CancelButtonColor = PageStyles.Blue
    };
    private readonly Picker _searchScopePicker = PageStyles.Picker("検索対象を選択");
    private readonly Picker _teamPicker = PageStyles.Picker(AllTeams);
    private readonly Picker _positionPicker = PageStyles.Picker(AllPositions);
    private readonly Label _catalogStatus = PageStyles.MutedLabel("選手データを読み込み中...");
    private readonly Label _resultCount = new()
    {
        FontSize = 13,
        FontAttributes = FontAttributes.Bold,
        TextColor = PageStyles.Navy,
        VerticalTextAlignment = TextAlignment.Center
    };
    private readonly Label _empty = new()
    {
        Text = "条件に合う選手はいません。\n検索語や絞り込み条件を変えてみてください。",
        TextColor = PageStyles.Muted,
        HorizontalTextAlignment = TextAlignment.Center,
        VerticalTextAlignment = TextAlignment.Center,
        IsVisible = false,
        Margin = new Thickness(32)
    };
    private readonly Button _retryButton = PageStyles.SecondaryButton("再読み込み");
    private readonly Button _clearFiltersButton = PageStyles.SecondaryButton("絞り込みを解除");
    private IReadOnlyList<PlayerRecord> _players = [];
    private string _selectedDivision = "";
    private bool _loaded;
    private bool _loading;
    private bool _updatingFilters;

    public PlayersPage()
    {
        Title = "選手検索";
        BackgroundColor = PageStyles.Background;
        Shell.SetNavBarIsVisible(this, false);
        _searchScopePicker.ItemsSource = new[] { "すべての項目", "現所属チーム", "過去の所属チーム", "出身校" };
        _searchScopePicker.SelectedIndex = 0;
        _searchScopePicker.SelectedIndexChanged += OnSearchScopeChanged;
        _searchBar.TextChanged += OnSearchTextChanged;
        _teamPicker.SelectedIndexChanged += OnFilterChanged;
        _positionPicker.SelectedIndexChanged += OnFilterChanged;
        _retryButton.IsVisible = false;
        _clearFiltersButton.IsVisible = false;
        _retryButton.Clicked += async (_, _) => await LoadAsync();
        _clearFiltersButton.Clicked += (_, _) => ClearFilters();

        var list = new CollectionView
        {
            ItemsSource = _results,
            ItemTemplate = new DataTemplate(CreatePlayerCard),
            SelectionMode = SelectionMode.None,
            ItemsLayout = new LinearItemsLayout(ItemsLayoutOrientation.Vertical) { ItemSpacing = 2 },
            Margin = new Thickness(0, 0, 0, 8)
        };

        var divisionPicker = BuildDivisionPicker();
        var filters = new VerticalStackLayout
        {
            Spacing = 0,
            Margin = new Thickness(16, 0, 16, 4),
            Children = { _teamPicker, _positionPicker }
        };
        var resultHeader = new Grid
        {
            ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto) },
            Margin = new Thickness(16, 0, 16, 4),
            Children =
            {
                _catalogStatus,
                _resultCount.Column(1)
            }
        };
        var emptyState = new VerticalStackLayout
        {
            Spacing = 12,
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.Center,
            Children = { _empty, _clearFiltersButton, _retryButton }
        };

        Content = new Grid
        {
            RowDefinitions =
            {
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Star)
            },
            Children =
            {
                PageStyles.NavigationTitle(this, "選手検索"),
                _searchScopePicker.Row(1).Margin(new Thickness(16, 0, 16, 0)),
                _searchBar.Row(2).Margin(new Thickness(16, 0, 16, 4)),
                divisionPicker.Row(3),
                filters.Row(4),
                resultHeader.Row(5),
                list.Row(6),
                emptyState.Row(6)
            }
        };
        UpdateDivisionButtons();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (!_loaded && !_loading) await LoadAsync();
    }

    private View BuildDivisionPicker()
    {
        var grid = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Star)
            },
            ColumnSpacing = 6,
            Margin = new Thickness(16, 0, 16, 2)
        };

        for (var i = 0; i < Divisions.Length; i++)
        {
            var divisionIndex = i;
            var button = PageStyles.SecondaryButton(Divisions[i].Label);
            button.FontSize = 12;
            button.Padding = new Thickness(4, 7);
            button.Clicked += (_, _) => SelectDivision(divisionIndex);
            SemanticProperties.SetHint(button, Divisions[i].Code.Length == 0
                ? "全ディビジョンの選手を表示"
                : $"{Divisions[i].Label}の選手に絞り込む");
            _divisionButtons.Add(button);
            grid.Add(button, i, 0);
        }

        return grid;
    }

    private void SelectDivision(int index)
    {
        _selectedDivision = Divisions[index].Code;
        PopulateTeamPicker();
        UpdateDivisionButtons();
        RefreshResults();
    }

    private void UpdateDivisionButtons()
    {
        var selectedIndex = Array.FindIndex(Divisions, division => division.Code == _selectedDivision);
        for (var i = 0; i < _divisionButtons.Count; i++)
        {
            var selected = i == selectedIndex;
            _divisionButtons[i].BackgroundColor = selected ? PageStyles.Blue : PageStyles.ChipBackground;
            _divisionButtons[i].TextColor = selected ? Colors.White : PageStyles.Blue;
            _divisionButtons[i].FontAttributes = selected ? FontAttributes.Bold : FontAttributes.None;
            SemanticProperties.SetDescription(_divisionButtons[i], selected ? "選択中" : "タップして選択");
        }
    }

    private async Task LoadAsync()
    {
        if (_loading) return;
        _loading = true;
        _retryButton.IsVisible = false;
        _catalogStatus.Text = "選手データを読み込み中...";
        try
        {
            var catalog = await _loader.LoadAsync();
            _players = catalog.Players;
            _loaded = true;
            PopulateTeamPicker();
            RefreshResults();
            _catalogStatus.Text = $"{catalog.Season}シーズン · 全{_players.Count:N0}人";
        }
        catch (Exception ex)
        {
            _catalogStatus.Text = $"選手データを読み込めませんでした: {ex.Message}";
            _resultCount.Text = "";
            _empty.IsVisible = false;
            _retryButton.IsVisible = true;
        }
        finally
        {
            _loading = false;
        }
    }

    private void PopulateTeamPicker()
    {
        var selectedTeam = _teamPicker.SelectedItem as string;
        var teams = _players
            .Where(player => _selectedDivision.Length == 0 || player.Division == _selectedDivision)
            .Select(player => player.CurrentTeamName)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(name => name, StringComparer.CurrentCulture)
            .ToArray();

        _updatingFilters = true;
        var teamOptions = new[] { AllTeams }.Concat(teams).ToArray();
        _teamPicker.ItemsSource = teamOptions;
        var keepSelection = selectedTeam is not null && teams.Contains(selectedTeam, StringComparer.Ordinal);
        _teamPicker.SelectedIndex = keepSelection ? Array.IndexOf(teamOptions, selectedTeam) : 0;
        if (_positionPicker.ItemsSource is null)
        {
            _positionPicker.ItemsSource = new[] { AllPositions }.Concat(PositionFilters).ToArray();
            _positionPicker.SelectedIndex = 0;
        }
        _updatingFilters = false;
    }

    private void OnSearchTextChanged(object? sender, TextChangedEventArgs e) => RefreshResults();

    private void OnSearchScopeChanged(object? sender, EventArgs e)
    {
        _searchBar.Placeholder = _searchScopePicker.SelectedIndex switch
        {
            1 => "現所属チーム名を入力",
            2 => "過去に所属したチーム名を入力",
            3 => "出身校名を入力",
            _ => "選手名・チーム・出身校などを入力"
        };
        RefreshResults();
    }

    private void OnFilterChanged(object? sender, EventArgs e)
    {
        if (!_updatingFilters) RefreshResults();
    }

    private void RefreshResults()
    {
        _results.Clear();
        if (_players.Count == 0)
        {
            _resultCount.Text = "";
            _empty.Text = _loaded
                ? "このシーズンの選手データはまだありません。"
                : "選手データを読み込めませんでした。再読み込みしてください。";
            _empty.IsVisible = _loaded;
            _clearFiltersButton.IsVisible = false;
            return;
        }

        _empty.Text = "条件に合う選手はいません。\n検索語や絞り込み条件を変えてみてください。";
        var team = _teamPicker.SelectedItem as string;
        var position = _positionPicker.SelectedItem as string;
        var searchScope = _searchScopePicker.SelectedIndex switch
        {
            1 => PlayerSearchScope.CurrentTeam,
            2 => PlayerSearchScope.PreviousTeam,
            3 => PlayerSearchScope.School,
            _ => PlayerSearchScope.All
        };
        var matches = PlayerSearch.Search(_players, _searchBar.Text, searchScope)
            .Where(player => _selectedDivision.Length == 0 || player.Division == _selectedDivision)
            .Where(player => string.IsNullOrWhiteSpace(team) || team == AllTeams || player.CurrentTeamName == team)
            .Where(player => string.IsNullOrWhiteSpace(position) || position == AllPositions || HasPosition(player, position))
            .OrderBy(player => string.IsNullOrWhiteSpace(player.NameJa) ? player.NameEn : player.NameJa, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();

        foreach (var player in matches) _results.Add(player);
        _resultCount.Text = $"{matches.Length:N0}人を表示";
        _empty.IsVisible = matches.Length == 0;
        _clearFiltersButton.IsVisible = matches.Length == 0 && HasActiveFilters();
    }

    private bool HasActiveFilters()
        => !string.IsNullOrWhiteSpace(_searchBar.Text) ||
           _selectedDivision.Length > 0 ||
           (_teamPicker.SelectedItem is string team && team != AllTeams) ||
           (_positionPicker.SelectedItem is string position && position != AllPositions);

    private void ClearFilters()
    {
        _searchBar.Text = "";
        _searchScopePicker.SelectedIndex = 0;
        _selectedDivision = "";
        _updatingFilters = true;
        PopulateTeamPicker();
        _teamPicker.SelectedIndex = 0;
        _positionPicker.SelectedIndex = 0;
        _updatingFilters = false;
        UpdateDivisionButtons();
        RefreshResults();
    }

    internal static bool HasPosition(PlayerRecord player, string position)
    {
        var aliases = position switch
        {
            "PR" => new[] { "PR", "PROP", "プロップ" },
            "HO" => new[] { "HO", "HOOKER", "フッカー" },
            "LO" => new[] { "LO", "LOCK", "ロック" },
            "FL" => new[] { "FL", "FLANKER", "フランカー" },
            "No.8" => new[] { "NO8", "NO.8", "NUMBER8", "ナンバーエイト", "エイト" },
            "SH" => new[] { "SH", "SCRUMHALF", "スクラムハーフ" },
            "SO" => new[] { "SO", "STANDOFF", "スタンドオフ" },
            "CTB" => new[] { "CTB", "CENTER", "センター" },
            "WTB" => new[] { "WTB", "WING", "ウイング", "ウィング" },
            "FB" => new[] { "FB", "FULLBACK", "フルバック" },
            "UTB" => new[] { "UTB", "UBK", "UTILITYBACK", "ユーティリティーバックス", "ユーティリティーバック" },
            _ => new[] { position }
        };

        return player.Positions.Any(value =>
        {
            var normalized = NormalizePositionText(value);
            return aliases.Any(alias => normalized.Contains(NormalizePositionText(alias), StringComparison.Ordinal));
        });
    }

    private static string NormalizePositionText(string value)
        => new(value.Normalize().Where(char.IsLetterOrDigit).Select(char.ToUpperInvariant).ToArray());

    internal static string PositionBadgeFor(PlayerRecord player)
        => PositionFilters.FirstOrDefault(position => HasPosition(player, position)) ?? "選手";

    private View CreatePlayerCard()
    {
        var mainPosition = new Label
        {
            FontSize = 12,
            FontAttributes = FontAttributes.Bold,
            TextColor = PageStyles.Blue,
            HorizontalTextAlignment = TextAlignment.Center,
            VerticalTextAlignment = TextAlignment.Center
        };
        mainPosition.SetBinding(Label.TextProperty, new Binding(".", converter: new PositionBadgeConverter()));
        var badge = new Border
        {
            WidthRequest = 50,
            HeightRequest = 50,
            BackgroundColor = PageStyles.ChipBackground,
            Stroke = Colors.Transparent,
            StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 14 },
            Content = mainPosition
        };

        var name = new Label { FontSize = 16, FontAttributes = FontAttributes.Bold, TextColor = PageStyles.Navy, LineBreakMode = LineBreakMode.TailTruncation };
        name.SetBinding(Label.TextProperty, new Binding(".", converter: new PlayerDisplayNameConverter()));
        var englishName = new Label { FontSize = 11, TextColor = PageStyles.Muted, LineBreakMode = LineBreakMode.TailTruncation };
        englishName.SetBinding(Label.TextProperty, nameof(PlayerRecord.NameEn));
        englishName.SetBinding(IsVisibleProperty, new Binding(nameof(PlayerRecord.NameEn), converter: new NonEmptyStringConverter()));
        var meta = new Label { FontSize = 12, TextColor = PageStyles.Text, LineBreakMode = LineBreakMode.WordWrap };
        meta.SetBinding(Label.TextProperty, new Binding(".", converter: new PlayerMetadataConverter()));
        var previous = new Label { FontSize = 11, TextColor = PageStyles.Muted, LineBreakMode = LineBreakMode.TailTruncation };
        previous.SetBinding(Label.TextProperty, new Binding(".", converter: new PlayerPastTeamsConverter()));
        previous.SetBinding(IsVisibleProperty, new Binding(".", converter: new PlayerHasPastTeamsConverter()));

        var details = new VerticalStackLayout { Spacing = 2, Children = { name, englishName, meta, previous } };
        var row = new Grid
        {
            ColumnDefinitions = { new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Star) },
            ColumnSpacing = 12,
            VerticalOptions = LayoutOptions.Center,
            Children = { badge.Column(0), details.Column(1) }
        };
        var card = PageStyles.Card(row);
        card.Padding = 12;
        card.SetBinding(BindableObject.BindingContextProperty, ".");
        var tap = new TapGestureRecognizer();
        tap.Tapped += async (_, _) =>
        {
            if (card.BindingContext is PlayerRecord player) await ShowPlayerAsync(player);
        };
        card.GestureRecognizers.Add(tap);
        SemanticProperties.SetHint(card, "タップして選手プロフィールと出典を表示");
        return card;
    }

    private async Task ShowPlayerAsync(PlayerRecord player)
    {
        await Navigation.PushModalAsync(new NavigationPage(new PlayerDetailPage(player))
        {
            BarBackgroundColor = PageStyles.Surface,
            BarTextColor = PageStyles.Navy
        });
    }
}

internal sealed class PlayerDisplayNameConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
        => value is PlayerRecord player ? (string.IsNullOrWhiteSpace(player.NameJa) ? player.NameEn : player.NameJa) : "";
    public object ConvertBack(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture) => throw new NotSupportedException();
}

internal sealed class PlayerMetadataConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
        => value is PlayerRecord player ? $"{player.CurrentTeamName} · {string.Join(" / ", player.Positions)}" : "";
    public object ConvertBack(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture) => throw new NotSupportedException();
}

internal sealed class PositionBadgeConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
        => value is PlayerRecord player ? PlayersPage.PositionBadgeFor(player) : "選手";
    public object ConvertBack(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture) => throw new NotSupportedException();
}

internal sealed class PlayerPastTeamsConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
    {
        var pastTeams = GetPastTeams(value);
        return pastTeams.Length > 0 ? $"前所属: {string.Join("、", pastTeams)}" : "";
    }
    internal static string[] GetPastTeams(object? value)
        => value is PlayerRecord player
            ? player.TeamHistory.Where(history => history.TeamId != player.CurrentTeamId || history.TeamName != player.CurrentTeamName)
                .Select(history => history.TeamName).Distinct(StringComparer.Ordinal).ToArray()
            : [];
    public object ConvertBack(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture) => throw new NotSupportedException();
}

internal sealed class PlayerHasPastTeamsConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
        => PlayerPastTeamsConverter.GetPastTeams(value).Length > 0;
    public object ConvertBack(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture) => throw new NotSupportedException();
}

internal sealed class NonEmptyStringConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
        => value is string text && !string.IsNullOrWhiteSpace(text);
    public object ConvertBack(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture) => throw new NotSupportedException();
}

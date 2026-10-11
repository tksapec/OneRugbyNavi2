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
    private const string LastCatalogCheckPreferenceKey = "player-catalog-last-check-utc";
    private static readonly HttpClient CatalogHttpClient = new(new HttpClientHandler { AllowAutoRedirect = false });

    private readonly PlayerCatalogLoader _loader = new();
    private readonly PlayerCatalogUpdateService _updateService = new(
        CatalogHttpClient,
        PlayerCatalogUpdateService.DefaultManifestUri,
        Path.Combine(FileSystem.AppDataDirectory, PlayerCatalogCache.CacheFileName));
    private readonly ObservableCollection<PlayerRecord> _results = [];
    private readonly SearchBar _searchBar = new()
    {
        Placeholder = "選手名・所属歴・出身校・シーズンを入力",
        TextColor = PageStyles.Navy,
        PlaceholderColor = PageStyles.Muted,
        BackgroundColor = PageStyles.Surface,
        CancelButtonColor = PageStyles.Blue
    };
    private readonly Picker _teamPicker = PageStyles.Picker(AllTeams);
    private readonly Picker _positionPicker = PageStyles.Picker(AllPositions);
    private readonly Button _filterButton = PageStyles.SecondaryButton("絞り込み");
    private readonly Label _filterSummary = PageStyles.MutedLabel("絞り込み条件はありません");
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
    private readonly Button _updateButton = PageStyles.SecondaryButton("選手データを更新");
    private readonly ActivityIndicator _updateActivity = new()
    {
        Color = PageStyles.Blue,
        IsVisible = false,
        IsRunning = false,
        WidthRequest = 24,
        HeightRequest = 24,
        VerticalOptions = LayoutOptions.Center
    };
    private readonly Button _clearFiltersButton = PageStyles.SecondaryButton("検索条件をすべて解除");
    private IReadOnlyList<PlayerRecord> _players = [];
    private string _selectedDivision = "";
    private string _selectedAffiliationType = "";
    private bool _loaded;
    private bool _loading;
    private bool _updatingFilters;

    public PlayersPage()
    {
        Title = "選手検索";
        BackgroundColor = PageStyles.Background;
        Shell.SetNavBarIsVisible(this, false);
        _searchBar.TextChanged += OnSearchTextChanged;
        _teamPicker.SelectedIndexChanged += OnFilterChanged;
        _positionPicker.SelectedIndexChanged += OnFilterChanged;
        _retryButton.IsVisible = false;
        _clearFiltersButton.IsVisible = false;
        _filterButton.IsEnabled = false;
        _retryButton.Clicked += async (_, _) => await LoadAsync();
        _updateButton.Clicked += async (_, _) => await UpdateCatalogAsync();
        _clearFiltersButton.Clicked += (_, _) => ClearFilters();
        _filterButton.Clicked += async (_, _) => await ShowFiltersAsync();

        var list = new CollectionView
        {
            ItemsSource = _results,
            ItemTemplate = new DataTemplate(CreatePlayerCard),
            SelectionMode = SelectionMode.None,
            ItemsLayout = new LinearItemsLayout(ItemsLayoutOrientation.Vertical) { ItemSpacing = 2 },
            Margin = new Thickness(0, 0, 0, 8)
        };

        var filterBar = new Grid
        {
            ColumnDefinitions = { new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto) },
            ColumnSpacing = 8,
            Margin = new Thickness(16, 0, 16, 4),
            Children = { _filterButton.Column(0), _filterSummary.Column(1), _clearFiltersButton.Column(2) }
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
        var updateBar = new HorizontalStackLayout
        {
            Spacing = 8,
            HorizontalOptions = LayoutOptions.End,
            Margin = new Thickness(16, 0, 16, 4),
            Children = { _updateActivity, _updateButton }
        };
        var emptyState = new VerticalStackLayout
        {
            Spacing = 12,
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.Center,
            Children = { _empty, _retryButton }
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
                new RowDefinition(GridLength.Star),
            },
            Children =
            {
                PageStyles.NavigationTitle(this, "選手検索"),
                new Border
                {
                    BackgroundColor = PageStyles.Surface,
                    Stroke = PageStyles.Stroke,
                    StrokeThickness = 1,
                    StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 12 },
                    Padding = new Thickness(8, 0),
                    Margin = new Thickness(16, 0, 16, 4),
                    Content = _searchBar
                }.Row(1),
                filterBar.Row(2),
                resultHeader.Row(3),
                updateBar.Row(4),
                list.Row(5),
                emptyState.Row(5)
            }
        };
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (!_loaded && !_loading) await LoadAsync();
    }

    private async Task LoadAsync()
    {
        if (_loading) return;
        _loading = true;
        _updateButton.IsEnabled = false;
        _retryButton.IsVisible = false;
        _catalogStatus.Text = "選手データを読み込み中...";
        try
        {
            var catalog = await _loader.LoadAsync();
            _players = catalog.Players;
            _loaded = true;
            _filterButton.IsEnabled = true;
            PopulateTeamPicker();
            RefreshResults();
            _catalogStatus.Text = BuildCatalogStatus(catalog);
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
            _updateButton.IsEnabled = true;
        }
    }

    private async Task UpdateCatalogAsync()
    {
        if (_loading || !_updateButton.IsEnabled) return;
        _updateButton.IsEnabled = false;
        _updateActivity.IsVisible = true;
        _updateActivity.IsRunning = true;
        _catalogStatus.Text = "GitHubから選手データを確認しています...";

        try
        {
            var bundledManifestJson = await PlayerCatalogLoader.LoadBundledManifestJsonAsync();
            var result = await _updateService.UpdateAsync(bundledManifestJson);
            Preferences.Set(LastCatalogCheckPreferenceKey, result.CheckedAtUtc.ToString("O"));
            _players = result.Catalog.Players;
            _loaded = true;
            _filterButton.IsEnabled = true;

            if (_selectedDivision.Length > 0 && !_players.Any(player => player.Division == _selectedDivision))
                _selectedDivision = "";
            if (_selectedAffiliationType.Length > 0 &&
                !_players.Any(player => (player.TeamHistory ?? []).Any(history => history is not null && history.Type == _selectedAffiliationType)))
                _selectedAffiliationType = "";

            PopulateTeamPicker();
            if (_positionPicker.SelectedItem is string position && position != AllPositions &&
                !_players.Any(player => HasPosition(player, position)))
                _positionPicker.SelectedIndex = 0;
            RefreshResults();

            var checkedAt = result.CheckedAtUtc.ToLocalTime().ToString("yyyy/M/d H:mm");
            _catalogStatus.Text = $"{result.Catalog.Season}シーズン · 全{_players.Count:N0}人 · {checkedAt}に" +
                (result.Changed ? "更新しました" : "確認済みです") +
                (result.UsedBundledManifest ? " (同梱のファイル一覧)" : "");
        }
        catch (Exception ex)
        {
            var reason = ex switch
            {
                InvalidDataException => "GitHubの選手データを検証できませんでした。",
                HttpRequestException or TaskCanceledException => "通信を確認して、もう一度お試しください。",
                _ => "時間をおいて、もう一度お試しください。"
            };
            _catalogStatus.Text = $"選手データを更新できませんでした。現在の表示は維持されています。{reason}";
        }
        finally
        {
            _updateActivity.IsRunning = false;
            _updateActivity.IsVisible = false;
            _updateButton.IsEnabled = true;
        }
    }

    private static string BuildCatalogStatus(PlayerCatalogDocument catalog)
    {
        var lastCheck = Preferences.Get(LastCatalogCheckPreferenceKey, "");
        if (DateTimeOffset.TryParse(lastCheck, out var checkedAt))
            return $"{catalog.Season}シーズン · 全{catalog.Players.Count:N0}人 · 最終確認 {checkedAt.ToLocalTime():yyyy/M/d H:mm}";
        return $"{catalog.Season}シーズン · 全{catalog.Players.Count:N0}人";
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
        var matches = PlayerSearch.Search(_players, _searchBar.Text)
            .Where(player => _selectedDivision.Length == 0 || player.Division == _selectedDivision)
            .Where(player => string.IsNullOrWhiteSpace(team) || team == AllTeams || player.CurrentTeamName == team)
            .Where(player => string.IsNullOrWhiteSpace(position) || position == AllPositions || HasPosition(player, position))
            .Where(player => string.IsNullOrEmpty(_selectedAffiliationType) ||
                (player.TeamHistory ?? []).Any(history => history is not null && history.Type == _selectedAffiliationType))
            .OrderBy(player => string.IsNullOrWhiteSpace(player.NameJa) ? player.NameEn : player.NameJa, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();

        foreach (var player in matches) _results.Add(player);
        _resultCount.Text = $"{matches.Length:N0}人を表示";
        var activeFilters = ActiveFilterCount();
        _filterButton.Text = activeFilters == 0 ? "絞り込み" : $"絞り込み ({activeFilters})";
        _filterSummary.Text = BuildFilterSummary();
        _empty.IsVisible = matches.Length == 0;
        _clearFiltersButton.IsVisible = HasActiveFilters();
    }

    private bool HasActiveFilters()
        => !string.IsNullOrWhiteSpace(_searchBar.Text) ||
           _selectedDivision.Length > 0 ||
           _selectedAffiliationType.Length > 0 ||
           (_teamPicker.SelectedItem is string team && team != AllTeams) ||
           (_positionPicker.SelectedItem is string position && position != AllPositions);

    private void ClearFilters()
    {
        _searchBar.Text = "";
        _selectedDivision = "";
        _selectedAffiliationType = "";
        _updatingFilters = true;
        PopulateTeamPicker();
        _teamPicker.SelectedIndex = 0;
        _positionPicker.SelectedIndex = 0;
        _updatingFilters = false;
        RefreshResults();
    }

    private int ActiveFilterCount()
        => (_selectedDivision.Length > 0 ? 1 : 0) + (_selectedAffiliationType.Length > 0 ? 1 : 0) +
           (_teamPicker.SelectedItem is string team && team != AllTeams ? 1 : 0) +
           (_positionPicker.SelectedItem is string position && position != AllPositions ? 1 : 0);

    private string BuildFilterSummary()
    {
        var values = new List<string>();
        if (!string.IsNullOrWhiteSpace(_searchBar.Text)) values.Add($"検索: {_searchBar.Text.Trim()}");
        if (_selectedDivision.Length > 0) values.Add(Divisions.First(item => item.Code == _selectedDivision).Label);
        if (_teamPicker.SelectedItem is string team && team != AllTeams) values.Add(team);
        if (_positionPicker.SelectedItem is string position && position != AllPositions) values.Add(position);
        if (_selectedAffiliationType.Length > 0)
            values.Add(_selectedAffiliationType switch { "university" => "大学歴あり", "highSchool" => "高校歴あり", _ => "クラブ歴あり" });
        return values.Count == 0 ? "絞り込み条件はありません" : string.Join(" · ", values);
    }

    private async Task ShowFiltersAsync()
    {
        var selection = new PlayerFilterSelection(
            _selectedDivision,
            _teamPicker.SelectedItem as string is { } team && team != AllTeams ? team : "",
            _positionPicker.SelectedItem as string is { } position && position != AllPositions ? position : "",
            _selectedAffiliationType);
        await Navigation.PushModalAsync(new NavigationPage(new PlayerFiltersPage(_players, selection, selected =>
        {
            _selectedDivision = selected.Division;
            _selectedAffiliationType = selected.AffiliationType;
            _updatingFilters = true;
            PopulateTeamPicker();
            _teamPicker.SelectedIndex = string.IsNullOrEmpty(selected.Team) ? 0 :
                Math.Max(Array.IndexOf(_teamPicker.ItemsSource.Cast<string>().ToArray(), selected.Team), 0);
            _positionPicker.SelectedIndex = string.IsNullOrEmpty(selected.Position) ? 0 :
                Math.Max(Array.IndexOf(_positionPicker.ItemsSource.Cast<string>().ToArray(), selected.Position), 0);
            _updatingFilters = false;
            RefreshResults();
        }))
        {
            BarBackgroundColor = PageStyles.Surface,
            BarTextColor = PageStyles.Navy
        });
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

        return (player.Positions ?? []).Any(value =>
        {
            if (string.IsNullOrWhiteSpace(value)) return false;
            var normalized = NormalizePositionText(value);
            return aliases.Any(alias => normalized.Contains(NormalizePositionText(alias), StringComparison.Ordinal));
        });
    }

    private static string NormalizePositionText(string value)
        => new(value.Normalize().Where(char.IsLetterOrDigit).Select(char.ToUpperInvariant).ToArray());

    internal static string PositionBadgeFor(PlayerRecord player)
        => PositionFilters.FirstOrDefault(position => HasPosition(player, position)) ?? "選手";

    internal static string HistoryTypeLabel(string? type)
        => type switch { "university" => "大学", "highSchool" => "高校", _ => "クラブ" };

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
        => value is PlayerRecord player ? $"{player.CurrentTeamName} · {string.Join(" / ", player.Positions ?? [])}" : "";
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
            ? PlayerTeamHistoryDisplayGroup.Create(player)
                .Where(group => group.Type != "club" ||
                    !group.Stints.Any(history => history.TeamId == player.CurrentTeamId || history.TeamName == player.CurrentTeamName))
                .OrderByDescending(group => group.Stints.Max(history => SeasonYear(
                    string.IsNullOrWhiteSpace(history.FromSeason) ? history.ToSeason : history.FromSeason)))
                .Select(group => $"{PlayersPage.HistoryTypeLabel(group.Type)} {group.TeamName} ({group.Periods})")
                .Take(2).ToArray()
            : [];
    private static int SeasonYear(string? season)
        => !string.IsNullOrWhiteSpace(season) && int.TryParse(season.AsSpan(0, Math.Min(4, season.Length)), out var year) ? year : 0;
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

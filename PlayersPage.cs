using System.Collections.ObjectModel;

namespace OneRugbyNavi2;

public sealed class PlayersPage : ContentPage
{
    private readonly PlayerCatalogLoader _loader = new();
    private readonly ObservableCollection<PlayerRecord> _results = [];
    private IReadOnlyList<PlayerRecord> _players = [];
    private readonly SearchBar _searchBar = new() { Placeholder = "選手名、所属、ポジション、出身校など" };
    private readonly Label _status = PageStyles.MutedLabel("選手データを読み込み中...");
    private readonly Label _empty = new()
    {
        Text = "該当する選手はいません。",
        TextColor = PageStyles.Muted,
        HorizontalTextAlignment = TextAlignment.Center,
        VerticalTextAlignment = TextAlignment.Center,
        IsVisible = false
    };
    private bool _loaded;
    private bool _loading;

    public PlayersPage()
    {
        Title = "選手検索";
        BackgroundColor = PageStyles.Background;
        Shell.SetNavBarIsVisible(this, false);
        _searchBar.TextChanged += OnSearchTextChanged;

        var list = new CollectionView
        {
            ItemsSource = _results,
            ItemTemplate = new DataTemplate(CreatePlayerCard),
            SelectionMode = SelectionMode.None
        };

        Content = new Grid
        {
            RowDefinitions =
            {
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Star)
            },
            Children =
            {
                PageStyles.NavigationTitle(this, "選手検索"),
                _searchBar.Row(1).Margin(new Thickness(16, 0, 16, 8)),
                _status.Row(2).Margin(new Thickness(16, 0, 16, 8)),
                list.Row(3),
                _empty.Row(3)
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
        _loading = true;
        _status.Text = "選手データを読み込み中...";
        try
        {
            var catalog = await _loader.LoadAsync();
            _players = catalog.Players;
            _loaded = true;
            RefreshResults(_searchBar.Text);
            _status.Text = _players.Count == 0
                ? $"{catalog.Season}シーズンの選手データはまだ登録されていません。"
                : $"{catalog.Season}シーズン: {_results.Count}名";
        }
        catch (Exception ex)
        {
            _status.Text = $"選手データを読み込めませんでした: {ex.Message}";
        }
        finally
        {
            _loading = false;
        }
    }

    private void OnSearchTextChanged(object? sender, TextChangedEventArgs e)
    {
        RefreshResults(e.NewTextValue);
        _status.Text = _players.Count == 0 ? "選手データはありません。" : $"検索結果: {_results.Count}名";
    }

    private void RefreshResults(string? query)
    {
        _results.Clear();
        foreach (var player in PlayerSearch.Search(_players, query)) _results.Add(player);
        _empty.IsVisible = _players.Count > 0 && _results.Count == 0;
    }

    private View CreatePlayerCard()
    {
        var portrait = new Image
        {
            WidthRequest = 64,
            HeightRequest = 86,
            Aspect = Aspect.AspectFill,
            BackgroundColor = PageStyles.ChipBackground
        };
        portrait.BindingContextChanged += (_, _) =>
        {
            portrait.Source = portrait.BindingContext is PlayerRecord p
                ? AssetImageResolver.CreateImageSource(p.Portrait?.AssetPath)
                : null;
        };

        var name = new Label { FontSize = 16, FontAttributes = FontAttributes.Bold, TextColor = PageStyles.Navy };
        name.BindingContextChanged += (_, _) =>
            name.Text = name.BindingContext is PlayerRecord p ? (string.IsNullOrWhiteSpace(p.NameJa) ? p.NameEn : p.NameJa) : "";

        var meta = new Label { FontSize = 13, TextColor = PageStyles.Muted, LineBreakMode = LineBreakMode.WordWrap };
        meta.BindingContextChanged += (_, _) =>
            meta.Text = meta.BindingContext is PlayerRecord p ? $"{p.CurrentTeamName} · {string.Join("/", p.Positions)}" : "";

        var row = new Grid
        {
            ColumnDefinitions = { new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Star) },
            ColumnSpacing = 12,
            Children =
            {
                portrait.Column(0),
                new VerticalStackLayout { Spacing = 4, Children = { name, meta } }.Column(1)
            }
        };
        var card = PageStyles.Card(row);
        card.SetBinding(BindableObject.BindingContextProperty, ".");
        var tap = new TapGestureRecognizer();
        tap.Tapped += async (_, _) =>
        {
            if (card.BindingContext is PlayerRecord player) await ShowPlayerAsync(player);
        };
        card.GestureRecognizers.Add(tap);
        return card;
    }

    private async Task ShowPlayerAsync(PlayerRecord player)
    {
        var lines = new List<string> { $"{player.CurrentTeamName} ({player.Division})", $"ポジション: {string.Join("/", player.Positions)}" };
        if (!string.IsNullOrWhiteSpace(player.NameEn)) lines.Add($"英語名: {player.NameEn}");

        if (player.BirthDate is { } birthDate)
        {
            var today = DateOnly.FromDateTime(DateTime.Today);
            var age = today.Year - birthDate.Year;
            if (birthDate > today.AddYears(-age)) age--;
            lines.Add($"生年月日: {birthDate:yyyy-MM-dd} ({age}歳)");
        }

        if (player.HeightCm.HasValue || player.WeightKg.HasValue)
            lines.Add($"身長/体重: {(player.HeightCm is { } h ? $"{h}cm" : "-")} / {(player.WeightKg is { } w ? $"{w}kg" : "-")}");

        if (player.Schools.Count > 0) lines.Add($"出身校: {string.Join(" / ", player.Schools)}");
        if (player.TeamHistory.Count > 0)
        {
            lines.Add("所属歴:");
            lines.AddRange(player.TeamHistory.Select(item => $"・{item.TeamName} ({item.FromSeason}～{item.ToSeason})"));
        }

        if (player.RepresentativeHistory.Count > 0)
        {
            lines.Add("代表歴:");
            lines.AddRange(player.RepresentativeHistory.Select(item =>
                $"・{item.TeamName}{(item.Caps is { } caps ? $" ({caps} caps)" : "")}{(item.Seasons.Count > 0 ? $" [{string.Join(", ", item.Seasons)}]" : "")}"));
        }

        if (player.Sources.Count > 0)
        {
            lines.Add("出典:");
            lines.AddRange(player.Sources.Select(source => $"・{source.Publisher}: {source.Url}"));
        }

        var title = string.IsNullOrWhiteSpace(player.NameJa) ? player.NameEn : player.NameJa;
        await DisplayAlert(title, string.Join(Environment.NewLine, lines), "閉じる");
    }
}
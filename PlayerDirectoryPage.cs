using System.Collections.ObjectModel;

namespace OneRugbyNavi2;

public sealed class PlayerDirectoryPage : ContentPage
{
    private readonly ObservableCollection<PlayerCard> _players = new();
    private readonly List<TeamCard> _teamOptions = new();
    private readonly Label _status = PageStyles.MutedLabel("読み込み中...");
    private readonly Entry _keyword = PageStyles.Entry("名前・チーム・ポジション・出身校で検索");
    private readonly Entry _schoolKeyword = PageStyles.Entry("出身校・チーム歴で検索");
    private readonly Picker _sortPicker = PageStyles.Picker("並び替え");
    private readonly Picker _teamPicker = PageStyles.Picker("チーム");
    private readonly Picker _positionPicker = PageStyles.Picker("ポジション");

    private readonly int? _initialTeamId;
    private readonly string? _initialTeamName;
    private bool _filtersLoaded;

    public PlayerDirectoryPage(int? initialTeamId = null, string? initialTeamName = null)
    {
        _initialTeamId = initialTeamId;
        _initialTeamName = initialTeamName;
        Title = string.IsNullOrWhiteSpace(initialTeamName) ? "選手" : $"{initialTeamName} 選手";
        BackgroundColor = PageStyles.Background;

        _sortPicker.ItemsSource = new[] { "名前", "チーム", "ポジション", "身長", "体重", "年齢", "キャップ数" };
        _sortPicker.SelectedIndex = 0;
        _sortPicker.SelectedIndexChanged += async (_, _) => await LoadAsync();
        _teamPicker.SelectedIndexChanged += async (_, _) => await LoadAsync();
        _positionPicker.SelectedIndexChanged += async (_, _) => await LoadAsync();
        _keyword.Completed += async (_, _) => await LoadAsync();
        _schoolKeyword.Completed += async (_, _) => await LoadAsync();

        var search = new Button
        {
            Text = "検索",
            BackgroundColor = PageStyles.Blue,
            TextColor = Colors.White,
            CornerRadius = 12
        };
        search.Clicked += async (_, _) => await LoadAsync();

        var clear = new Button
        {
            Text = "クリア",
            BackgroundColor = Color.FromArgb("#EDF3FF"),
            TextColor = PageStyles.Blue,
            CornerRadius = 12
        };
        clear.Clicked += async (_, _) =>
        {
            _keyword.Text = "";
            _schoolKeyword.Text = "";
            _teamPicker.SelectedIndex = 0;
            _positionPicker.SelectedIndex = 0;
            await LoadAsync();
        };

        var list = new CollectionView
        {
            ItemsSource = _players,
            SelectionMode = SelectionMode.Single,
            ItemTemplate = new DataTemplate(CreatePlayerCard)
        };
        list.SelectionChanged += async (_, e) =>
        {
            if (e.CurrentSelection.FirstOrDefault() is PlayerCard player)
            {
                list.SelectedItem = null;
                await Navigation.PushAsync(new PlayerDetailPage(player.Id));
            }
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
                new RowDefinition(GridLength.Star)
            },
            Children =
            {
                PageStyles.Title(string.IsNullOrWhiteSpace(initialTeamName) ? "選手名鑑" : $"{initialTeamName} 所属選手"),
                new Grid
                {
                    ColumnDefinitions =
                    {
                        new ColumnDefinition(GridLength.Star),
                        new ColumnDefinition(GridLength.Auto),
                        new ColumnDefinition(GridLength.Auto)
                    },
                    Margin = new Thickness(16, 0, 16, 8),
                    ColumnSpacing = 8,
                    Children = { _keyword.Column(0), search.Column(1), clear.Column(2) }
                }.Row(1),
                new Grid
                {
                    ColumnDefinitions =
                    {
                        new ColumnDefinition(GridLength.Star),
                        new ColumnDefinition(GridLength.Star)
                    },
                    Margin = new Thickness(16, 0, 16, 8),
                    ColumnSpacing = 8,
                    Children = { _teamPicker.Column(0), _positionPicker.Column(1) }
                }.Row(2),
                _schoolKeyword.Row(3).Margin(new Thickness(16, 0, 16, 8)),
                new Grid
                {
                    ColumnDefinitions =
                    {
                        new ColumnDefinition(GridLength.Star),
                        new ColumnDefinition(GridLength.Star)
                    },
                    Margin = new Thickness(16, 0, 16, 8),
                    ColumnSpacing = 8,
                    Children = { _sortPicker.Column(0), _status.Column(1).CenterVertical() }
                }.Row(4),
                list.Row(5)
            }
        };
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (!_filtersLoaded)
        {
            await LoadFiltersAsync();
        }

        if (_players.Count == 0)
        {
            await LoadAsync();
        }
    }

    private async Task LoadFiltersAsync()
    {
        _filtersLoaded = true;
        _teamOptions.Clear();
        _teamOptions.AddRange(await AppServices.Database.GetTeamsAsync());
        _teamPicker.ItemsSource = new[] { "すべて" }.Concat(_teamOptions.Select(team => team.TeamName)).ToArray();

        var positions = await AppServices.Database.GetPlayerPositionsAsync();
        _positionPicker.ItemsSource = new[] { "すべて" }.Concat(positions).ToArray();

        _teamPicker.SelectedIndex = GetInitialTeamIndex();
        _positionPicker.SelectedIndex = 0;
    }

    private int GetInitialTeamIndex()
    {
        if (!_initialTeamId.HasValue)
        {
            return 0;
        }

        var index = _teamOptions.FindIndex(team => team.Id == _initialTeamId.Value);
        return index < 0 ? 0 : index + 1;
    }

    private async Task LoadAsync()
    {
        if (!_filtersLoaded)
        {
            return;
        }

        try
        {
            _players.Clear();
            var teamId = SelectedTeamId();
            var position = SelectedPickerValue(_positionPicker);
            foreach (var player in await AppServices.Database.GetPlayersAsync(
                _keyword.Text,
                SortKey(),
                teamId,
                position,
                _schoolKeyword.Text))
            {
                _players.Add(player);
            }

            _status.Text = $"検索結果: {_players.Count}人";
        }
        catch (Exception ex)
        {
            _status.Text = $"読み込みに失敗しました: {ex.Message}";
        }
    }

    private int? SelectedTeamId()
    {
        if (_teamPicker.SelectedIndex <= 0)
        {
            return null;
        }

        var index = _teamPicker.SelectedIndex - 1;
        return index >= 0 && index < _teamOptions.Count ? _teamOptions[index].Id : null;
    }

    private static string? SelectedPickerValue(Picker picker)
    {
        var value = picker.SelectedItem as string;
        return string.IsNullOrWhiteSpace(value) || value == "すべて" ? null : value;
    }

    private string SortKey() => _sortPicker.SelectedIndex switch
    {
        1 => "team",
        2 => "position",
        3 => "height_desc",
        4 => "weight_desc",
        5 => "age_desc",
        6 => "caps_desc",
        _ => "name"
    };

    public static View CreatePlayerCard()
    {
        var photo = new Image { WidthRequest = 60, HeightRequest = 60, Aspect = Aspect.AspectFill };
        photo.SetBinding(Image.SourceProperty, nameof(PlayerCard.PhotoSource));

        var badge = new Label
        {
            WidthRequest = 60,
            HeightRequest = 60,
            HorizontalTextAlignment = TextAlignment.Center,
            VerticalTextAlignment = TextAlignment.Center,
            BackgroundColor = Color.FromArgb("#EDF3FF"),
            TextColor = PageStyles.Blue,
            FontAttributes = FontAttributes.Bold
        };
        badge.SetBinding(Label.TextProperty, nameof(PlayerCard.Initials));

        var photoLayer = new Grid { WidthRequest = 60, HeightRequest = 60 };
        photoLayer.Children.Add(badge);
        photoLayer.Children.Add(photo);

        var name = new Label { FontSize = 16, FontAttributes = FontAttributes.Bold, TextColor = PageStyles.Navy };
        name.SetBinding(Label.TextProperty, nameof(PlayerCard.NameJa));

        var team = PageStyles.MutedLabel();
        team.SetBinding(Label.TextProperty, nameof(PlayerCard.TeamName));

        var position = PageStyles.Chip("");
        position.SetBinding(Label.TextProperty, nameof(PlayerCard.PositionCode));

        var size = PageStyles.MutedLabel();
        size.SetBinding(Label.TextProperty, nameof(PlayerCard.SizeText));

        var caps = PageStyles.MutedLabel();
        caps.SetBinding(Label.TextProperty, nameof(PlayerCard.CapsText));

        var school = PageStyles.MutedLabel();
        school.LineBreakMode = LineBreakMode.TailTruncation;
        school.SetBinding(Label.TextProperty, nameof(PlayerCard.SchoolTeamHistoryText));

        var text = new VerticalStackLayout
        {
            Spacing = 4,
            Children =
            {
                name,
                team,
                new HorizontalStackLayout { Spacing = 8, Children = { position, size, caps } },
                school
            }
        };

        return PageStyles.Card(new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Auto),
                new ColumnDefinition(GridLength.Star)
            },
            ColumnSpacing = 12,
            Children = { photoLayer.Column(0), text.Column(1) }
        });
    }
}

using System.Collections.ObjectModel;

namespace OneRugbyNavi2;

public sealed class PlayerDirectoryPage : ContentPage
{
    private readonly ObservableCollection<PlayerCard> _players = new();
    private readonly Label _status = PageStyles.MutedLabel("読み込み中...");
    private readonly Picker _sortPicker = new() { Title = "並び替え" };

    public PlayerDirectoryPage()
    {
        Title = "選手";
        BackgroundColor = PageStyles.Background;

        _sortPicker.ItemsSource = new[] { "名前", "チーム", "ポジション", "身長", "体重", "年齢", "キャップ数" };
        _sortPicker.SelectedIndex = 0;
        _sortPicker.SelectedIndexChanged += async (_, _) => await LoadAsync();

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
                new RowDefinition(GridLength.Star)
            },
            Children =
            {
                PageStyles.Title("選手名鑑"),
                _sortPicker.Row(1).Margin(new Thickness(16, 0, 16, 8)),
                _status.Row(2).Margin(new Thickness(16, 0, 16, 8)),
                list.Row(3)
            }
        };
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (_players.Count == 0)
        {
            await LoadAsync();
        }
    }

    private async Task LoadAsync()
    {
        try
        {
            _players.Clear();
            foreach (var player in await AppServices.Database.GetPlayersAsync(sort: SortKey()))
            {
                _players.Add(player);
            }

            _status.Text = $"{_players.Count}人";
        }
        catch (Exception ex)
        {
            _status.Text = $"読み込みに失敗しました: {ex.Message}";
        }
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

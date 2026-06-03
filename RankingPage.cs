using System.Collections.ObjectModel;

namespace OneRugbyNavi2;

public sealed class RankingPage : ContentPage
{
    private readonly ObservableCollection<RankingRow> _rows = new();
    private readonly Picker _rankingPicker = PageStyles.Picker("並び替え");
    private readonly Picker _directionPicker = PageStyles.Picker("並び順");
    private readonly Label _status = PageStyles.MutedLabel("読み込み中...");

    public RankingPage()
    {
        Title = "ランキング";
        BackgroundColor = PageStyles.Background;
        Shell.SetNavBarIsVisible(this, false);

        _rankingPicker.ItemsSource = new[] { "並び替え: 身長", "並び替え: 体重", "並び替え: 年齢", "並び替え: キャップ数", "並び替え: 出身校候補人数" };
        _rankingPicker.SelectedIndex = 0;
        _rankingPicker.SelectedIndexChanged += async (_, _) => await LoadAsync();

        _directionPicker.ItemsSource = new[] { "並び順: 降順", "並び順: 昇順" };
        _directionPicker.SelectedIndex = 0;
        _directionPicker.SelectedIndexChanged += async (_, _) => await LoadAsync();

        var list = new CollectionView
        {
            ItemsSource = _rows,
            SelectionMode = SelectionMode.Single,
            ItemTemplate = new DataTemplate(CreateRankingCard)
        };
        list.SelectionChanged += async (_, e) =>
        {
            if (e.CurrentSelection.FirstOrDefault() is RankingRow { PlayerId: int playerId })
            {
                list.SelectedItem = null;
                await Navigation.PushAsync(new PlayerDetailPage(playerId));
            }
            else
            {
                list.SelectedItem = null;
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
                PageStyles.Title("ランキング"),
                new Grid
                {
                    ColumnDefinitions =
                    {
                        new ColumnDefinition(GridLength.Star),
                        new ColumnDefinition(GridLength.Star)
                    },
                    Margin = new Thickness(16, 0, 16, 8),
                    ColumnSpacing = 8,
                    Children = { _rankingPicker.Column(0), _directionPicker.Column(1) }
                }.Row(1),
                _status.Row(2).Margin(new Thickness(16, 0, 16, 8)),
                list.Row(3)
            }
        };
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (_rows.Count == 0)
        {
            await LoadAsync();
        }
    }

    private async Task LoadAsync()
    {
        try
        {
            _rows.Clear();
            var isSchoolCount = RankingKey() == "school_count";
            _directionPicker.IsEnabled = !isSchoolCount;

            foreach (var row in await AppServices.Database.GetRankingAsync(RankingKey(), _directionPicker.SelectedIndex == 0))
            {
                _rows.Add(row);
            }

            _status.Text = $"{_rows.Count}件";
        }
        catch (Exception ex)
        {
            _status.Text = $"読み込みに失敗しました: {ex.Message}";
        }
    }

    private string RankingKey() => _rankingPicker.SelectedIndex switch
    {
        1 => "weight",
        2 => "age",
        3 => "caps",
        4 => "school_count",
        _ => "height"
    };

    private static View CreateRankingCard()
    {
        var rank = new Label
        {
            FontSize = 20,
            FontAttributes = FontAttributes.Bold,
            TextColor = PageStyles.Blue,
            WidthRequest = 44,
            VerticalTextAlignment = TextAlignment.Center
        };
        rank.SetBinding(Label.TextProperty, nameof(RankingRow.Rank));

        var photo = new Image { WidthRequest = 52, HeightRequest = 52, Aspect = Aspect.AspectFill };
        photo.SetBinding(Image.SourceProperty, nameof(RankingRow.PhotoSource));

        var badge = new Label
        {
            WidthRequest = 52,
            HeightRequest = 52,
            HorizontalTextAlignment = TextAlignment.Center,
            VerticalTextAlignment = TextAlignment.Center,
            BackgroundColor = Color.FromArgb("#EDF3FF"),
            TextColor = PageStyles.Blue,
            FontAttributes = FontAttributes.Bold
        };
        badge.SetBinding(Label.TextProperty, nameof(RankingRow.Initials));

        var photoLayer = new Grid { WidthRequest = 52, HeightRequest = 52 };
        photoLayer.Children.Add(badge);
        photoLayer.Children.Add(photo);

        var title = new Label { FontSize = 16, FontAttributes = FontAttributes.Bold, TextColor = PageStyles.Navy };
        title.SetBinding(Label.TextProperty, nameof(RankingRow.Title));

        var subtitle = PageStyles.MutedLabel();
        subtitle.SetBinding(Label.TextProperty, nameof(RankingRow.Subtitle));

        var value = PageStyles.Chip("");
        value.SetBinding(Label.TextProperty, nameof(RankingRow.ValueText));

        return PageStyles.Card(new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Auto),
                new ColumnDefinition(GridLength.Auto),
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto)
            },
            ColumnSpacing = 10,
            Children =
            {
                rank.Column(0),
                photoLayer.Column(1),
                new VerticalStackLayout { Children = { title, subtitle } }.Column(2),
                value.Column(3).CenterVertical()
            }
        });
    }
}

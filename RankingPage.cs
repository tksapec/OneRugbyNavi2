using System.Collections.ObjectModel;

namespace OneRugbyNavi2;

public sealed class RankingPage : ContentPage
{
    private readonly ObservableCollection<RankingRow> _rows = new();
    private readonly Picker _rankingPicker = new() { Title = "ランキング" };
    private readonly Label _status = PageStyles.MutedLabel("読み込み中...");

    public RankingPage()
    {
        Title = "ランキング";
        BackgroundColor = PageStyles.Background;

        _rankingPicker.ItemsSource = new[] { "身長順", "体重順", "年齢順", "キャップ数順", "出身校候補人数順" };
        _rankingPicker.SelectedIndex = 0;
        _rankingPicker.SelectedIndexChanged += async (_, _) => await LoadAsync();

        var list = new CollectionView
        {
            ItemsSource = _rows,
            ItemTemplate = new DataTemplate(CreateRankingCard)
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
                _rankingPicker.Row(1).Margin(new Thickness(16, 0, 16, 8)),
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
            foreach (var row in await AppServices.Database.GetRankingAsync(RankingKey()))
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
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto)
            },
            ColumnSpacing = 10,
            Children =
            {
                rank.Column(0),
                new VerticalStackLayout { Children = { title, subtitle } }.Column(1),
                value.Column(2).CenterVertical()
            }
        });
    }
}

using System.Collections.ObjectModel;

namespace OneRugbyNavi2;

public sealed class SchoolSearchPage : ContentPage
{
    private readonly ObservableCollection<PlayerCard> _players = new();
    private readonly Entry _keyword = new() { Placeholder = "出身校・チーム歴で検索" };
    private readonly Picker _sortPicker = PageStyles.Picker("並び替え");
    private readonly Label _status = PageStyles.MutedLabel("検索語を入力してください");

    public SchoolSearchPage()
    {
        Title = "出身校";
        BackgroundColor = PageStyles.Background;

        _sortPicker.ItemsSource = new[] { "名前", "チーム", "ポジション", "身長", "体重", "年齢", "キャップ数" };
        _sortPicker.SelectedIndex = 1;
        _sortPicker.SelectedIndexChanged += async (_, _) => await SearchAsync();

        var search = new Button
        {
            Text = "検索",
            BackgroundColor = PageStyles.Blue,
            TextColor = Colors.White,
            CornerRadius = 12
        };
        search.Clicked += async (_, _) => await SearchAsync();
        _keyword.Completed += async (_, _) => await SearchAsync();

        var list = new CollectionView
        {
            ItemsSource = _players,
            ItemTemplate = new DataTemplate(PlayerDirectoryPage.CreatePlayerCard),
            SelectionMode = SelectionMode.Single
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
                new RowDefinition(GridLength.Star)
            },
            Children =
            {
                PageStyles.Title("出身校・チーム歴検索"),
                new Grid
                {
                    ColumnDefinitions =
                    {
                        new ColumnDefinition(GridLength.Star),
                        new ColumnDefinition(GridLength.Auto)
                    },
                    Margin = new Thickness(16, 0, 16, 8),
                    ColumnSpacing = 8,
                    Children = { _keyword.Column(0), search.Column(1) }
                }.Row(1),
                _sortPicker.Row(2).Margin(new Thickness(16, 0, 16, 8)),
                _status.Row(3).Margin(new Thickness(16, 0, 16, 8)),
                list.Row(4)
            }
        };
    }

    private async Task SearchAsync()
    {
        var keyword = _keyword.Text?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(keyword))
        {
            _players.Clear();
            _status.Text = "検索語を入力してください";
            return;
        }

        try
        {
            _players.Clear();
            foreach (var player in await AppServices.Database.GetPlayersAsync(sort: SortKey(), schoolKeyword: keyword))
            {
                _players.Add(player);
            }

            _status.Text = $"検索結果: {_players.Count}人";
        }
        catch (Exception ex)
        {
            _status.Text = $"検索に失敗しました: {ex.Message}";
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
}

namespace OneRugbyNavi2;

public sealed class InfoPage : ContentPage
{
    private readonly VerticalStackLayout _body = new() { Spacing = 10 };

    public InfoPage()
    {
        Title = "情報";
        BackgroundColor = PageStyles.Background;
        Content = new ScrollView
        {
            Content = new VerticalStackLayout
            {
                Children =
                {
                    PageStyles.Title("One Rugby Navi2"),
                    _body
                }
            }
        };
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadAsync();
    }

    private async Task LoadAsync()
    {
        _body.Children.Clear();
        try
        {
            var summary = await AppServices.Database.GetSummaryAsync();
            _body.Children.Add(PageStyles.Card(new VerticalStackLayout
            {
                Spacing = 8,
                Children =
                {
                    Line("DB", AppServices.Database.GetDatabasePath()),
                    Line("チーム", $"{summary.Teams}件"),
                    Line("選手", $"{summary.Players}件"),
                    Line("試合", $"{summary.Matches}件"),
                    Line("画像/asset", $"{summary.Assets}件"),
                    Line("生成日時", string.IsNullOrWhiteSpace(summary.GeneratedAt) ? "-" : summary.GeneratedAt)
                }
            }));

            _body.Children.Add(PageStyles.Card(new Label
            {
                Text = "起動時は同梱DBをAppDataへ初回コピーし、以後はローカルDBを優先表示します。全件一括更新は行わず、チーム画面からチーム単位で更新します。",
                TextColor = PageStyles.Navy,
                FontSize = 14
            }));
        }
        catch (Exception ex)
        {
            _body.Children.Add(PageStyles.Card(new Label
            {
                Text = $"情報の読み込みに失敗しました: {ex.Message}",
                TextColor = Colors.DarkRed
            }));
        }
    }

    private static View Line(string title, string value) => new VerticalStackLayout
    {
        Spacing = 2,
        Children =
        {
            new Label { Text = title, FontSize = 12, TextColor = PageStyles.Muted },
            new Label { Text = value, FontSize = 15, TextColor = PageStyles.Navy }
        }
    };
}

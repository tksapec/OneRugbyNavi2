namespace OneRugbyNavi2;

public sealed class RankingPage : ContentPage
{
    private readonly WebView _rankingView;
    private readonly Label _status;
    private bool _loaded;

    public RankingPage()
    {
        Title = "公式ランキング";
        BackgroundColor = PageStyles.Background;
        Shell.SetNavBarIsVisible(this, false);

        _status = PageStyles.MutedLabel("League One公式ランキングを読み込んでいます。");
        _rankingView = new WebView
        {
            BackgroundColor = Colors.White,
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Fill
        };
        _rankingView.Navigated += (_, e) =>
        {
            _status.Text = e.Result == WebNavigationResult.Success
                ? "公式ページ内でDivision・シーズン・ランキング項目を選択できます。"
                : "公式ランキングを読み込めませんでした。通信状態を確認してください。";
        };

        Content = new Grid
        {
            RowDefinitions = { new RowDefinition(GridLength.Auto), new RowDefinition(GridLength.Star) },
            Children =
            {
                new VerticalStackLayout
                {
                    Spacing = 4,
                    Children =
                    {
                        PageStyles.NavigationTitle(this, "公式ランキング"),
                        _status.Margin(new Thickness(16, 0, 16, 8))
                    }
                },
                _rankingView.Row(1)
            }
        };
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        if (_loaded) return;

        var seasonYear = SeasonCatalog.CurrentSeasonStartYear;
        _rankingView.Source = new UrlWebViewSource { Url = OfficialRankingUrl.ForSeason(seasonYear) };
        _loaded = true;
    }
}

using System.Collections.ObjectModel;
using Microsoft.Maui.ApplicationModel;

namespace OneRugbyNavi2;

public sealed class TeamListPage : ContentPage
{
    private readonly TeamCatalogFetcher _catalogFetcher = new();

    private readonly ObservableCollection<TeamCard> _teams = new();
    private readonly SemaphoreSlim _loadGate = new(1, 1);
    private readonly Label _status = PageStyles.MutedLabel("読み込み中...");

    public TeamListPage()
    {
        Title = "チーム";
        BackgroundColor = PageStyles.Background;
        Shell.SetNavBarIsVisible(this, false);

        var list = new CollectionView
        {
            ItemsSource = _teams,
            ItemTemplate = new DataTemplate(CreateTeamCard)
        };

        Content = new Grid
        {
            RowDefinitions =
            {
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Star)
            },
            Children =
            {
                PageStyles.NavigationTitle(this, "チーム一覧"),
                _status.Row(1).Margin(new Thickness(16, 0, 16, 8)),
                list.Row(2)
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
        if (!await _loadGate.WaitAsync(0)) return;
        try
        {
            _teams.Clear();
            var seasonYear = SeasonCatalog.CurrentSeasonStartYear;
            var catalog = await _catalogFetcher.GetAsync(seasonYear);
            var displayTeams = await BuildTeamCardsAsync(catalog.Snapshot);

            foreach (var team in displayTeams)
            {
                _teams.Add(team);
            }

            var seasonLabel = SeasonCatalog.ToSeasonUiLabel(seasonYear.ToString());
            var sourceLabel = catalog.UsedFallback ? "保存済み/内蔵カタログ" : "公式サイト";
            _status.Text = _teams.Count == 0
                ? $"{seasonLabel}: 公式チーム情報を確認できません"
                : $"{seasonLabel}: {sourceLabel}から{_teams.Count}チーム";
        }
        catch (Exception ex)
        {
            _status.Text = $"読み込みに失敗しました: {ex.Message}";
        }
        finally
        {
            _loadGate.Release();
        }
    }

    private static async Task<IReadOnlyList<TeamCard>> BuildTeamCardsAsync(TeamIndexResult catalog)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        var cards = await Task.WhenAll(catalog.Teams.Select(async official =>
        {
            var imagePath = ImageAssetCache.GetCachedPath(official.LeagueOneTeamId);
            if (imagePath is null)
            {
                try
                {
                    imagePath = await ImageAssetCache.FetchAndCacheAsync(
                        official.LogoUrl,
                        official.LeagueOneTeamId,
                        timeout.Token);
                }
                catch (OperationCanceledException) when (timeout.IsCancellationRequested)
                {
                    // Keep the URL-backed image or initials badge if the batch
                    // cache exceeds the page-load budget.
                }
            }

            return new TeamCard
            {
                LeagueOneTeamId = official.LeagueOneTeamId,
                TeamName = official.TeamName,
                DivisionCode = official.DivisionCode,
                TeamUrl = official.TeamUrl,
                LogoUrl = official.LogoUrl,
                LocalAssetPath = imagePath
            };
        }));

        return cards;
    }

    private View CreateTeamCard()
    {
        var logo = new Image { WidthRequest = 56, HeightRequest = 56, Aspect = Aspect.AspectFit };
        logo.SetBinding(Image.SourceProperty, nameof(TeamCard.LogoSource));

        var badge = new Label
        {
            WidthRequest = 56,
            HeightRequest = 56,
            HorizontalTextAlignment = TextAlignment.Center,
            VerticalTextAlignment = TextAlignment.Center,
            BackgroundColor = PageStyles.ChipBackground,
            TextColor = PageStyles.Blue,
            FontAttributes = FontAttributes.Bold,
            FontSize = 12
        };
        badge.SetBinding(Label.TextProperty, nameof(TeamCard.BadgeText));

        var imageLayer = new Grid { WidthRequest = 56, HeightRequest = 56 };
        imageLayer.Children.Add(badge);
        imageLayer.Children.Add(logo);
        var logoTap = new TapGestureRecognizer();
        logoTap.Tapped += async (_, _) =>
        {
            if (imageLayer.BindingContext is TeamCard team)
            {
                await OpenOfficialTeamPageAsync(team.TeamUrl);
            }
        };
        imageLayer.GestureRecognizers.Add(logoTap);
        SemanticProperties.SetHint(imageLayer, "タップすると公式チームページを開きます");

        var name = new Label { FontSize = 16, FontAttributes = FontAttributes.Bold, TextColor = PageStyles.Navy };
        name.SetBinding(Label.TextProperty, nameof(TeamCard.TeamName));

        var meta = PageStyles.MutedLabel();
        meta.SetBinding(Label.TextProperty, nameof(TeamCard.MetaText));

        var row = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Auto),
                new ColumnDefinition(GridLength.Star)
            },
            ColumnSpacing = 12,
            Children =
            {
                imageLayer.Column(0),
                new VerticalStackLayout { Spacing = 4, Children = { name, meta } }.Column(1)
            }
        };

        var card = PageStyles.Card(row);
        card.SetBinding(BindableObject.BindingContextProperty, ".");
        return card;
    }

    private async Task OpenOfficialTeamPageAsync(string teamUrl)
    {
        if (!TeamOfficialPageLink.TryCreateUri(teamUrl, out var uri))
        {
            await DisplayAlert("確認", "チーム公式ページのURLを確認できません。", "OK");
            return;
        }

        try
        {
            await Browser.Default.OpenAsync(uri, BrowserLaunchMode.SystemPreferred);
        }
        catch (Exception ex)
        {
            await DisplayAlert("確認", $"チーム公式ページを開けませんでした。\n{ex.Message}", "OK");
        }
    }
}

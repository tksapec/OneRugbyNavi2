using System.Collections.ObjectModel;

namespace OneRugbyNavi2;

public sealed class TeamListPage : ContentPage
{
    private const bool ShowUpdatePreparationButton = false;

    private readonly ObservableCollection<TeamCard> _teams = new();
    private readonly Label _status = PageStyles.MutedLabel("読み込み中...");
    private readonly ActivityIndicator _busy = new() { Color = PageStyles.Blue };

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
                PageStyles.Title("チーム一覧"),
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
        try
        {
            _teams.Clear();
            var storedTeams = await AppServices.Database.GetTeamsAsync();
            var displayTeams = SeasonCatalog.CurrentSeasonStartYear == 2026
                ? Build2026SeasonTeams(storedTeams)
                : storedTeams;

            foreach (var team in displayTeams)
            {
                _teams.Add(team);
            }

            _status.Text = SeasonCatalog.CurrentSeasonStartYear == 2026
                ? $"2026-27: {_teams.Count}チーム / 選手情報は同梱DBの収録時点"
                : $"{_teams.Count}チーム";
        }
        catch (Exception ex)
        {
            _status.Text = $"読み込みに失敗しました: {ex.Message}";
        }
    }

    private static IReadOnlyList<TeamCard> Build2026SeasonTeams(IReadOnlyList<TeamCard> storedTeams)
    {
        var sourceByCurrentName = storedTeams
            .GroupBy(team => SeasonCatalog.NormalizeTeamName(team.TeamName, 2026), StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
        var result = new List<TeamCard>(SeasonCatalog.Teams2026.Count);

        foreach (var official in SeasonCatalog.Teams2026)
        {
            sourceByCurrentName.TryGetValue(official.TeamName, out var source);
            var discardLegacyLogo = source != null && SeasonCatalog.HasChangedBranding(source.TeamName);
            result.Add(new TeamCard
            {
                Id = source?.Id ?? 0,
                LeagueOneTeamId = source?.LeagueOneTeamId ?? "",
                TeamName = official.TeamName,
                ShortName = !string.IsNullOrWhiteSpace(official.ShortName) ? official.ShortName : source?.ShortName ?? "",
                DivisionCode = official.DivisionCode,
                AreaText = !string.IsNullOrWhiteSpace(official.AreaText) ? official.AreaText : source?.AreaText ?? "",
                TeamUrl = source?.TeamUrl ?? "",
                LocalAssetPath = discardLegacyLogo ? null : source?.LocalAssetPath
            });
        }

        return result;
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

        var name = new Label { FontSize = 16, FontAttributes = FontAttributes.Bold, TextColor = PageStyles.Navy };
        name.SetBinding(Label.TextProperty, nameof(TeamCard.TeamName));

        var meta = PageStyles.MutedLabel();
        meta.SetBinding(Label.TextProperty, nameof(TeamCard.MetaText));

        var update = new Button
        {
            Text = "更新準備確認",
            BackgroundColor = PageStyles.Blue,
            TextColor = Colors.White,
            Padding = new Thickness(14, 6),
            CornerRadius = 12,
            FontSize = 13,
            IsVisible = ShowUpdatePreparationButton
        };
        update.SetBinding(BindableObject.BindingContextProperty, ".");
        update.Clicked += OnUpdateClicked;

        var row = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Auto),
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto)
            },
            ColumnSpacing = 12,
            Children =
            {
                imageLayer.Column(0),
                new VerticalStackLayout { Spacing = 4, Children = { name, meta } }.Column(1),
                update.Column(2).CenterVertical()
            }
        };

        var card = PageStyles.Card(row);
        var tap = new TapGestureRecognizer();
        tap.Tapped += async (_, _) =>
        {
            if (card.BindingContext is not TeamCard team)
            {
                return;
            }

            if (team.Id <= 0)
            {
                await DisplayAlert(
                    "選手データ未収録",
                    $"{team.TeamName} は2026-27シーズンの新規チームです。現在の同梱DBには選手データがまだ収録されていません。",
                    "OK");
                return;
            }

            await Navigation.PushAsync(new PlayerDirectoryPage(team.Id, team.TeamName));
        };
        card.SetBinding(BindableObject.BindingContextProperty, ".");
        card.GestureRecognizers.Add(tap);
        return card;
    }

    private async void OnUpdateClicked(object? sender, EventArgs e)
    {
        if (sender is not Button { BindingContext: TeamCard team })
        {
            return;
        }

        var confirmed = await DisplayAlert(
            "確認",
            $"{team.TeamName} の更新準備確認を行います。\n\n実更新は未実装です。DB更新は行いません。現在は一時DB方式の準備確認のみ行います。",
            "確認する",
            "キャンセル");

        if (!confirmed)
        {
            return;
        }

        try
        {
            _busy.IsRunning = true;
            _status.Text = $"{team.TeamName} の更新準備を確認中...";
            var progress = new Progress<string>(message => _status.Text = message);
            var result = await AppServices.TeamUpdater.UpdateTeamAsync(team, progress);
            await DisplayAlert(
                "更新準備確認結果",
                $"対象チーム: {result.TeamName}\n確認済み選手数: {result.UpdatedPlayers}\n新規選手数: {result.NewPlayers}\n削除/未掲載候補: {result.MissingCandidates}\n画像更新数: {result.UpdatedImages}\n取得失敗件数: {result.FailedPages}\n確認日時: {result.UpdatedAt:yyyy-MM-dd HH:mm}\n\n実更新は未実装です。DB更新は行いません。現在は一時DB方式の準備確認のみ行います。\n\n{result.Message}",
                "OK");
            await LoadAsync();
        }
        catch (Exception ex)
        {
            await DisplayAlert("更新準備確認失敗", ex.Message, "OK");
        }
        finally
        {
            _busy.IsRunning = false;
        }
    }
}

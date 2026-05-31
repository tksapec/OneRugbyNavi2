namespace OneRugbyNavi2;

public sealed class PlayerDetailPage : ContentPage
{
    private readonly int _playerId;

    public PlayerDetailPage(int playerId)
    {
        _playerId = playerId;
        Title = "選手詳細";
        BackgroundColor = PageStyles.Background;
        Content = new ActivityIndicator { IsRunning = true, Color = PageStyles.Blue };
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        var player = await AppServices.Database.GetPlayerAsync(_playerId);
        Content = player is null ? PageStyles.Title("選手が見つかりません") : BuildContent(player);
    }

    private static View BuildContent(PlayerCard player)
    {
        var photo = new Image { HeightRequest = 180, Aspect = Aspect.AspectFit };
        photo.Source = player.PhotoSource;

        var badge = new Label
        {
            Text = player.Initials,
            HeightRequest = 120,
            HorizontalTextAlignment = TextAlignment.Center,
            VerticalTextAlignment = TextAlignment.Center,
            BackgroundColor = Color.FromArgb("#EDF3FF"),
            TextColor = PageStyles.Blue,
            FontSize = 34,
            FontAttributes = FontAttributes.Bold
        };

        return new ScrollView
        {
            Content = new VerticalStackLayout
            {
                Spacing = 10,
                Children =
                {
                    PageStyles.Title(player.NameJa),
                    PageStyles.Card(new VerticalStackLayout
                    {
                        Spacing = 10,
                        Children =
                        {
                            player.PhotoSource is null ? badge : photo,
                            Label("英字名", player.NameEn),
                            Label("チーム", player.TeamName),
                            Label("ポジション", player.PositionCode),
                            Label("身長/体重", player.SizeText),
                            Label("生年月日", player.BirthDate),
                            Label("年齢", player.AgeText),
                            Label("登録区分", player.RegistrationCategory),
                            Label("リーグワンキャップ数", player.CapsText),
                            Label("出身校・チーム歴", player.SchoolTeamHistoryText),
                            Label("公式ページ", player.ProfileUrl)
                        }
                    })
                }
            }
        };
    }

    private static View Label(string title, string value) => new VerticalStackLayout
    {
        Spacing = 2,
        Children =
        {
            new Label { Text = title, FontSize = 12, TextColor = PageStyles.Muted },
            new Label { Text = string.IsNullOrWhiteSpace(value) ? "-" : value, FontSize = 15, TextColor = PageStyles.Navy }
        }
    };
}

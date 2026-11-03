using Microsoft.Maui.Controls.Shapes;
using Microsoft.Maui.ApplicationModel;

namespace OneRugbyNavi2;

public static class PageStyles
{
    public static readonly Color Background = Color.FromArgb("#F5F7FB");
    public static readonly Color Surface = Colors.White;
    public static readonly Color Blue = Color.FromArgb("#0057B8");
    public static readonly Color BlueStrong = Color.FromArgb("#0B376D");
    public static readonly Color Navy = Color.FromArgb("#10233F");
    public static readonly Color Text = Color.FromArgb("#172033");
    public static readonly Color Muted = Color.FromArgb("#667085");
    public static readonly Color Stroke = Color.FromArgb("#E5EAF3");
    public static readonly Color ChipBackground = Color.FromArgb("#EDF3FF");
    public static readonly Color InformationBackground = Color.FromArgb("#EAF3FF");
    public static readonly Color InformationStroke = Color.FromArgb("#BBD7FF");
    public static readonly Color WarningBackground = Color.FromArgb("#FFF8E6");
    public static readonly Color WarningStroke = Color.FromArgb("#FFE3A3");
    public static readonly Color WarningText = Color.FromArgb("#7A4E00");

    public static Border Card(View content) => new()
    {
        BackgroundColor = Surface,
        Stroke = Stroke,
        StrokeThickness = 1,
        StrokeShape = new RoundRectangle { CornerRadius = 16 },
        Padding = 14,
        Margin = new Thickness(14, 6),
        Content = content
    };

    public static View WithBottomNavigation(
        ContentPage page,
        View content,
        BottomNavigationSection selected,
        bool isModal = false,
        Func<Task>? moreAction = null)
    {
        var layout = new Grid
        {
            RowDefinitions =
            {
                new RowDefinition(GridLength.Star),
                new RowDefinition(GridLength.Auto)
            }
        };
        layout.Add(content, 0, 0);
        layout.Add(CreateBottomNavigation(page, selected, isModal, moreAction), 0, 1);
        return layout;
    }

    public static View CreateBottomNavigation(
        ContentPage page,
        BottomNavigationSection selected,
        bool isModal = false,
        Func<Task>? moreAction = null)
    {
        moreAction ??= async () =>
        {
            if (isModal)
            {
                await page.Navigation.PopModalAsync();
            }

            var menuPage = Shell.Current.CurrentPage as ContentPage ?? page;
            await ShowNavigationMenuAsync(menuPage);
        };

        var navigation = new Grid
        {
            HeightRequest = 66,
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Star)
            },
            ColumnSpacing = 2,
            Padding = new Thickness(4, 3),
            BackgroundColor = Navy
        };

        AddNavigationButton(navigation, page, BottomNavigationSection.Schedule, "日程", "nav_calendar.svg", selected, isModal, 0);
        AddNavigationButton(navigation, page, BottomNavigationSection.Teams, "チーム", "nav_teams.svg", selected, isModal, 1);
        AddNavigationButton(navigation, page, BottomNavigationSection.Players, "選手", "nav_jersey.svg", selected, isModal, 2);
        AddNavigationButton(navigation, page, BottomNavigationSection.More, "その他", "nav_more.svg", selected, isModal, 3, moreAction);
        return navigation;
    }

    private static void AddNavigationButton(
        Grid navigation,
        ContentPage page,
        BottomNavigationSection section,
        string label,
        string icon,
        BottomNavigationSection selected,
        bool isModal,
        int column,
        Func<Task>? moreAction = null)
    {
        var button = new Button
        {
            Text = label,
            ImageSource = icon,
            ContentLayout = new Button.ButtonContentLayout(Button.ButtonContentLayout.ImagePosition.Top, 2),
            CornerRadius = 12,
            FontAttributes = FontAttributes.Bold,
            FontSize = 12,
            BorderWidth = 0,
            MinimumHeightRequest = 58,
            Padding = new Thickness(4, 2),
            BackgroundColor = selected == section ? Blue : Colors.Transparent,
            TextColor = Colors.White
        };
        SemanticProperties.SetDescription(button, label);
        SemanticProperties.SetHint(button, $"{label}画面へ移動します");
        button.Clicked += async (_, _) =>
        {
            if (section == BottomNavigationSection.More && moreAction is not null)
            {
                await moreAction();
                return;
            }

            await NavigateFromBottomAsync(page, section, isModal);
        };
        navigation.Add(button, column, 0);
    }

    private static async Task NavigateFromBottomAsync(
        ContentPage page,
        BottomNavigationSection section,
        bool isModal)
    {
        if (isModal)
        {
            await page.Navigation.PopModalAsync();
        }

        var route = BottomNavigationRoute.RouteFor(section);
        if (section == BottomNavigationSection.Schedule)
        {
            await Shell.Current.GoToAsync(route);
            return;
        }

        await Shell.Current.GoToAsync("//SchedulePage");
        await Shell.Current.GoToAsync(route);
    }

    public static View NavigationTitle(string text)
    {
        var title = new Label
        {
            Text = text,
            FontSize = 22,
            FontAttributes = FontAttributes.Bold,
            TextColor = Navy,
            FontFamily = "OpenSansSemibold",
            VerticalTextAlignment = TextAlignment.Center,
            LineBreakMode = LineBreakMode.TailTruncation
        };
        return new Grid
        {
            Margin = new Thickness(16, 8, 16, 8),
            Children = { title }
        };
    }

    public static async Task ShowNavigationMenuAsync(
        ContentPage page,
        IReadOnlyDictionary<string, Func<Task>>? additionalActions = null)
    {
        var items = SecondaryNavigationMenu.BuildItems(additionalActions?.Keys);
        var choice = await page.DisplayActionSheet("メニュー", "閉じる", null, items);
        if (string.IsNullOrEmpty(choice)) return;

        if (additionalActions is not null && additionalActions.TryGetValue(choice, out var action))
        {
            await action();
            return;
        }

        if (choice == "順位表")
        {
            await Browser.Default.OpenAsync("https://league-one.jp/standings/");
            return;
        }

        var route = choice switch
        {
            "ランキング" => nameof(RankingPage),
            "アプリ情報" => nameof(InfoPage),
            _ => null
        };
        if (route is null) return;

        await Shell.Current.GoToAsync("//SchedulePage");
        if (route != "//SchedulePage") await Shell.Current.GoToAsync(route);
    }

    public static Label MutedLabel(string text = "") => new()
    {
        Text = text,
        FontSize = 13,
        FontFamily = "OpenSansRegular",
        TextColor = Muted
    };

    public static Label Chip(string text) => new()
    {
        Text = text,
        FontSize = 12,
        FontAttributes = FontAttributes.Bold,
        TextColor = Blue,
        BackgroundColor = ChipBackground,
        FontFamily = "OpenSansSemibold",
        Padding = new Thickness(8, 4)
    };

    public static Picker Picker(string title) => new()
    {
        Title = title,
        TextColor = Navy,
        TitleColor = Muted,
        FontFamily = "OpenSansRegular",
        BackgroundColor = Surface
    };

    public static Button SecondaryButton(string text) => new()
    {
        Text = text,
        TextColor = Blue,
        BackgroundColor = ChipBackground,
        FontFamily = "OpenSansSemibold",
        FontSize = 13,
        CornerRadius = 12,
        Padding = new Thickness(10, 8),
        MinimumHeightRequest = 44,
        BorderWidth = 0
    };

    public static Entry Entry(string placeholder) => new()
    {
        Placeholder = placeholder,
        TextColor = Navy,
        PlaceholderColor = Muted,
        FontFamily = "OpenSansRegular",
        BackgroundColor = Surface
    };
}

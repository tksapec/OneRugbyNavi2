namespace OneRugbyNavi2;

public static class PageViewExtensions
{
    public static T Margin<T>(this T view, Thickness margin) where T : View
    {
        view.Margin = margin;
        return view;
    }

    public static T CenterVertical<T>(this T view) where T : View
    {
        view.VerticalOptions = LayoutOptions.Center;
        return view;
    }
}

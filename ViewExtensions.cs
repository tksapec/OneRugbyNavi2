namespace OneRugbyNavi2;

public static class PageViewExtensions
{
    public static T Apply<T>(this T view, Action<T> configure) where T : BindableObject
    {
        configure(view);
        return view;
    }

    public static T Row<T>(this T view, int row) where T : View
    {
        Grid.SetRow(view, row);
        return view;
    }

    public static T Column<T>(this T view, int column) where T : View
    {
        Grid.SetColumn(view, column);
        return view;
    }

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

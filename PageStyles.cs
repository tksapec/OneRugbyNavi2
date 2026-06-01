using Microsoft.Maui.Controls.Shapes;

namespace OneRugbyNavi2;

public static class PageStyles
{
    public static readonly Color Background = Color.FromArgb("#F5F7FB");
    public static readonly Color Blue = Color.FromArgb("#0057B8");
    public static readonly Color Navy = Color.FromArgb("#10233F");
    public static readonly Color Muted = Color.FromArgb("#667085");
    public static readonly Color Stroke = Color.FromArgb("#E5EAF3");

    public static Border Card(View content) => new()
    {
        BackgroundColor = Colors.White,
        Stroke = Stroke,
        StrokeThickness = 1,
        StrokeShape = new RoundRectangle { CornerRadius = 16 },
        Padding = 14,
        Margin = new Thickness(14, 6),
        Content = content
    };

    public static Label Title(string text) => new()
    {
        Text = text,
        FontSize = 22,
        FontAttributes = FontAttributes.Bold,
        TextColor = Navy,
        Margin = new Thickness(16, 16, 16, 8)
    };

    public static Label MutedLabel(string text = "") => new()
    {
        Text = text,
        FontSize = 13,
        TextColor = Muted
    };

    public static Label Chip(string text) => new()
    {
        Text = text,
        FontSize = 12,
        FontAttributes = FontAttributes.Bold,
        TextColor = Blue,
        BackgroundColor = Color.FromArgb("#EDF3FF"),
        Padding = new Thickness(8, 4)
    };

    public static Picker Picker(string title) => new()
    {
        Title = title,
        TextColor = Navy,
        TitleColor = Muted,
        BackgroundColor = Colors.White
    };
}

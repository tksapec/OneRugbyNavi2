using Microsoft.Maui.Controls.Shapes;

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
        BackgroundColor = ChipBackground,
        Padding = new Thickness(8, 4)
    };

    public static Picker Picker(string title) => new()
    {
        Title = title,
        TextColor = Navy,
        TitleColor = Muted,
        BackgroundColor = Surface
    };

    public static Entry Entry(string placeholder) => new()
    {
        Placeholder = placeholder,
        TextColor = Navy,
        PlaceholderColor = Muted,
        BackgroundColor = Surface
    };
}

using Microsoft.UI;
using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace PresentAI;

public enum AppColorScheme
{
    PresentAI,
    Ocean,
    Ember,
    Forest
}

public enum AppBackdropMode
{
    Solid,
    Acrylic,
    Mica,
    TabbedMica
}

public sealed record ThemePalette(
    Color Page,
    Color Sidebar,
    Color Panel,
    Color Slide,
    Color Primary,
    Color Accent,
    Color Text,
    Color Muted,
    Color LightText);

public static class ThemeManager
{
    public static ThemePalette PaletteFor(AppColorScheme scheme) => scheme switch
    {
        AppColorScheme.Ocean => new ThemePalette(
            Color.FromArgb(255, 239, 246, 246),
            Color.FromArgb(238, 13, 45, 55),
            Color.FromArgb(238, 217, 231, 228),
            Color.FromArgb(255, 8, 21, 30),
            Color.FromArgb(255, 13, 126, 138),
            Color.FromArgb(255, 211, 160, 68),
            Color.FromArgb(255, 14, 30, 34),
            Color.FromArgb(255, 79, 99, 103),
            Color.FromArgb(255, 248, 244, 231)),
        AppColorScheme.Ember => new ThemePalette(
            Color.FromArgb(255, 246, 241, 232),
            Color.FromArgb(238, 45, 33, 29),
            Color.FromArgb(238, 235, 222, 206),
            Color.FromArgb(255, 22, 20, 20),
            Color.FromArgb(255, 154, 72, 47),
            Color.FromArgb(255, 47, 125, 121),
            Color.FromArgb(255, 35, 28, 25),
            Color.FromArgb(255, 98, 82, 72),
            Color.FromArgb(255, 255, 244, 226)),
        AppColorScheme.Forest => new ThemePalette(
            Color.FromArgb(255, 238, 242, 235),
            Color.FromArgb(238, 24, 43, 33),
            Color.FromArgb(238, 221, 230, 215),
            Color.FromArgb(255, 10, 23, 18),
            Color.FromArgb(255, 58, 123, 85),
            Color.FromArgb(255, 191, 148, 65),
            Color.FromArgb(255, 23, 38, 30),
            Color.FromArgb(255, 82, 101, 88),
            Color.FromArgb(255, 245, 241, 223)),
        _ => new ThemePalette(
            Color.FromArgb(255, 245, 242, 234),
            Color.FromArgb(238, 24, 32, 30),
            Color.FromArgb(238, 231, 224, 206),
            Color.FromArgb(255, 16, 18, 24),
            Color.FromArgb(255, 47, 125, 121),
            Color.FromArgb(255, 208, 162, 75),
            Color.FromArgb(255, 16, 18, 24),
            Color.FromArgb(255, 83, 98, 93),
            Color.FromArgb(255, 247, 241, 223))
    };

    public static Brush Brush(Color color) => new SolidColorBrush(color);

    public static SystemBackdrop? CreateBackdrop(AppBackdropMode mode) => mode switch
    {
        AppBackdropMode.Acrylic => new DesktopAcrylicBackdrop(),
        AppBackdropMode.Mica => new MicaBackdrop { Kind = MicaKind.Base },
        AppBackdropMode.TabbedMica => new MicaBackdrop { Kind = MicaKind.BaseAlt },
        _ => null
    };

    public static string DisplayName(AppColorScheme scheme) => scheme switch
    {
        AppColorScheme.Ocean => "Ocean",
        AppColorScheme.Ember => "Ember",
        AppColorScheme.Forest => "Forest",
        _ => "PresentAI"
    };

    public static string DisplayName(AppBackdropMode mode) => mode switch
    {
        AppBackdropMode.Acrylic => "Acrylic",
        AppBackdropMode.Mica => "Mica",
        AppBackdropMode.TabbedMica => "Tabbed Mica",
        _ => "Solid"
    };
}

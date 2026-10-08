using System.Windows.Media;

namespace Corkboard.Services;

public static class ColorUtil
{
    public static string ToHex(Color c) => $"#{c.A:X2}{c.R:X2}{c.G:X2}{c.B:X2}";

    public static Color FromHex(string hex)
    {
        try { return (Color)ColorConverter.ConvertFromString(hex); }
        catch { return Colors.Red; }
    }

    public static SolidColorBrush Brush(string hex) => new(FromHex(hex));
}

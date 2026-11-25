using System.Windows.Media;
using AlfaRacing.Core.Catalog;
using AlfaRacing.Core.Launching;

namespace AlfaRacing.App.Infrastructure;

/// <summary>Brushes used from code (status dots, badges).</summary>
public static class Palette
{
    public static readonly Brush Running = Frozen(0x2E, 0xCC, 0x71);
    public static readonly Brush Stopped = Frozen(0x8B, 0x93, 0xA1);
    public static readonly Brush Missing = Frozen(0xFF, 0x5C, 0x5C);
    public static readonly Brush Disabled = Frozen(0x5B, 0x62, 0x70);
    public static readonly Brush Unknown = Frozen(0xE0, 0xB3, 0x41);

    public static readonly Brush Required = Frozen(0xE8, 0x1B, 0x25);
    public static readonly Brush Game = Frozen(0x2B, 0x3F, 0xAE);
    public static readonly Brush Hardware = Frozen(0x7A, 0x4C, 0xC9);
    public static readonly Brush Recommended = Frozen(0x1F, 0x8F, 0x7E);

    public static Brush For(ItemRunState state) => state switch
    {
        ItemRunState.Running => Running,
        ItemRunState.Stopped => Stopped,
        ItemRunState.Missing => Missing,
        ItemRunState.Disabled => Disabled,
        _ => Unknown,
    };

    public static Brush For(ProgramCategory category) => category switch
    {
        ProgramCategory.Required => Required,
        ProgramCategory.Game => Game,
        ProgramCategory.Hardware => Hardware,
        _ => Recommended,
    };

    private static Brush Frozen(byte r, byte g, byte b)
    {
        var brush = new SolidColorBrush(Color.FromRgb(r, g, b));
        brush.Freeze();
        return brush;
    }
}

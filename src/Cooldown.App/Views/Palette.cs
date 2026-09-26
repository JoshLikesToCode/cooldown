using Avalonia.Media;

namespace Cooldown.App.Views;

/// <summary>One place for colors. Ice blue drains to amber, then red, like an ability cooldown.</summary>
internal static class Palette
{
    public static readonly IBrush Background = Brush.Parse("#1B2330");
    public static readonly IBrush Surface = Brush.Parse("#243044");
    public static readonly IBrush Text = Brush.Parse("#E6EDF5");
    public static readonly IBrush Muted = Brush.Parse("#8A9BB3");
    public static readonly IBrush Plenty = Brush.Parse("#7FD1F5");
    public static readonly IBrush Low = Brush.Parse("#F2B84B");
    public static readonly IBrush Empty = Brush.Parse("#E5484D");
    public static readonly IBrush Track = Brush.Parse("#34425A");
}

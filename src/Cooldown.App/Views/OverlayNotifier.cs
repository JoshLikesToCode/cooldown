using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;
using Cooldown.Core;
using Cooldown.Core.Abstractions;
using Cooldown.Core.Models;

namespace Cooldown.App.Views;

/// <summary>
/// Shows warnings in a small always-on-top window instead of system toasts.
/// Windows silences toasts while a game is running, so toasts would be missed right when they matter.
/// </summary>
public sealed class OverlayNotifier : INotifier
{
    public void Notify(string title, string message, NotificationLevel level)
    {
        Log.Info($"[notify] {title}: {message}");
        Dispatcher.UIThread.Post(() => new OverlayWindow(title, message, level).ShowBriefly());
    }
}

internal sealed class OverlayWindow : Window
{
    private readonly NotificationLevel _level;

    public OverlayWindow(string title, string message, NotificationLevel level)
    {
        _level = level;
        SystemDecorations = SystemDecorations.None;
        Topmost = true;
        ShowActivated = false;   // Never steal focus from the game.
        ShowInTaskbar = false;
        CanResize = false;
        Width = 360;
        SizeToContent = SizeToContent.Height;
        WindowStartupLocation = WindowStartupLocation.Manual;
        Background = Palette.Surface;

        var accent = level == NotificationLevel.Alert ? Palette.Empty : Palette.Low;
        Content = new Border
        {
            BorderBrush = accent,
            BorderThickness = new Thickness(4, 0, 0, 0),
            Padding = new Thickness(16, 12),
            Child = new StackPanel
            {
                Spacing = 4,
                Children =
                {
                    new TextBlock { Text = title, FontSize = 15, FontWeight = FontWeight.SemiBold, Foreground = Palette.Text },
                    new TextBlock { Text = message, FontSize = 13, Foreground = Palette.Text, TextWrapping = TextWrapping.Wrap },
                },
            },
        };
    }

    public void ShowBriefly()
    {
        if (Screens.Primary is { } screen)
        {
            var area = screen.WorkingArea;
            int margin = (int)(24 * screen.Scaling);
            Position = new PixelPoint(area.Right - (int)(Width * screen.Scaling) - margin, area.Y + margin);
        }
        Show();
        var seconds = _level == NotificationLevel.Alert ? 12 : 8;
        DispatcherTimer.RunOnce(Close, TimeSpan.FromSeconds(seconds));
    }
}

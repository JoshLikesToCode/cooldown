using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Cooldown.Core.Models;
using Cooldown.Core.Services;

namespace Cooldown.App.Views;

/// <summary>Shows what's running and how much time is left in each bucket. Closing hides it to the tray.</summary>
public sealed class MainWindow : Window
{
    private readonly AppHost _host;
    private readonly TextBlock _nowPlaying = new() { FontSize = 15, Foreground = Palette.Text };
    private readonly StackPanel _buckets = new() { Spacing = 12 };

    public bool AllowClose { get; set; }

    public MainWindow(AppHost host)
    {
        _host = host;
        Title = "Cooldown";
        Width = 440;
        SizeToContent = SizeToContent.Height;
        CanResize = false;
        Background = Palette.Background;

        Content = new StackPanel
        {
            Margin = new Thickness(24),
            Spacing = 20,
            Children =
            {
                _nowPlaying,
                _buckets,
                new TextBlock
                {
                    Text = $"{host.Status}\nSettings: {host.ConfigPath}",
                    FontSize = 11,
                    Foreground = Palette.Muted,
                    TextWrapping = TextWrapping.Wrap,
                },
            },
        };

        Refresh();
    }

    public void Refresh()
    {
        var now = _host.Clock.Now;
        var game = _host.Tracker.RunningGame;
        var bucket = game is null ? null : _host.Budgets.BucketFor(game.AppId);

        if (game is null)
            _nowPlaying.Text = "Nothing running";
        else if (bucket is null)
            _nowPlaying.Text = $"Playing {game.DisplayName}. Not in a bucket, so no limit.";
        else
            _nowPlaying.Text = $"Playing {game.DisplayName} in {bucket.Name}";

        _buckets.Children.Clear();
        foreach (var status in _host.Budgets.Snapshot(now))
            _buckets.Children.Add(BucketRow(status));
    }

    private static Control BucketRow(BucketStatus s)
    {
        double fraction = s.Bucket.Budget > TimeSpan.Zero ? s.Remaining / s.Bucket.Budget : 0;
        var color = s.IsExhausted ? Palette.Empty : fraction <= 0.2 ? Palette.Low : Palette.Plenty;
        var rule = s.Bucket.Enforcement == Enforcement.Block ? "closes games" : "reminds only";

        var header = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        header.Children.Add(new StackPanel
        {
            Children =
            {
                new TextBlock { Text = s.Bucket.Name, FontSize = 16, FontWeight = FontWeight.SemiBold, Foreground = Palette.Text },
                new TextBlock
                {
                    Text = $"{Format.Duration(s.Bucket.Budget)} {Periods.Noun(s.Bucket.Period)}, {rule}",
                    FontSize = 12,
                    Foreground = Palette.Muted,
                },
            },
        });
        var remaining = new TextBlock
        {
            Text = s.IsExhausted ? "Out of time" : $"{Format.Duration(s.Remaining)} left",
            FontSize = 20,
            FontWeight = FontWeight.Bold,
            Foreground = color,
            VerticalAlignment = VerticalAlignment.Center,
        };
        Grid.SetColumn(remaining, 1);
        header.Children.Add(remaining);

        var bar = new ProgressBar
        {
            Minimum = 0,
            Maximum = 1,
            Value = fraction,
            Height = 6,
            MinHeight = 6,
            CornerRadius = new CornerRadius(3),
            Foreground = color,
            Background = Palette.Track,
        };

        return new Border
        {
            Background = Palette.Surface,
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(16, 12),
            Child = new StackPanel { Spacing = 10, Children = { header, bar } },
        };
    }

    protected override void OnClosing(WindowClosingEventArgs e)
    {
        // The X button hides to the tray. Quit from the tray menu really exits.
        if (!AllowClose && e.CloseReason != WindowCloseReason.ApplicationShutdown)
        {
            e.Cancel = true;
            Hide();
        }
        base.OnClosing(e);
    }
}

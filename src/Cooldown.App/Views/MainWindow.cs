using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform;
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
        Icon = new WindowIcon(AssetLoader.Open(new Uri("avares://Cooldown/Assets/app-icon.png")));

        var settingsButton = new Button { Content = "Settings", HorizontalAlignment = HorizontalAlignment.Left };
        settingsButton.Click += (_, _) => new SettingsWindow(_host, Refresh).Show();

        Content = new StackPanel
        {
            Margin = new Thickness(24),
            Spacing = 20,
            Children =
            {
                _nowPlaying,
                _buckets,
                settingsButton,
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
        var buckets = game is null ? [] : _host.Budgets.BucketsFor(game.AppId);

        if (game is null)
            _nowPlaying.Text = "Nothing running";
        else if (buckets.Count == 0)
            _nowPlaying.Text = $"Playing {game.DisplayName}. Not in a bucket, so no limit.";
        else
            _nowPlaying.Text = $"Playing {game.DisplayName} in {string.Join(", ", buckets.Select(b => b.Name))}";

        _buckets.Children.Clear();
        foreach (var status in _host.Budgets.Snapshot(now))
            _buckets.Children.Add(BucketRow(status));
    }

    private static Control BucketRow(BucketStatus s)
    {
        var name = string.IsNullOrEmpty(s.Bucket.Icon) ? s.Bucket.Name : $"{s.Bucket.Icon} {s.Bucket.Name}";

        double fraction;
        IBrush color;
        string valueText;
        string subtitle;

        if (s.Bucket.IsGoal)
        {
            fraction = s.Bucket.Budget > TimeSpan.Zero ? Math.Min(1.0, s.Used / s.Bucket.Budget) : 0;
            color = s.Bucket.Color is { } goalHex ? Brush.Parse(goalHex) : Palette.Plenty;
            valueText = fraction >= 1
                ? $"{Format.Duration(s.Used)} — goal met!"
                : $"{Format.Duration(s.Used)} / {Format.Duration(s.Bucket.Budget)}";
            subtitle = $"Goal {Periods.Noun(s.Bucket.Period)}";
        }
        else
        {
            fraction = s.Bucket.Budget > TimeSpan.Zero ? s.Remaining / s.Bucket.Budget : 0;
            var defaultColor = s.IsExhausted ? Palette.Empty : fraction <= 0.2 ? Palette.Low : Palette.Plenty;
            color = s.Bucket.Color is { } hex ? Brush.Parse(hex) : defaultColor;
            var rule = s.Bucket.Enforcement == Enforcement.Block ? "closes games" : "reminds only";
            valueText = s.IsExhausted ? "Out of time" : $"{Format.Duration(s.Remaining)} left";
            subtitle = $"{Format.Duration(s.Bucket.Budget)} {Periods.Noun(s.Bucket.Period)}, {rule}";
        }

        var header = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        header.Children.Add(new StackPanel
        {
            Children =
            {
                new TextBlock { Text = name, FontSize = 16, FontWeight = FontWeight.SemiBold, Foreground = Palette.Text },
                new TextBlock { Text = subtitle, FontSize = 12, Foreground = Palette.Muted },
            },
        });
        var remaining = new TextBlock
        {
            Text = valueText,
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

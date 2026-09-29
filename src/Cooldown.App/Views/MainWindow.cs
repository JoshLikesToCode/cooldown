using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform;
using Cooldown.Core.Models;
using Cooldown.Core.Services;

namespace Cooldown.App.Views;

/// <summary>Shows what's running and how much time is left in each bucket. Closing hides it to the tray.</summary>
public sealed class MainWindow : Window
{
    private const double BaseWidth = 440;
    private const double CalendarPanelWidth = 280;

    private readonly AppHost _host;
    private readonly TextBlock _nowPlaying = new() { FontSize = 15, Foreground = Palette.Text };
    private readonly TextBlock _summary = new() { FontSize = 12, Foreground = Palette.Muted, TextWrapping = TextWrapping.Wrap, IsVisible = false };
    private readonly StackPanel _buckets = new() { Spacing = 12 };

    private readonly Button _calendarToggle = new() { Content = "Calendar →", HorizontalAlignment = HorizontalAlignment.Left, IsVisible = false };
    private readonly Border _calendarPanel = new()
    {
        Width = CalendarPanelWidth,
        IsVisible = false,
        Background = Palette.Surface,
        CornerRadius = new CornerRadius(8),
        Padding = new Thickness(14),
        Margin = new Thickness(0, 24, 24, 24),
    };
    private readonly TextBlock _calendarTitle = new() { FontWeight = FontWeight.SemiBold, Foreground = Palette.Text, VerticalAlignment = VerticalAlignment.Center };
    private readonly ContentControl _calendarBody = new();
    private readonly CheckBox _calShowGoals = new() { Content = "Show goals" };
    private readonly CheckBox _calShowLimits = new() { Content = "Show limits" };
    private bool _calendarOpen;
    private int _calendarYear;
    private int _calendarMonth;

    public bool AllowClose { get; set; }

    public MainWindow(AppHost host)
    {
        _host = host;
        Title = "Cooldown";
        Width = BaseWidth;
        SizeToContent = SizeToContent.Height;
        CanResize = false;
        Background = Palette.Background;
        Icon = new WindowIcon(AssetLoader.Open(new Uri("avares://Cooldown/Assets/app-icon.png")));

        var now = _host.Clock.Now;
        _calendarYear = now.Year;
        _calendarMonth = now.Month;

        var settingsButton = new Button { Content = "Settings", HorizontalAlignment = HorizontalAlignment.Left };
        settingsButton.Click += (_, _) => new SettingsWindow(_host, Refresh).Show();
        _calendarToggle.Click += (_, _) => ToggleCalendar();
        BuildCalendarPanel();

        var mainContent = new StackPanel
        {
            Margin = new Thickness(24),
            Spacing = 20,
            Children =
            {
                _nowPlaying,
                _summary,
                _buckets,
                new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Children = { settingsButton, _calendarToggle } },
                new TextBlock
                {
                    Text = $"{host.Status}\nSettings: {host.ConfigPath}",
                    FontSize = 11,
                    Foreground = Palette.Muted,
                    TextWrapping = TextWrapping.Wrap,
                },
            },
        };

        // Fixed pixel columns, not Auto: Auto ties column width to the content's measured size,
        // which flexes slightly every tick as duration strings change length ("1h 0m left" vs
        // "53m left") - that's what was making the whole window visibly jitter/reposition.
        var root = new Grid { ColumnDefinitions = new ColumnDefinitions($"{BaseWidth},{CalendarPanelWidth}") };
        root.Children.Add(mainContent);
        Grid.SetColumn(_calendarPanel, 1);
        root.Children.Add(_calendarPanel);
        Content = root;

        Refresh();
    }

    // ---- Pop-out calendar ----

    private void BuildCalendarPanel()
    {
        var prev = new Button { Content = "‹", Padding = new Thickness(8, 2) };
        var next = new Button { Content = "›", Padding = new Thickness(8, 2) };
        var close = new Button { Content = "← Close" };
        prev.Click += (_, _) => ShiftMonth(-1);
        next.Click += (_, _) => ShiftMonth(+1);
        close.Click += (_, _) => ToggleCalendar();

        var header = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto") };
        header.Children.Add(prev);
        Grid.SetColumn(_calendarTitle, 1);
        header.Children.Add(_calendarTitle);
        Grid.SetColumn(next, 2);
        header.Children.Add(next);

        var legend = new TextBlock
        {
            Text = "✓ met · ✗ missed",
            FontSize = 10,
            Foreground = Palette.Muted,
            HorizontalAlignment = HorizontalAlignment.Center,
        };

        _calShowGoals.IsChecked = _host.Config.CalendarShowGoals;
        _calShowLimits.IsChecked = _host.Config.CalendarShowLimits;
        _calShowGoals.IsCheckedChanged += (_, _) => SetCalendarCriterion(goals: _calShowGoals.IsChecked ?? true, limits: null);
        _calShowLimits.IsCheckedChanged += (_, _) => SetCalendarCriterion(goals: null, limits: _calShowLimits.IsChecked ?? true);

        var toggles = new StackPanel
        {
            Spacing = 8,
            Children =
            {
                ToggleRow(_calShowGoals, "Green check the days you met every goal bucket."),
                ToggleRow(_calShowLimits, "Green check the days you stayed under every limit."),
            },
        };

        _calendarPanel.Child = new StackPanel { Spacing = 10, Children = { header, _calendarBody, legend, toggles, close } };
    }

    private static Control ToggleRow(CheckBox checkBox, string description) => new StackPanel
    {
        Spacing = 1,
        Children =
        {
            checkBox,
            new TextBlock
            {
                Text = description,
                FontSize = 10,
                Foreground = Palette.Muted,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(22, 0, 0, 0),
            },
        },
    };

    private void SetCalendarCriterion(bool? goals, bool? limits)
    {
        if (goals is { } g) _host.Config.CalendarShowGoals = g;
        if (limits is { } l) _host.Config.CalendarShowLimits = l;
        ConfigStore.Save(_host.ConfigPath, _host.Config);
        if (_calendarOpen)
            RefreshCalendar();
    }

    private void ToggleCalendar()
    {
        _calendarOpen = !_calendarOpen;
        _calendarPanel.IsVisible = _calendarOpen;
        Width = _calendarOpen ? BaseWidth + CalendarPanelWidth : BaseWidth;
        if (_calendarOpen)
            RefreshCalendar();
    }

    private void ShiftMonth(int delta)
    {
        var d = new DateOnly(_calendarYear, _calendarMonth, 1).AddMonths(delta);
        _calendarYear = d.Year;
        _calendarMonth = d.Month;
        RefreshCalendar();
    }

    private void RefreshCalendar()
    {
        _calendarTitle.Text = new DateOnly(_calendarYear, _calendarMonth, 1).ToString("MMMM yyyy");
        _calendarBody.Content = BuildCalendarGrid(_calendarYear, _calendarMonth);
    }

    private Control BuildCalendarGrid(int year, int month)
    {
        var grid = new UniformGrid { Columns = 7, Rows = 7 };
        foreach (var label in new[] { "S", "M", "T", "W", "T", "F", "S" })
            grid.Children.Add(new TextBlock { Text = label, FontSize = 10, Foreground = Palette.Muted, HorizontalAlignment = HorizontalAlignment.Center });

        var firstOfMonth = new DateOnly(year, month, 1);
        for (int i = 0; i < (int)firstOfMonth.DayOfWeek; i++)
            grid.Children.Add(new Border());

        var now = _host.Clock.Now;
        var config = _host.Config;
        int daysInMonth = DateTime.DaysInMonth(year, month);
        for (int day = 1; day <= daysInMonth; day++)
        {
            var date = new DateOnly(year, month, day);
            var result = CalendarStatus.ForDay(_host.Budgets, date, now, config.DayStartHour, config.CalendarShowGoals, config.CalendarShowLimits);
            grid.Children.Add(CalendarCell(day, result));
        }
        return grid;
    }

    private static Control CalendarCell(int day, DayResult result)
    {
        var (glyph, color) = result switch
        {
            DayResult.Pass => ("✓", Palette.Plenty),
            DayResult.Fail => ("✗", Palette.Empty),
            DayResult.NoCriteria => ("–", Palette.Muted),
            _ => ("", Palette.Muted), // hasn't happened yet
        };
        return new StackPanel
        {
            Spacing = 1,
            Margin = new Thickness(0, 2),
            HorizontalAlignment = HorizontalAlignment.Center,
            Children =
            {
                new TextBlock { Text = day.ToString(), FontSize = 10, Foreground = Palette.Muted, HorizontalAlignment = HorizontalAlignment.Center },
                new TextBlock { Text = glyph, FontSize = 13, FontWeight = FontWeight.Bold, Foreground = color, HorizontalAlignment = HorizontalAlignment.Center },
            },
        };
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

        RefreshSummary(now);

        _buckets.Children.Clear();
        foreach (var status in _host.Budgets.Snapshot(now))
            _buckets.Children.Add(BucketRow(status));

        _calendarToggle.IsVisible = _host.Config.CalendarEnabled;
        if (!_host.Config.CalendarEnabled && _calendarOpen)
        {
            _calendarOpen = false;
            _calendarPanel.IsVisible = false;
            Width = BaseWidth;
        }
        else if (_calendarOpen)
        {
            RefreshCalendar();
        }
    }

    /// <summary>Daily/weekly playtime split by countdown vs. goal buckets, separate from any one bucket's own numbers.</summary>
    private void RefreshSummary(DateTimeOffset now)
    {
        var config = _host.Config;
        bool hasCountdown = config.Buckets.Any(b => !b.IsGoal);
        bool hasGoal = config.Buckets.Any(b => b.IsGoal);

        var dayStart = Periods.CurrentStart(ResetPeriod.Daily, now, config.DayStartHour, config.WeekStart);
        var weekStart = Periods.CurrentStart(ResetPeriod.Weekly, now, config.DayStartHour, config.WeekStart);

        var lines = new List<string>();
        if (hasCountdown)
        {
            var day = _host.Budgets.CountdownPlaytime(dayStart, now);
            var week = _host.Budgets.CountdownPlaytime(weekStart, now);
            lines.Add($"Countdown games: {Format.Duration(day)} today, {Format.Duration(week)} this week");
        }
        if (hasGoal)
        {
            var day = _host.Budgets.GoalPlaytime(dayStart, now);
            var week = _host.Budgets.GoalPlaytime(weekStart, now);
            lines.Add($"Goal games: {Format.Duration(day)} today, {Format.Duration(week)} this week");
        }

        _summary.Text = string.Join("\n", lines);
        _summary.IsVisible = lines.Count > 0;
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

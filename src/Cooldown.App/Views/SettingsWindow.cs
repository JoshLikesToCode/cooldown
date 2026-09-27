using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Cooldown.Core.Models;
using Cooldown.Core.Services;

namespace Cooldown.App.Views;

/// <summary>
/// Lets buckets and game assignments be created/edited without hand-editing config.json.
/// Every mutating action applies straight to the shared CooldownConfig (atomic list/dict swap,
/// per Tracker's concurrency rule), saves to disk immediately, and pushes an undo entry.
/// Undo/redo is in-session only: it's a convenience for the current editing pass, not a history
/// worth persisting across app restarts, so it isn't written to disk.
/// </summary>
public sealed class SettingsWindow : Window
{
    private readonly AppHost _host;
    private readonly Action _onChanged;

    private readonly Stack<Snapshot> _undo = new();
    private readonly Stack<Snapshot> _redo = new();
    private readonly Button _undoButton = new() { Content = "Undo" };
    private readonly Button _redoButton = new() { Content = "Redo" };

    private readonly StackPanel _bucketList = new() { Spacing = 8 };
    private readonly StackPanel _assignmentList = new() { Spacing = 6 };
    private readonly TextBox _search = new() { Watermark = "Search games..." };

    private readonly NumericUpDown _dayStartHour = new() { Minimum = 0, Maximum = 23 };
    private readonly ComboBox _weekStart = new() { ItemsSource = Enum.GetValues<DayOfWeek>(), HorizontalAlignment = HorizontalAlignment.Stretch };
    private readonly TextBox _warningMinutes = new() { Watermark = "e.g. 10, 5, 1" };
    private readonly NumericUpDown _graceSeconds = new() { Minimum = 0, Maximum = 3600 };
    private readonly NumericUpDown _launchGraceSeconds = new() { Minimum = 0, Maximum = 3600 };
    private readonly CheckBox _showDaily = new() { Content = "Today's total" };
    private readonly CheckBox _showWeekly = new() { Content = "This week's total" };
    private readonly CheckBox _showAllTime = new() { Content = "All-time total" };

    private sealed record Snapshot(
        List<Bucket> Buckets,
        Dictionary<int, List<string>> Assignments,
        int DayStartHour,
        DayOfWeek WeekStart,
        int[] WarningMinutes,
        int GraceSeconds,
        int LaunchGraceSeconds,
        bool ShowDailyPlaytime,
        bool ShowWeeklyPlaytime,
        bool ShowAllTimePlaytime);

    public SettingsWindow(AppHost host, Action onChanged)
    {
        _host = host;
        _onChanged = onChanged;

        Title = "Cooldown Settings";
        Width = 520;
        Height = 620;
        Background = Palette.Background;

        var undoRow = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(16, 12, 16, 0),
            Children = { _undoButton, _redoButton },
        };
        _undoButton.Click += (_, _) => Undo();
        _redoButton.Click += (_, _) => Redo();

        var tabs = new TabControl
        {
            Margin = new Thickness(16),
            Items =
            {
                new TabItem { Header = "Buckets", Content = BuildBucketsTab() },
                new TabItem { Header = "Assignments", Content = BuildAssignmentsTab() },
                new TabItem { Header = "Global", Content = BuildGlobalTab() },
            },
        };

        Content = new DockPanel
        {
            Children = { Docked(undoRow, Dock.Top), tabs },
        };

        LoadGlobalFields();
        RefreshBucketList();
        RefreshAssignmentList();
        RefreshUndoRedoButtons();
    }

    private static Control Docked(Control c, Dock side)
    {
        DockPanel.SetDock(c, side);
        return c;
    }

    // ---- Buckets tab ----

    private Control BuildBucketsTab()
    {
        var addButton = new Button { Content = "+ Add bucket", Background = Palette.Plenty, HorizontalAlignment = HorizontalAlignment.Left };
        addButton.Click += async (_, _) =>
        {
            var bucket = await BucketEditDialog.ShowAsync(this, null, _host.Config.Buckets.Select(b => b.Id).ToList());
            if (bucket is not null)
                Commit(() => _host.Config.Buckets = ConfigEditing.UpsertBucket(_host.Config.Buckets, bucket));
        };

        return new StackPanel
        {
            Spacing = 12,
            Children =
            {
                new ScrollViewer { Content = _bucketList, MaxHeight = 440 },
                addButton,
            },
        };
    }

    private void RefreshBucketList()
    {
        _bucketList.Children.Clear();
        var buckets = _host.Config.Buckets;
        for (int i = 0; i < buckets.Count; i++)
            _bucketList.Children.Add(BucketRow(buckets[i], i, buckets.Count));
    }

    private Control BucketRow(Bucket bucket, int index, int count)
    {
        var up = new Button
        {
            Content = "▲", IsEnabled = index > 0, Width = 28, Padding = new Thickness(0),
            HorizontalContentAlignment = HorizontalAlignment.Center,
        };
        var down = new Button
        {
            Content = "▼", IsEnabled = index < count - 1, Width = 28, Padding = new Thickness(0),
            HorizontalContentAlignment = HorizontalAlignment.Center,
        };
        var edit = new Button { Content = "Edit" };
        var delete = new Button { Content = "Delete", Background = Palette.Empty };

        up.Click += (_, _) => Commit(() => _host.Config.Buckets = ConfigEditing.MoveBucket(_host.Config.Buckets, index, -1));
        down.Click += (_, _) => Commit(() => _host.Config.Buckets = ConfigEditing.MoveBucket(_host.Config.Buckets, index, +1));

        edit.Click += async (_, _) =>
        {
            var takenIds = _host.Config.Buckets.Select(b => b.Id).Where(id => id != bucket.Id).ToList();
            var updated = await BucketEditDialog.ShowAsync(this, bucket, takenIds);
            if (updated is not null)
                Commit(() => _host.Config.Buckets = ConfigEditing.UpsertBucket(_host.Config.Buckets, updated));
        };

        delete.Click += async (_, _) =>
        {
            if (ConfigEditing.HasAssignments(_host.Config.Assignments, bucket.Id))
            {
                bool ok = await ConfirmDialog.ShowAsync(this, "Delete bucket",
                    $"\"{bucket.Name}\" has games assigned to it. Deleting it leaves those games unassigned (unlimited), not deleted. Continue?");
                if (!ok)
                    return;
            }
            Commit(() =>
            {
                var (b, a) = ConfigEditing.RemoveBucket(_host.Config.Buckets, _host.Config.Assignments, bucket.Id);
                _host.Config.Buckets = b;
                _host.Config.Assignments = a;
            });
        };

        var accent = bucket.Color is { } hex ? Brush.Parse(hex) : Palette.Plenty;
        var label = string.IsNullOrEmpty(bucket.Icon) ? bucket.Name : $"{bucket.Icon} {bucket.Name}";

        var header = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto") };
        header.Children.Add(new Border { Width = 10, Height = 10, CornerRadius = new CornerRadius(5), Background = accent, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0) });
        var title = new StackPanel
        {
            Children =
            {
                new TextBlock { Text = label, FontWeight = FontWeight.SemiBold, Foreground = Palette.Text },
                new TextBlock
                {
                    Text = $"{Format.Duration(bucket.Budget)} {Periods.Noun(bucket.Period)}, {(bucket.Enforcement == Enforcement.Block ? "closes games" : "reminds only")}",
                    FontSize = 11,
                    Foreground = Palette.Muted,
                },
            },
        };
        Grid.SetColumn(title, 1);
        header.Children.Add(title);
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4, Children = { up, down, edit, delete } };
        Grid.SetColumn(buttons, 2);
        header.Children.Add(buttons);

        return new Border
        {
            Background = Palette.Surface,
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(12, 8),
            Child = header,
        };
    }

    // ---- Assignments tab ----

    private Control BuildAssignmentsTab()
    {
        _search.TextChanged += (_, _) => RefreshAssignmentList();
        return new StackPanel
        {
            Spacing = 10,
            Children = { _search, new ScrollViewer { Content = _assignmentList, MaxHeight = 470 } },
        };
    }

    private void RefreshAssignmentList()
    {
        _assignmentList.Children.Clear();

        var installed = _host.Catalog.GetInstalledGames().ToDictionary(g => g.AppId, g => g.DisplayName);
        var assignedIds = _host.Config.Assignments.Keys;
        var allIds = installed.Keys.Union(assignedIds).ToList();

        // Playtime totals for whichever periods are turned on in the Global tab, so this
        // tab doubles as a simple stats view instead of needing a separate page.
        var now = _host.Clock.Now;
        var daily = _host.Config.ShowDailyPlaytime
            ? TotalsSince(Periods.CurrentStart(ResetPeriod.Daily, now, _host.Config.DayStartHour, _host.Config.WeekStart), now)
            : null;
        var weekly = _host.Config.ShowWeeklyPlaytime
            ? TotalsSince(Periods.CurrentStart(ResetPeriod.Weekly, now, _host.Config.DayStartHour, _host.Config.WeekStart), now)
            : null;
        var allTime = _host.Config.ShowAllTimePlaytime ? TotalsSince(DateTimeOffset.UnixEpoch, now) : null;

        var filter = _search.Text?.Trim() ?? "";
        var rows = allIds
            .Select(id => (AppId: id, Name: installed.GetValueOrDefault(id, $"App {id}"), Installed: installed.ContainsKey(id)))
            .Where(g => filter.Length == 0 || g.Name.Contains(filter, StringComparison.OrdinalIgnoreCase))
            .OrderBy(g => g.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        foreach (var game in rows)
        {
            var stats = new List<string>();
            if (daily is not null && daily.GetValueOrDefault(game.AppId) is { } d && d > TimeSpan.Zero)
                stats.Add($"{Format.Duration(d)} today");
            if (weekly is not null && weekly.GetValueOrDefault(game.AppId) is { } w && w > TimeSpan.Zero)
                stats.Add($"{Format.Duration(w)} this week");
            if (allTime is not null && allTime.GetValueOrDefault(game.AppId) is { } a && a > TimeSpan.Zero)
                stats.Add($"{Format.Duration(a)} total");

            _assignmentList.Children.Add(AssignmentRow(game.AppId, game.Name, game.Installed, string.Join(" · ", stats)));
        }

        if (rows.Count == 0)
            _assignmentList.Children.Add(new TextBlock { Text = "No games found.", Foreground = Palette.Muted, Margin = new Thickness(4) });
    }

    /// <summary>Per-app playtime since <paramref name="since"/>, clipping sessions that straddle the boundary.</summary>
    private Dictionary<int, TimeSpan> TotalsSince(DateTimeOffset since, DateTimeOffset now) =>
        _host.Sessions.GetSessionsSince(since)
            .GroupBy(s => s.AppId)
            .ToDictionary(g => g.Key, g => g.Aggregate(TimeSpan.Zero, (acc, s) =>
            {
                var from = s.Start < since ? since : s.Start;
                var to = s.End > now ? now : s.End;
                return to > from ? acc + (to - from) : acc;
            }));

    private Control AssignmentRow(int appId, string name, bool installed, string statsText)
    {
        var icon = new Image { Width = 32, Height = 18, Stretch = Stretch.UniformToFill };
        var iconBox = new Border
        {
            Width = 32, Height = 18, CornerRadius = new CornerRadius(3),
            ClipToBounds = true, Child = icon,
            VerticalAlignment = VerticalAlignment.Center,
            IsVisible = false, // stays hidden - no grey placeholder box - unless a real icon loads below
        };
        LoadIconAsync(appId, icon, iconBox);

        var label = new TextBlock
        {
            Text = installed ? name : $"{name} (not installed)",
            Foreground = installed ? Palette.Text : Palette.Muted,
            TextWrapping = TextWrapping.NoWrap,
            TextTrimming = TextTrimming.CharacterEllipsis,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(8, 0),
        };

        var totalLabel = new TextBlock
        {
            Text = statsText,
            Foreground = Palette.Muted,
            FontSize = 11,
            VerticalAlignment = VerticalAlignment.Center,
        };

        var top = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto") };
        top.Children.Add(iconBox);
        Grid.SetColumn(label, 1);
        top.Children.Add(label);
        Grid.SetColumn(totalLabel, 2);
        top.Children.Add(totalLabel);

        var chips = new WrapPanel { Margin = new Thickness(0, 6, 0, 0) };
        var assigned = _host.Config.Assignments.GetValueOrDefault(appId) ?? [];
        foreach (var bucket in _host.Config.Buckets)
        {
            var chip = new ToggleButton
            {
                Content = bucket.Name,
                IsChecked = assigned.Contains(bucket.Id),
                Margin = new Thickness(0, 0, 6, 6),
                Padding = new Thickness(10, 3),
                CornerRadius = new CornerRadius(10),
            };
            chip.IsCheckedChanged += (_, _) =>
            {
                bool nowOn = chip.IsChecked == true;
                var current = _host.Config.Assignments.GetValueOrDefault(appId) ?? [];
                if (nowOn == current.Contains(bucket.Id))
                    return;
                Commit(() => _host.Config.Assignments = ConfigEditing.SetAssignment(_host.Config.Assignments, appId, bucket.Id, nowOn));
            };
            chips.Children.Add(chip);
        }
        if (_host.Config.Buckets.Count == 0)
            chips.Children.Add(new TextBlock { Text = "No buckets yet - add one on the Buckets tab.", Foreground = Palette.Muted, FontSize = 11 });

        return new Border
        {
            Background = Palette.Surface,
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(12, 8),
            Child = new StackPanel { Children = { top, chips } },
        };
    }

    private void LoadIconAsync(int appId, Image target, Border box) => _ = LoadIconCoreAsync(appId, target, box);

    private async Task LoadIconCoreAsync(int appId, Image target, Border box)
    {
        var bitmap = await SteamIconCache.GetAsync(appId, Path.Combine(_host.DataDir, "icon-cache"));
        if (bitmap is null)
            return; // no icon available (offline, 404, fake-mode App ID) - leave the box hidden, no placeholder

        Dispatcher.UIThread.Post(() =>
        {
            target.Source = bitmap;
            box.IsVisible = true;
        });
    }

    // ---- Global tab ----

    private Control BuildGlobalTab()
    {
        var save = new Button { Content = "Save global settings", Background = Palette.Plenty, HorizontalAlignment = HorizontalAlignment.Left };
        save.Click += (_, _) =>
        {
            var minutes = (_warningMinutes.Text ?? "")
                .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                .Select(s => int.TryParse(s, out var m) ? m : (int?)null)
                .Where(m => m is not null)
                .Select(m => m!.Value)
                .ToArray();

            Commit(() =>
            {
                _host.Config.DayStartHour = (int)(_dayStartHour.Value ?? 4);
                _host.Config.WeekStart = (DayOfWeek)_weekStart.SelectedItem!;
                _host.Config.WarningMinutes = minutes.Length > 0 ? minutes : _host.Config.WarningMinutes;
                _host.Config.GraceSeconds = (int)(_graceSeconds.Value ?? 60);
                _host.Config.LaunchGraceSeconds = (int)(_launchGraceSeconds.Value ?? 15);
                _host.Config.ShowDailyPlaytime = _showDaily.IsChecked ?? true;
                _host.Config.ShowWeeklyPlaytime = _showWeekly.IsChecked ?? true;
                _host.Config.ShowAllTimePlaytime = _showAllTime.IsChecked ?? true;
            });
        };

        return new StackPanel
        {
            Spacing = 12,
            Children =
            {
                Field("Day start hour (0-23)", _dayStartHour),
                Field("Week starts on", _weekStart),
                Field("Warning minutes (comma separated)", _warningMinutes),
                Field("Grace seconds before closing a game", _graceSeconds),
                Field("Grace seconds if launched already-empty", _launchGraceSeconds),
                Field("Playtime totals shown on the Assignments tab", new StackPanel { Spacing = 4, Children = { _showDaily, _showWeekly, _showAllTime } }),
                save,
            },
        };
    }

    private void LoadGlobalFields()
    {
        _dayStartHour.Value = _host.Config.DayStartHour;
        _weekStart.SelectedItem = _host.Config.WeekStart;
        _warningMinutes.Text = string.Join(", ", _host.Config.WarningMinutes);
        _graceSeconds.Value = _host.Config.GraceSeconds;
        _launchGraceSeconds.Value = _host.Config.LaunchGraceSeconds;
        _showDaily.IsChecked = _host.Config.ShowDailyPlaytime;
        _showWeekly.IsChecked = _host.Config.ShowWeeklyPlaytime;
        _showAllTime.IsChecked = _host.Config.ShowAllTimePlaytime;
    }

    private static Control Field(string label, Control input) => new StackPanel
    {
        Spacing = 4,
        Children = { new TextBlock { Text = label, Foreground = Palette.Muted, FontSize = 12 }, input },
    };

    // ---- Undo/redo + persistence plumbing ----

    private Snapshot Capture() => new(
        new List<Bucket>(_host.Config.Buckets),
        _host.Config.Assignments.ToDictionary(kv => kv.Key, kv => new List<string>(kv.Value)),
        _host.Config.DayStartHour,
        _host.Config.WeekStart,
        (int[])_host.Config.WarningMinutes.Clone(),
        _host.Config.GraceSeconds,
        _host.Config.LaunchGraceSeconds,
        _host.Config.ShowDailyPlaytime,
        _host.Config.ShowWeeklyPlaytime,
        _host.Config.ShowAllTimePlaytime);

    private void Apply(Snapshot s)
    {
        _host.Config.Buckets = s.Buckets;
        _host.Config.Assignments = s.Assignments;
        _host.Config.DayStartHour = s.DayStartHour;
        _host.Config.WeekStart = s.WeekStart;
        _host.Config.WarningMinutes = s.WarningMinutes;
        _host.Config.GraceSeconds = s.GraceSeconds;
        _host.Config.LaunchGraceSeconds = s.LaunchGraceSeconds;
        _host.Config.ShowDailyPlaytime = s.ShowDailyPlaytime;
        _host.Config.ShowWeeklyPlaytime = s.ShowWeeklyPlaytime;
        _host.Config.ShowAllTimePlaytime = s.ShowAllTimePlaytime;
    }

    private void Commit(Action mutate)
    {
        _undo.Push(Capture());
        _redo.Clear();
        mutate();
        Persist();
        RefreshAll();
    }

    private void Undo()
    {
        if (_undo.Count == 0)
            return;
        _redo.Push(Capture());
        Apply(_undo.Pop());
        Persist();
        RefreshAll();
    }

    private void Redo()
    {
        if (_redo.Count == 0)
            return;
        _undo.Push(Capture());
        Apply(_redo.Pop());
        Persist();
        RefreshAll();
    }

    private void Persist()
    {
        ConfigStore.Save(_host.ConfigPath, _host.Config);
        _onChanged();
    }

    private void RefreshAll()
    {
        RefreshBucketList();
        RefreshAssignmentList();
        LoadGlobalFields();
        RefreshUndoRedoButtons();
    }

    private void RefreshUndoRedoButtons()
    {
        _undoButton.IsEnabled = _undo.Count > 0;
        _redoButton.IsEnabled = _redo.Count > 0;
    }
}

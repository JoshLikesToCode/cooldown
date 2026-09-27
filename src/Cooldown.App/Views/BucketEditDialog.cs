using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Cooldown.Core.Models;

namespace Cooldown.App.Views;

/// <summary>Add/edit form for a single bucket. Returns null if the user cancels.</summary>
internal static class BucketEditDialog
{
    private static readonly string[] Swatches = ["#7FD1F5", "#F2B84B", "#E5484D", "#8A6FE8", "#4BC97A", "#E56FA0"];
    private const string NoIcon = "None";
    private static readonly string[] IconChoices =
        [NoIcon, "🎮", "🎯", "⚔️", "🏆", "🎲", "🕹️", "🔫", "🧙", "🏎️", "⏱️", "🛡️", "🌟", "🔥", "❄️", "⛏️", "💀"];

    public static async Task<Bucket?> ShowAsync(Window owner, Bucket? existing, IReadOnlyCollection<string> takenIds)
    {
        Bucket? result = null;

        var initialBudget = existing?.Budget ?? TimeSpan.FromHours(1);
        var name = new TextBox { Text = existing?.Name ?? "", Watermark = "Bucket name" };
        var hours = new NumericUpDown { Minimum = 0, Maximum = 999, Value = (int)initialBudget.TotalHours };
        var minutes = new NumericUpDown { Minimum = 0, Maximum = 59, Value = initialBudget.Minutes };
        var period = new ComboBox
        {
            ItemsSource = Enum.GetValues<ResetPeriod>(),
            SelectedItem = existing?.Period ?? ResetPeriod.Daily,
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };
        var enforcement = new ComboBox
        {
            ItemsSource = Enum.GetValues<Enforcement>(),
            SelectedItem = existing?.Enforcement ?? Enforcement.Remind,
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };
        var isGoal = new CheckBox { Content = "Goal - count time UP toward this target instead of counting a limit down", IsChecked = existing?.IsGoal ?? false };
        var goalHint = new TextBlock
        {
            Text = "Goal buckets are never enforced - no warnings, no closing games. They just show progress toward the target.",
            FontSize = 11,
            Foreground = Palette.Muted,
            TextWrapping = TextWrapping.Wrap,
            IsVisible = isGoal.IsChecked ?? false,
        };
        isGoal.IsCheckedChanged += (_, _) =>
        {
            goalHint.IsVisible = isGoal.IsChecked ?? false;
            enforcement.IsEnabled = !(isGoal.IsChecked ?? false);
        };
        enforcement.IsEnabled = !(isGoal.IsChecked ?? false);
        var icon = new ComboBox
        {
            ItemsSource = IconChoices,
            SelectedItem = string.IsNullOrEmpty(existing?.Icon) ? NoIcon : existing.Icon,
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };

        string? chosenColor = existing?.Color;
        var colorLabel = new TextBlock { Text = chosenColor ?? "Default", Foreground = Palette.Muted, VerticalAlignment = VerticalAlignment.Center };
        var swatchRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        void RefreshSwatchSelection() => colorLabel.Text = chosenColor ?? "Default";
        foreach (var hex in Swatches)
        {
            var swatch = new Button
            {
                Width = 24,
                Height = 24,
                CornerRadius = new CornerRadius(12),
                Background = Brush.Parse(hex),
                BorderThickness = new Thickness(0),
            };
            swatch.Click += (_, _) => { chosenColor = hex; RefreshSwatchSelection(); };
            swatchRow.Children.Add(swatch);
        }
        var clearColor = new Button { Content = "Default" };
        clearColor.Click += (_, _) => { chosenColor = null; RefreshSwatchSelection(); };

        var error = new TextBlock { Foreground = Palette.Empty, IsVisible = false, TextWrapping = TextWrapping.Wrap };
        var save = new Button { Content = existing is null ? "Add" : "Save", Background = Palette.Plenty };
        var cancel = new Button { Content = "Cancel" };

        var dialog = new Window
        {
            Title = existing is null ? "Add bucket" : "Edit bucket",
            Width = 380,
            SizeToContent = SizeToContent.Height,
            CanResize = false,
            Background = Palette.Background,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Content = new StackPanel
            {
                Margin = new Thickness(20),
                Spacing = 12,
                Children =
                {
                    Field("Name", name),
                    new Grid
                    {
                        ColumnDefinitions = new ColumnDefinitions("*,*"),
                        Children =
                        {
                            Col(Field("Hours", hours), 0),
                            Col(Field("Minutes", minutes), 1),
                        },
                    },
                    Field("Resets", period),
                    Field("Enforcement", enforcement),
                    new StackPanel { Spacing = 4, Children = { isGoal, goalHint } },
                    Field("Icon", icon),
                    new StackPanel
                    {
                        Spacing = 6,
                        Children =
                        {
                            new TextBlock { Text = "Color", Foreground = Palette.Muted, FontSize = 12 },
                            new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10, Children = { swatchRow, clearColor, colorLabel } },
                        },
                    },
                    error,
                    new StackPanel
                    {
                        Orientation = Orientation.Horizontal,
                        Spacing = 8,
                        HorizontalAlignment = HorizontalAlignment.Right,
                        Children = { cancel, save },
                    },
                },
            },
        };

        save.Click += (_, _) =>
        {
            var trimmedName = name.Text?.Trim() ?? "";
            if (trimmedName.Length == 0)
            {
                error.Text = "Name can't be empty.";
                error.IsVisible = true;
                return;
            }

            var budget = TimeSpan.FromHours((double)(hours.Value ?? 0)) + TimeSpan.FromMinutes((double)(minutes.Value ?? 0));
            var id = existing?.Id ?? Slugify(trimmedName, takenIds);
            result = new Bucket(
                id,
                trimmedName,
                budget,
                (ResetPeriod)period.SelectedItem!,
                (Enforcement)enforcement.SelectedItem!,
                chosenColor,
                icon.SelectedItem as string is null or NoIcon ? null : (string)icon.SelectedItem,
                isGoal.IsChecked ?? false);
            dialog.Close();
        };
        cancel.Click += (_, _) => dialog.Close();

        await dialog.ShowDialog(owner);
        return result;
    }

    private static Control Field(string label, Control input) => new StackPanel
    {
        Spacing = 4,
        Children = { new TextBlock { Text = label, Foreground = Palette.Muted, FontSize = 12 }, input },
    };

    private static Control Col(Control c, int column)
    {
        Grid.SetColumn(c, column);
        return c;
    }

    private static string Slugify(string name, IReadOnlyCollection<string> takenIds)
    {
        var baseId = new string(name.ToLowerInvariant().Where(c => char.IsLetterOrDigit(c) || c == '-').ToArray());
        if (baseId.Length == 0) baseId = "bucket";
        var id = baseId;
        int n = 2;
        while (takenIds.Contains(id))
            id = $"{baseId}-{n++}";
        return id;
    }
}

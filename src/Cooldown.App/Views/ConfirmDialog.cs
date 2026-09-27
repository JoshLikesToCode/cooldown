using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace Cooldown.App.Views;

/// <summary>Small Yes/No modal. Avalonia has no built-in MessageBox.</summary>
internal static class ConfirmDialog
{
    public static async Task<bool> ShowAsync(Window owner, string title, string message)
    {
        var result = false;
        var yes = new Button { Content = "Delete", Background = Palette.Empty, Foreground = Palette.Text };
        var no = new Button { Content = "Cancel" };

        var dialog = new Window
        {
            Title = title,
            Width = 360,
            SizeToContent = SizeToContent.Height,
            CanResize = false,
            Background = Palette.Background,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Content = new StackPanel
            {
                Margin = new Thickness(20),
                Spacing = 16,
                Children =
                {
                    new TextBlock { Text = message, Foreground = Palette.Text, TextWrapping = TextWrapping.Wrap },
                    new StackPanel
                    {
                        Orientation = Orientation.Horizontal,
                        Spacing = 8,
                        HorizontalAlignment = HorizontalAlignment.Right,
                        Children = { no, yes },
                    },
                },
            },
        };

        yes.Click += (_, _) => { result = true; dialog.Close(); };
        no.Click += (_, _) => dialog.Close();

        await dialog.ShowDialog(owner);
        return result;
    }
}

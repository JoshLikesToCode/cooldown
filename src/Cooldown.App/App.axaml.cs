using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Platform;
using Avalonia.Threading;
using Cooldown.App.Views;

namespace Cooldown.App;

public partial class App : Application
{
    public static AppOptions Options { get; set; } = new();

    private AppHost? _host;
    private MainWindow? _window;

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            _host = new AppHost(Options, new OverlayNotifier());
            _window = new MainWindow(_host);
            _host.Tracker.Ticked += () => Dispatcher.UIThread.Post(_window.Refresh);

            SetUpTray(desktop);
            desktop.ShutdownRequested += (_, _) => _host.Dispose();

            if (!Options.StartHidden)
                _window.Show();

            _host.Start();
        }
        base.OnFrameworkInitializationCompleted();
    }

    private void SetUpTray(IClassicDesktopStyleApplicationLifetime desktop)
    {
        var open = new NativeMenuItem { Header = "Open Cooldown" };
        open.Click += (_, _) => ShowWindow();

        var quit = new NativeMenuItem { Header = "Quit" };
        quit.Click += (_, _) =>
        {
            _window!.AllowClose = true;
            desktop.Shutdown();
        };

        var tray = new TrayIcon
        {
            Icon = new WindowIcon(AssetLoader.Open(new Uri("avares://Cooldown/Assets/cooldown.png"))),
            ToolTipText = "Cooldown",
        };

        var menu = new NativeMenu();
        menu.Items.Add(open);
        menu.Items.Add(new NativeMenuItemSeparator());
        menu.Items.Add(quit);
        tray.Menu = menu;

        tray.Clicked += (_, _) => ShowWindow();
        TrayIcon.SetIcons(this, new TrayIcons { tray });
    }

    private void ShowWindow()
    {
        _window!.Show();
        _window.Activate();
    }
}

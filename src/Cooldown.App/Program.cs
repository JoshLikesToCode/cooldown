using Avalonia;
using Avalonia.Controls;

namespace Cooldown.App;

internal static class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        App.Options = AppOptions.Parse(args);
        return BuildAvaloniaApp().StartWithClassicDesktopLifetime(args, ShutdownMode.OnExplicitShutdown);
    }

    // Also used by the Avalonia previewer.
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>().UsePlatformDetect().LogToTrace();
}

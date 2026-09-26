using System.Globalization;

namespace Cooldown.App;

/// <summary>
/// Command line options.
///   --fake              Use scripted fake games instead of Steam (default on non-Windows).
///   --scenario FILE     Script for fake mode. See scenarios/demo.json.
///   --speed N           Fake mode only. Run the clock N times faster than real time.
///   --hidden            Start in the tray without opening the window.
/// </summary>
public sealed record AppOptions(bool Fake, string? Scenario, double Speed, bool StartHidden)
{
    public AppOptions() : this(false, null, 1, false) { }

    public static AppOptions Parse(string[] args)
    {
        var o = new AppOptions(Fake: !OperatingSystem.IsWindows(), Scenario: null, Speed: 60, StartHidden: false);
        for (int i = 0; i < args.Length; i++)
        {
            o = args[i] switch
            {
                "--fake" => o with { Fake = true },
                "--scenario" when i + 1 < args.Length => o with { Fake = true, Scenario = args[++i] },
                "--speed" when i + 1 < args.Length => o with { Speed = double.Parse(args[++i], CultureInfo.InvariantCulture) },
                "--hidden" => o with { StartHidden = true },
                _ => o,
            };
        }
        return o;
    }
}

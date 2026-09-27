# Cooldown

Set time budgets for groups of Steam games. When a group runs out, Cooldown reminds you or closes the game.

Put your competitive shooters in one group with an hour a day. Put long story games in another with ten hours a week. Cooldown tracks what you play, warns you as time runs low, and enforces the limit you picked.

It's built for adults managing their own habits, not for parental control. It's easy to quit on purpose. The point is to make you decide, instead of drifting past your limit.

> Status: early. Windows detection and enforcement work, with an in-app Settings screen for everything below. Linux detection, and a packaged installer, are next. See the roadmap.

## How it works

Every few seconds Cooldown asks Steam which game is running. Steam keeps this in the registry on Windows, so no hooking or injection is involved. That matters for anti-cheat: Cooldown never touches game memory.

Playtime is stored locally in SQLite. Games are organized into buckets, each with a budget, a reset period (daily, weekly or monthly), and how it's enforced. A game can belong to more than one bucket at once. When a bucket gets low you get warnings at 10, 5 and 1 minute (also configurable). When it's empty:

- **Remind** buckets show a notice and let you keep playing.
- **Block** buckets give you a grace period to save, ask the game to close, then force it closed if needed. Launching a game from an empty bucket gets a shorter grace period.

There's also a **Goal** mode for the opposite case - games you're trying to play *more* of. Instead of counting a limit down, a goal bucket counts time *up* toward a target (say, two hours of a learning game this month) and is never enforced; it's just a progress bar.

Warnings appear in a small always-on-top window, not as system notifications. Windows silences notifications while you're gaming, which is exactly when these matter. The window never takes focus from the game. It won't show over exclusive fullscreen games, so borderless windowed mode works best.

Everything - buckets, which games are assigned to them, colors and icons, goals, and global timing settings - is configured from the **Settings** button in the main window. There's nothing to hand-edit to get started.

## Developing

You need the .NET 10 SDK. On Ubuntu or Kubuntu 24.04: `sudo apt install dotnet-sdk-10.0`.

```bash
dotnet test
dotnet run --project src/Cooldown.App -- --fake --scenario scenarios/demo.json
```

Fake mode replaces Steam with a scripted list of games and runs the clock 60 times faster. The demo plays an hour of Counter-Strike 2 in about a minute, so you can watch the warnings, the grace period and the close happen. Use `--speed 1` for real time. Fake mode keeps its data in a separate folder and starts fresh each run.

### Testing on a Windows PC from Linux

1. On the Windows PC, run `scripts/windows-setup.ps1` once in an admin PowerShell. It turns on the built-in SSH server and creates a task that launches Cooldown on your desktop.
2. Set up SSH key login from your Linux machine.
3. Then, from Linux:

```bash
export COOLDOWN_HOST=you@gaming-pc
scripts/deploy.sh   # build for Windows, copy, restart
scripts/logs.sh     # follow the log live
```

The deploy script uses the scheduled task because programs started over SSH run in a hidden session and can't show windows.

A quick detection check without launching a game: in `regedit`, set `HKEY_CURRENT_USER\Software\Valve\Steam\RunningAppID` to a game's App ID.

### Project layout

| Project | Purpose |
| --- | --- |
| `Cooldown.Core` | Budgets, reset periods, the tracking loop. No platform code. |
| `Cooldown.Steam` | Reads Steam's library files to map App IDs to names and folders. |
| `Cooldown.Data` | SQLite storage for play sessions. |
| `Cooldown.Platform.Windows` | Detects the running game and closes it on Windows. |
| `Cooldown.Platform.Fake` | Scripted games and a fast clock for testing anywhere. |
| `Cooldown.App` | Avalonia tray app and warning windows. |

Every project targets plain `net10.0`, so the whole solution builds on Linux. Windows-only code is marked with `[SupportedOSPlatform("windows")]`.

## Roadmap

- Packaged installer, so running Cooldown doesn't require the .NET SDK, a terminal, or this repo
- Documented single-command setup for a non-technical user - install, open, done, no config file involved at any point
- Linux detection using the `SteamAppId` value Steam sets on game processes (covers native and Proton games)
- Suggested buckets from Steam store genres
- Richer playtime history (charts over time, not just the running totals on the Assignments tab)
- Optional friction to override a block, like typing a phrase
- Steam Deck support through a Decky Loader plugin

## License

MIT

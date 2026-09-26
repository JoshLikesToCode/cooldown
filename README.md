# Cooldown

Set time budgets for groups of Steam games. When a group runs out, Cooldown reminds you or closes the game.

Put your competitive shooters in one group with an hour a day. Put long story games in another with ten hours a week. Cooldown tracks what you play, warns you as time runs low, and enforces the limit you picked.

It's built for adults managing their own habits, not for parental control. It's easy to quit on purpose. The point is to make you decide, instead of drifting past your limit.

> Status: early. Windows detection and enforcement work. Linux detection is planned.

## How it works

Every few seconds Cooldown asks Steam which game is running. Steam keeps this in the registry on Windows, so no hooking or injection is involved. That matters for anti-cheat: Cooldown never touches game memory.

Playtime is stored locally in SQLite. Each group has a budget and a reset period (daily or weekly). When a group gets low you get warnings at 10, 5 and 1 minute. When it's empty:

- **Remind** groups show a notice and let you keep playing.
- **Block** groups give you a grace period to save, ask the game to close, then force it closed if needed. Launching a game from an empty group gets a shorter grace period.

Warnings appear in a small always-on-top window, not as system notifications. Windows silences notifications while you're gaming, which is exactly when these matter. The window never takes focus from the game. It won't show over exclusive fullscreen games, so borderless windowed mode works best.

## Setting it up

On first run Cooldown creates `config.json` in `%APPDATA%\Cooldown`. It also writes `installed-games.txt` there, listing every installed game with its Steam App ID.

```json
{
  "buckets": [
    { "id": "competitive", "name": "Competitive", "budget": "01:00:00", "period": "Daily", "enforcement": "Block" },
    { "id": "story", "name": "Story games", "budget": "10:00:00", "period": "Weekly", "enforcement": "Remind" }
  ],
  "assignments": {
    "730": "competitive",
    "1145360": "story"
  },
  "dayStartHour": 4,
  "weekStart": "Monday",
  "warningMinutes": [10, 5, 1],
  "graceSeconds": 60,
  "launchGraceSeconds": 15,
  "pollSeconds": 5
}
```

Games not listed under `assignments` are tracked but never limited. `dayStartHour` sets when a day resets, so a 1 AM session counts toward the evening before. Restart Cooldown after editing. (A settings screen is on the roadmap.)

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

- Settings screen for groups and game assignments
- Suggested groups from Steam store genres
- Linux detection using the `SteamAppId` value Steam sets on game processes (covers native and Proton games)
- Weekly history view
- Optional friction to override a block, like typing a phrase
- Steam Deck support through a Decky Loader plugin

## License

MIT

#!/usr/bin/env bash
# Build a Windows copy on Linux, send it to the gaming PC, and restart it there.
# One-time setup on the PC: run scripts/windows-setup.ps1 in an admin PowerShell.
set -euo pipefail

HOST="${COOLDOWN_HOST:?Set COOLDOWN_HOST first, e.g. export COOLDOWN_HOST=josh@gaming-pc}"
REMOTE_DIR="${COOLDOWN_REMOTE_DIR:-C:/Tools/Cooldown}"
REMOTE_DIR_WIN="${REMOTE_DIR//\//\\}"
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
OUT="$ROOT/artifacts/win-x64"

echo "==> Publishing for Windows"
rm -rf "$OUT"
dotnet publish "$ROOT/src/Cooldown.App" -c Release -r win-x64 --self-contained \
  -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o "$OUT"

echo "==> Stopping the running copy"
ssh "$HOST" "taskkill /IM Cooldown.exe /F >NUL 2>&1 & exit 0"

echo "==> Copying to $HOST:$REMOTE_DIR"
ssh "$HOST" "if not exist \"$REMOTE_DIR_WIN\" mkdir \"$REMOTE_DIR_WIN\""
scp -q "$OUT"/* "$HOST:$REMOTE_DIR/"

# Programs started directly over SSH run in a hidden session and can't show windows.
# The scheduled task starts Cooldown in your normal desktop session instead.
echo "==> Starting on the desktop"
ssh "$HOST" "schtasks /Run /TN CooldownDev >NUL"

echo "Done. Watch it with: scripts/logs.sh"

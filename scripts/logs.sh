#!/usr/bin/env bash
# Follow the app's log on the gaming PC.
set -euo pipefail
HOST="${COOLDOWN_HOST:?Set COOLDOWN_HOST first, e.g. export COOLDOWN_HOST=josh@gaming-pc}"
ssh -t "$HOST" 'powershell -NoProfile -Command "Get-Content -Wait -Tail 50 $env:APPDATA\Cooldown\logs\cooldown.log"'

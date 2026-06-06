#!/usr/bin/env bash
set -euo pipefail

cd "$(dirname "$0")"

PID_FILE="${PID_FILE:-telegram_codex_bot.pid}"

is_bot_pid() {
  local pid="$1"
  local command
  command="$(ps -p "$pid" -o args= 2>/dev/null || true)"
  [[ "$command" == *"telegram_codex_bot.py"* ]]
}

if [[ ! -f "$PID_FILE" ]]; then
  echo "Bot is not running: $PID_FILE not found"
  exit 1
fi

pid="$(cat "$PID_FILE")"
if [[ -n "$pid" ]] && is_bot_pid "$pid"; then
  echo "Bot is running with PID $pid"
else
  echo "Bot is not running, but stale PID file exists: $PID_FILE"
  exit 1
fi

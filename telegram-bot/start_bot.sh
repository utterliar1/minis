#!/usr/bin/env bash
set -euo pipefail

cd "$(dirname "$0")"

PID_FILE="${PID_FILE:-telegram_codex_bot.pid}"
LOG_FILE="${LOG_FILE:-telegram_codex_bot.log}"

is_bot_pid() {
  local pid="$1"
  local command
  command="$(ps -p "$pid" -o args= 2>/dev/null || true)"
  [[ "$command" == *"telegram_codex_bot.py"* ]]
}

if [[ -f "$PID_FILE" ]]; then
  pid="$(cat "$PID_FILE")"
  if [[ -n "$pid" ]] && is_bot_pid "$pid"; then
    echo "Bot is already running with PID $pid"
    exit 0
  fi
  rm -f "$PID_FILE"
fi

nohup setsid python3 telegram_codex_bot.py >>"$LOG_FILE" 2>&1 &
pid="$!"
echo "$pid" >"$PID_FILE"

sleep 1
if kill -0 "$pid" 2>/dev/null; then
  echo "Bot started with PID $pid"
  echo "Log file: $LOG_FILE"
else
  echo "Bot failed to start. Recent logs:"
  tail -n 40 "$LOG_FILE" || true
  exit 1
fi

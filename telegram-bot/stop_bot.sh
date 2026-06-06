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
  exit 0
fi

pid="$(cat "$PID_FILE")"
if [[ -z "$pid" ]] || ! is_bot_pid "$pid"; then
  rm -f "$PID_FILE"
  echo "Bot is not running"
  exit 0
fi

kill "$pid"
for _ in {1..20}; do
  if ! kill -0 "$pid" 2>/dev/null; then
    rm -f "$PID_FILE"
    echo "Bot stopped"
    exit 0
  fi
  sleep 0.5
done

echo "Bot did not stop within 10 seconds. PID: $pid"
exit 1

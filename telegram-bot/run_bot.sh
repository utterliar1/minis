#!/usr/bin/env bash
set -euo pipefail

cd "$(dirname "$0")"
exec python3 telegram_codex_bot.py

#!/usr/bin/env bash
# Starts the API and the web app, waits until both answer, runs the Playwright tests, then stops everything.
#
# Usage: scripts/run-e2e.sh [--category Smoke|Regression] [--headed] [--browser Chromium|Firefox|WebKit|chrome|msedge]
#   Extra arguments after "--" go to `dotnet test`.
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
CATEGORY=""
export E2E_Browser__Headless="${E2E_Browser__Headless:-true}"

while [[ $# -gt 0 ]]; do
  case "$1" in
    --category) CATEGORY="$2"; shift 2 ;;
    --headed) export E2E_Browser__Headless=false; shift ;;
    --browser) export E2E_Browser__Name="$2"; shift 2 ;;
    --) shift; break ;;
    *) echo "Unknown option: $1" >&2; exit 64 ;;
  esac
done

API_URL="http://localhost:5080"
WEB_URL="http://localhost:5180"
PIDS=()

cleanup() {
  for pid in "${PIDS[@]:-}"; do
    # Each app runs in its own process group, so the child processes (dotnet, node) stop as well.
    [[ -n "$pid" ]] && kill -- "-$pid" 2>/dev/null || true
  done
}
trap cleanup EXIT

wait_for() {
  local url="$1" name="$2"
  for _ in $(seq 1 60); do
    curl -fsS "$url" >/dev/null 2>&1 && return 0
    sleep 1
  done
  echo "$name did not become ready at $url" >&2
  exit 2
}

for url in "$API_URL/health" "$WEB_URL"; do
  if curl -fsS "$url" >/dev/null 2>&1; then
    echo "Something already answers on $url. Stop it first, so the tests never hit the wrong app." >&2
    exit 2
  fi
done

setsid dotnet run --project "$ROOT/src/E2eStarter.Api" >"$ROOT/api.log" 2>&1 &
PIDS+=($!)
(cd "$ROOT/src/web" && exec setsid npm run dev >"$ROOT/web.log" 2>&1) &
PIDS+=($!)

wait_for "$API_URL/health" "API"
wait_for "$WEB_URL" "Web app"

FILTER=()
[[ -n "$CATEGORY" ]] && FILTER=(--filter-trait "Category=$CATEGORY")

dotnet test --project "$ROOT/tests/E2eStarter.E2E" "${FILTER[@]}" "$@"

#!/usr/bin/env bash
# Runs the E2E tests against an already running app and writes the dashboard into a run folder:
#   events.jsonl (live progress), evidence/<test>/ (failures), results.trx, data.json, index.html, report.html.
#
# Usage: scripts/run-ci.sh <run folder> [category] [-- extra dotnet test arguments]
# Called that way by E2eStarter.AspNetCore for every run; also usable by hand (start the app first, or use
# scripts/run-e2e.sh for the full cycle). Optional environment:
#   AUTOMATED_TESTS_FILTER_FILE  one fully qualified test method per line; only those run (used by "Re-run failed")
#   E2E_PREVIOUS_DATA            data.json of the previous run, to mark new failures and tests that are still failing
#   E2E_Title, E2E_ENVIRONMENT   dashboard title and environment label
set -uo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
RUN_DIR="${1:?Usage: run-ci.sh <run folder> [category] [-- dotnet test arguments]}"
shift
CATEGORY=""
if [[ $# -gt 0 && "$1" != "--" ]]; then CATEGORY="$1"; shift; fi
[[ "${1:-}" == "--" ]] && shift
mkdir -p "$RUN_DIR"
RUN_DIR="$(cd "$RUN_DIR" && pwd)"

export E2E_Artifacts__Directory="$RUN_DIR"
export E2E_RUN_ID=evidence
export E2E_LiveEventsPath="$RUN_DIR/events.jsonl"
export E2E_Browser__Headless="${E2E_Browser__Headless:-true}"

FILTER=()
[[ -n "$CATEGORY" ]] && FILTER+=(--filter-trait "Category=$CATEGORY")
if [[ -n "${AUTOMATED_TESTS_FILTER_FILE:-}" && -f "$AUTOMATED_TESTS_FILTER_FILE" ]]; then
  while IFS= read -r test || [[ -n "$test" ]]; do
    test="${test%%(*}"
    test="${test//[[:space:]]/}"
    [[ -n "$test" ]] && FILTER+=(--filter-method "$test")
  done <"$AUTOMATED_TESTS_FILTER_FILE"
fi

dotnet test --project "$ROOT/tests/E2eStarter.E2E" "${FILTER[@]}" "$@" \
  --report-trx --report-trx-filename results.trx --results-directory "$RUN_DIR"
TEST_STATUS=$?

PREVIOUS=()
[[ -n "${E2E_PREVIOUS_DATA:-}" && -f "$E2E_PREVIOUS_DATA" ]] && PREVIOUS=(--previous "$E2E_PREVIOUS_DATA")

if [[ -f "$RUN_DIR/results.trx" ]]; then
  dotnet run --project "$ROOT/src/E2eStarter.Reporters" -- \
    --trx "$RUN_DIR/results.trx" \
    --artifacts "$RUN_DIR/evidence" \
    --out "$RUN_DIR" \
    --run-id "$(basename "$RUN_DIR")" \
    --title "${E2E_Title:-UI tests}" \
    --env "${E2E_ENVIRONMENT:-local}" \
    --base-url "${E2E_BaseUrl:-http://localhost:5180}" \
    "${PREVIOUS[@]}"
fi

exit $TEST_STATUS

#!/usr/bin/env bash
# Runs the E2E tests against an already running app and writes the dashboard into a run folder.
# Called by E2eStarter.AspNetCore as: scripts/run-ci.sh <run folder> [category]
# Also usable by hand (start the app first, or use scripts/run-e2e.sh for the full cycle).
set -uo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
RUN_DIR="${1:?Usage: run-ci.sh <run folder> [category]}"
CATEGORY="${2:-}"
mkdir -p "$RUN_DIR"
RUN_DIR="$(cd "$RUN_DIR" && pwd)"

export E2E_RUN_ID=evidence
export E2E_Artifacts__Directory="$RUN_DIR/artifacts"
export E2E_Browser__Headless="${E2E_Browser__Headless:-true}"

FILTER=()
[[ -n "$CATEGORY" ]] && FILTER=(--filter-trait "Category=$CATEGORY")

dotnet test --project "$ROOT/tests/E2eStarter.E2E" "${FILTER[@]}" \
  --report-trx --report-trx-filename results.trx --results-directory "$RUN_DIR"
TEST_STATUS=$?

PREVIOUS=()
[[ -n "${E2E_PREVIOUS_DATA:-}" && -f "$E2E_PREVIOUS_DATA" ]] && PREVIOUS=(--previous "$E2E_PREVIOUS_DATA")

if [[ -f "$RUN_DIR/results.trx" ]]; then
  dotnet run --project "$ROOT/src/E2eStarter.Reporters" -- \
    --trx "$RUN_DIR/results.trx" \
    --artifacts "$RUN_DIR/artifacts/evidence" \
    --out "$RUN_DIR" \
    --run-id "$(basename "$RUN_DIR")" \
    --title "${AUTOMATED_TESTS_TITLE:-E2E tests}" \
    --env "${AUTOMATED_TESTS_ENV:-local}" \
    --base-url "${E2E_BaseUrl:-http://localhost:5180}" \
    "${PREVIOUS[@]}"
fi

exit $TEST_STATUS

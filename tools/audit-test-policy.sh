#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$ROOT_DIR"

empire_specs=0
empire_browser_tests=0
forbidden=()

# Scan repository-owned files only. `git ls-files` includes tracked and new
# untracked files while respecting .gitignore, so dependency and build output
# test files do not create false positives.
while IFS= read -r -d '' source; do
  [[ -f "$source" ]] || continue
  lower_source="${source,,}"
  basename="${lower_source##*/}"
  is_test_source=0

  # Match the policy literally instead of maintaining a narrow extension list:
  # a test written as .cy.mjs, .spec.vue, in __tests__/, or as FooTests.cs
  # is still automated test source.
  case "$lower_source" in
    *.spec.*|*.test.*|*.cy.*|*/__tests__/*|*/tests/*|*/test/*|*/specs/*|*/spec/*|*/cypress/e2e/*)
      is_test_source=1
      ;;
  esac
  case "$basename" in
    test_*.py|*_test.py|*_test.go|*_tests.rs|*tests.cs|*.tests.csproj|*.test.csproj)
      is_test_source=1
      ;;
  esac
  (( is_test_source == 1 )) || continue

  case "$lower_source" in
    web/vueclient/src/features/empires-endgame/*)
      empire_specs=$((empire_specs + 1))
      ;;
    web/vueclient/cypress/e2e/empires-endgame*.cy.*)
      empire_browser_tests=$((empire_browser_tests + 1))
      ;;
    *)
      forbidden+=("$source")
      ;;
  esac
done < <(git ls-files --cached --others --exclude-standard -z)

if (( ${#forbidden[@]} > 0 )); then
  echo "test-policy: forbidden automated test sources found:" >&2
  printf '  %s\n' "${forbidden[@]}" >&2
  echo "Only Empire's Endgame tests are permitted, under:" >&2
  echo "  Web/VueClient/src/features/empires-endgame/**/*.spec.*" >&2
  echo "  Web/VueClient/src/features/empires-endgame/**/*.test.*" >&2
  echo "  Web/VueClient/cypress/e2e/empires-endgame*.cy.*" >&2
  exit 1
fi

if (( empire_specs == 0 || empire_browser_tests == 0 )); then
  echo "test-policy: Empire's Endgame integrated test gate is incomplete." >&2
  echo "Found $empire_specs Vitest specs and $empire_browser_tests Cypress tests." >&2
  exit 1
fi

echo "test-policy: PASS ($empire_specs Empire unit-test sources, $empire_browser_tests Empire Cypress tests; no automated test sources elsewhere)."

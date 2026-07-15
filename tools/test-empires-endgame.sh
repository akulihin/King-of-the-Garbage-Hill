#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
CLIENT_DIR="$ROOT_DIR/Web/VueClient"
PORT="${EMPIRES_E2E_PORT:-4174}"
BASE_URL="http://127.0.0.1:$PORT"
VITE_LOG="${TMPDIR:-/tmp}/empires-endgame-vite-$$.log"
RUN_UNITS=1
RUN_BROWSER=1

case "${1:-}" in
  --unit-only) RUN_BROWSER=0 ;;
  --e2e-only) RUN_UNITS=0 ;;
  "") ;;
  *)
    echo "Usage: $0 [--unit-only|--e2e-only]" >&2
    exit 2
    ;;
esac

cleanup() {
  if [[ -n "${VITE_PID:-}" ]]; then
    kill "$VITE_PID" 2>/dev/null || true
    wait "$VITE_PID" 2>/dev/null || true
  fi
  rm -f "$VITE_LOG"
}
trap cleanup EXIT INT TERM

prepare_cypress_runtime() {
  local version cache_root binary missing runtime_root runtime_lib download_dir package
  version="$(node -p "require('cypress/package.json').version")"
  cache_root="${CYPRESS_CACHE_FOLDER:-$HOME/.cache/Cypress}"
  binary="${CYPRESS_RUN_BINARY:-$cache_root/$version/Cypress/Cypress}"

  if [[ ! -x "$binary" ]]; then
    echo "Installing the pinned Cypress $version browser binary..."
    pnpm exec cypress install
  fi

  if ! command -v ldd >/dev/null || [[ ! -x "$binary" ]]; then
    return
  fi
  missing="$(ldd "$binary" 2>/dev/null | awk '/not found/ { print $1 }')"
  if [[ "$missing" != *libnss3.so* && "$missing" != *libnspr4.so* ]]; then
    return
  fi
  if ! command -v apt-get >/dev/null || ! command -v dpkg-deb >/dev/null; then
    echo "Cypress needs libnss3 and libnspr4. Install them with your system package manager." >&2
    exit 1
  fi

  runtime_root="${XDG_CACHE_HOME:-$HOME/.cache}/empires-endgame/cypress-runtime"
  runtime_lib="$(find "$runtime_root" -type f -name libnss3.so -printf '%h\n' -quit 2>/dev/null || true)"
  if [[ -z "$runtime_lib" ]]; then
    echo "Unpacking libnss3 and libnspr4 into the user cache for Cypress..."
    download_dir="$(mktemp -d)"
    (
      cd "$download_dir"
      apt-get download libnss3 libnspr4
    )
    mkdir -p "$runtime_root"
    for package in "$download_dir"/*.deb; do
      dpkg-deb -x "$package" "$runtime_root"
    done
    rm -rf "$download_dir"
    runtime_lib="$(find "$runtime_root" -type f -name libnss3.so -printf '%h\n' -quit)"
  fi
  export LD_LIBRARY_PATH="$runtime_lib${LD_LIBRARY_PATH:+:$LD_LIBRARY_PATH}"
}

cd "$CLIENT_DIR"

if [[ "$RUN_UNITS" -eq 1 ]]; then
  pnpm run test:empires
fi

if [[ "$RUN_BROWSER" -eq 0 ]]; then
  exit 0
fi

prepare_cypress_runtime

node node_modules/vite/bin/vite.js --host 127.0.0.1 --port "$PORT" --strictPort >"$VITE_LOG" 2>&1 &
VITE_PID=$!
VITE_READY=0

for _ in {1..120}; do
  if ! kill -0 "$VITE_PID" 2>/dev/null; then
    cat "$VITE_LOG" >&2
    exit 1
  fi
  # Require Vite's own ready marker as well as an HTTP response. A stale
  # process on this port can satisfy curl while strict-port Vite is still
  # starting up and about to exit.
  if grep --quiet 'Local:' "$VITE_LOG" \
    && curl --fail --silent "$BASE_URL/empires-endgame" >/dev/null 2>&1; then
    VITE_READY=1
    break
  fi
  sleep 0.25
done

if [[ "$VITE_READY" -ne 1 ]] || ! kill -0 "$VITE_PID" 2>/dev/null; then
  cat "$VITE_LOG" >&2
  echo "Vite did not become ready at $BASE_URL." >&2
  exit 1
fi

if [[ -n "${CYPRESS_BROWSER:-}" ]]; then
  CYPRESS_BASE_URL="$BASE_URL" pnpm run test:empires:e2e -- --browser "$CYPRESS_BROWSER"
else
  CYPRESS_BASE_URL="$BASE_URL" pnpm run test:empires:e2e
fi

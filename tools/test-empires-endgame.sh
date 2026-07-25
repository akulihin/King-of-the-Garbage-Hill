#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
CLIENT_DIR="$ROOT_DIR/Web/VueClient"
PORT="${EMPIRES_E2E_PORT:-4174}"
BASE_URL="http://127.0.0.1:$PORT"
RUN_UNITS=1
RUN_BROWSER=1

create_task_temp_dir() {
  local candidate task_dir

  for candidate in "${TMPDIR:-}" "${TEMP:-}" "${TMP:-}" /tmp; do
    if [[ -z "$candidate" || ! -d "$candidate" || ! -w "$candidate" ]]; then
      continue
    fi
    if task_dir="$(mktemp -d "$candidate/empires-endgame.XXXXXX" 2>/dev/null)"; then
      printf '%s\n' "$task_dir"
      return 0
    fi
  done

  echo "Could not create a writable temporary directory for the Empire's Endgame test gate." >&2
  return 1
}

EMPIRES_TASK_TEMP_DIR="$(create_task_temp_dir)"
export TMPDIR="$EMPIRES_TASK_TEMP_DIR"
export TEMP="$EMPIRES_TASK_TEMP_DIR"
export TMP="$EMPIRES_TASK_TEMP_DIR"
export XDG_CONFIG_HOME="$EMPIRES_TASK_TEMP_DIR/xdg-config"
export XDG_CACHE_HOME="$EMPIRES_TASK_TEMP_DIR/xdg-cache"
mkdir -p "$XDG_CONFIG_HOME" "$XDG_CACHE_HOME"
VITE_LOG="$EMPIRES_TASK_TEMP_DIR/vite.log"

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
  rm -f -- "$VITE_LOG"
  if [[ -n "${EMPIRES_TASK_TEMP_DIR:-}" && -d "$EMPIRES_TASK_TEMP_DIR" ]]; then
    rm -rf -- "$EMPIRES_TASK_TEMP_DIR"
  fi
}
trap cleanup EXIT INT TERM

prepare_cypress_runtime() {
  local version cache_root binary binary_dir binary_state_dir state_cache_root
  local missing runtime_root runtime_lib download_dir package
  version="$(node -p "require('cypress/package.json').version")"
  cache_root="${CYPRESS_CACHE_FOLDER:-$HOME/.cache/Cypress}"
  binary="${CYPRESS_RUN_BINARY:-$cache_root/$version/Cypress/Cypress}"

  if [[ ! -x "$binary" ]]; then
    if [[ -z "${CYPRESS_RUN_BINARY:-}" && -z "${CYPRESS_CACHE_FOLDER:-}" ]]; then
      export CYPRESS_CACHE_FOLDER="$EMPIRES_TASK_TEMP_DIR/cypress-cache"
      cache_root="$CYPRESS_CACHE_FOLDER"
      binary="$cache_root/$version/Cypress/Cypress"
    fi
    echo "Installing the pinned Cypress $version browser binary..."
    pnpm exec cypress install
  fi

  binary="$(node -e "process.stdout.write(require('fs').realpathSync(process.argv[1]))" "$binary")"
  binary_dir="$(dirname "$binary")"
  binary_state_dir="$(dirname "$binary_dir")"
  if [[ ! -w "$binary_state_dir" ]]; then
    # Cypress writes binary_state.json beside the binary directory. Keep the
    # real installation read-only while preserving Cypress's normal verify step
    # through a writable cache facade.
    state_cache_root="$EMPIRES_TASK_TEMP_DIR/cypress-cache"
    mkdir -p "$state_cache_root/$version"
    ln -s "$binary_dir" "$state_cache_root/$version/Cypress"
    if [[ -f "$binary_state_dir/binary_state.json" ]]; then
      cp "$binary_state_dir/binary_state.json" "$state_cache_root/$version/binary_state.json"
    fi
    export CYPRESS_CACHE_FOLDER="$state_cache_root"
    unset CYPRESS_RUN_BINARY
    binary="$state_cache_root/$version/Cypress/Cypress"
  else
    # Cypress otherwise derives a different binary cache from the task-local
    # XDG cache even though the pinned binary was resolved above.
    export CYPRESS_RUN_BINARY="$binary"
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

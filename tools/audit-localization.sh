#!/usr/bin/env bash
set -euo pipefail

cd "$(dirname "$0")/.."
characters=King-of-the-Garbage-Hill/DataBase/characters.json
catalog=King-of-the-Garbage-Hill/DataBase/localization.en.json
tmp=$(mktemp -d)
trap 'rm -rf "$tmp"' EXIT

jq empty "$characters"
jq empty "$catalog"

jq -r '.[].Name' "$characters" | sort -u > "$tmp/characters-source"
jq -r '.characters | keys[]' "$catalog" | sort -u > "$tmp/characters-catalog"
jq -r '[.[] | .Passive[].PassiveName] | unique[]' "$characters" | sort -u > "$tmp/passives-source"
jq -r '.passives | keys[]' "$catalog" | sort -u > "$tmp/passives-catalog"

missing_characters=$(comm -23 "$tmp/characters-source" "$tmp/characters-catalog")
missing_passives=$(comm -23 "$tmp/passives-source" "$tmp/passives-catalog")

if [[ -n "$missing_characters" ]]; then
  echo "Missing English character content:"
  echo "$missing_characters"
  exit 1
fi
if [[ -n "$missing_passives" ]]; then
  echo "Missing English passive content:"
  echo "$missing_passives"
  exit 1
fi

if jq -r '.exact[], .characters[], .passives[]' "$catalog" | grep -nP '[А-Яа-яЁё]' > "$tmp/cyrillic"; then
  echo "Cyrillic leaked into English catalog values:"
  head -40 "$tmp/cyrillic"
  exit 1
fi

grep -q 'DataBase\\localization.en.json' King-of-the-Garbage-Hill/King-of-the-Garbage-Hill.csproj
grep -q 'localization.en.json' Web/VueClient/src/i18n.ts

echo "Localization audit passed: $(wc -l < "$tmp/characters-source") characters, $(wc -l < "$tmp/passives-source") passives."

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
jq -r '(.exact | keys[]), (.terms | keys[])' "$catalog" | sort -u > "$tmp/display-keys"
rg -o 'new PhraseClass\("[^"]*"' King-of-the-Garbage-Hill/Game/MemoryStorage/CharactersPhrases.cs \
  | sed 's/.*new PhraseClass("//; s/"$//' | sort -u > "$tmp/phrases-source"
jq -r '.phraseFallbacks | keys[]' "$catalog" | sort -u > "$tmp/phrase-fallbacks"

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

grep -P '[А-Яа-яЁё]' "$tmp/characters-source" > "$tmp/cyrillic-characters" || true
grep -P '[А-Яа-яЁё]' "$tmp/passives-source" > "$tmp/cyrillic-passives" || true
missing_character_names=$(comm -23 "$tmp/cyrillic-characters" "$tmp/display-keys")
missing_passive_names=$(comm -23 "$tmp/cyrillic-passives" "$tmp/display-keys")
grep -P '[А-Яа-яЁё]' "$tmp/phrases-source" > "$tmp/cyrillic-phrases" || true
missing_phrase_names=$(comm -23 "$tmp/cyrillic-phrases" "$tmp/display-keys")
missing_phrase_fallbacks=$(comm -23 "$tmp/cyrillic-phrases" "$tmp/phrase-fallbacks")

if [[ -n "$missing_character_names" ]]; then
  echo "Missing English character-name display translations:"
  echo "$missing_character_names"
  exit 1
fi
if [[ -n "$missing_passive_names" ]]; then
  echo "Missing English passive-name display translations:"
  echo "$missing_passive_names"
  exit 1
fi
if [[ -n "$missing_phrase_names" ]]; then
  echo "Missing English PhraseClass display translations:"
  echo "$missing_phrase_names"
  exit 1
fi
if [[ -n "$missing_phrase_fallbacks" ]]; then
  echo "Missing English PhraseClass fallbacks:"
  echo "$missing_phrase_fallbacks"
  exit 1
fi

if jq -r '.exact[], .terms[], .phraseFallbacks[], .characters[], .passives[]' "$catalog" | grep -nP '[А-Яа-яЁё]' > "$tmp/cyrillic"; then
  echo "Cyrillic leaked into English catalog values:"
  head -40 "$tmp/cyrillic"
  exit 1
fi

grep -q 'DataBase\\localization.en.json' King-of-the-Garbage-Hill/King-of-the-Garbage-Hill.csproj
grep -q 'localization.en.json' Web/VueClient/src/i18n.ts
grep -q 'SortedExact' King-of-the-Garbage-Hill/Helpers/GameLocalization.cs
grep -q 'russianExactEntries' Web/VueClient/src/i18n.ts
grep -q 'PhraseFallbacks' King-of-the-Garbage-Hill/Helpers/GameLocalization.cs
grep -q 'phraseFallbacks' Web/VueClient/src/i18n.ts

echo "Localization audit passed: $(wc -l < "$tmp/characters-source") characters, $(wc -l < "$tmp/passives-source") passives, $(wc -l < "$tmp/phrases-source") phrase classes."

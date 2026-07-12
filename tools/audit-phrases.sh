#!/usr/bin/env bash
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
SOURCE="$ROOT/King-of-the-Garbage-Hill/Game/MemoryStorage/CharactersPhrases.cs"
CATALOG="$ROOT/King-of-the-Garbage-Hill/DataBase/phrases.en.json"

python3 - "$SOURCE" "$CATALOG" <<'PY'
import collections
import json
import re
import sys

source_path, catalog_path = sys.argv[1:]
source = open(source_path, encoding="utf-8").read()
catalog = json.load(open(catalog_path, encoding="utf-8"))

counts = collections.Counter()
for line in source.splitlines():
    if line.lstrip().startswith("//"):
        continue
    match = re.search(r"([\w\u0400-\u04ff]+)\.PassiveLogRus\.Add\(", line)
    if match:
        counts[match.group(1)] += 1

fields = set(re.findall(r"public PhraseClass\s+([\w\u0400-\u04ff]+)\s*;", source))
errors = []
if set(catalog) != fields:
    errors.extend(f"missing English group: {name}" for name in sorted(fields - set(catalog)))
    errors.extend(f"unknown English group: {name}" for name in sorted(set(catalog) - fields))

for name in sorted(fields & set(catalog)):
    group = catalog[name]
    if not isinstance(group, dict):
        errors.append(f"{name} is not a paired phrase group")
        continue
    passive_ru = group.get("passiveNameRussian")
    passive_en = group.get("passiveNameEnglish")
    pairs = group.get("phrases")
    if not isinstance(passive_ru, str) or not passive_ru.strip():
        errors.append(f"{name}.passiveNameRussian is empty")
    if not isinstance(passive_en, str) or not passive_en.strip() or re.search(r"[А-Яа-яЁё]", passive_en):
        errors.append(f"{name}.passiveNameEnglish is empty or contains Cyrillic")
    if not isinstance(pairs, list):
        errors.append(f"{name}.phrases is not an array")
        continue
    if len(pairs) != counts[name]:
        errors.append(f"{name}: {counts[name]} Russian source variants / {len(pairs)} catalog variants")
    for index, pair in enumerate(pairs):
        if not isinstance(pair, dict):
            errors.append(f"{name}[{index}] is not a Russian/English pair")
            continue
        russian = pair.get("russian")
        english = pair.get("english")
        if not isinstance(russian, str) or not russian.strip():
            errors.append(f"{name}[{index}].russian is empty")
        if not isinstance(english, str) or not english.strip():
            errors.append(f"{name}[{index}].english is empty")
        elif re.search(r"[А-Яа-яЁё]", english):
            errors.append(f"{name}[{index}].english contains Cyrillic")

if errors:
    print("Phrase localization audit FAILED:")
    for error in errors:
        print(f"  - {error}")
    raise SystemExit(1)

print(f"Phrase localization audit OK: {len(fields)} groups, {sum(counts.values())} paired variants.")
PY

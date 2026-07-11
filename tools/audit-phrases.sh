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
    english = catalog[name]
    if len(english) != counts[name]:
        errors.append(f"{name}: {counts[name]} Russian variants / {len(english)} English variants")
    for index, text in enumerate(english):
        if not isinstance(text, str) or not text.strip():
            errors.append(f"{name}[{index}] is empty")
        elif re.search(r"[А-Яа-яЁё]", text):
            errors.append(f"{name}[{index}] contains Cyrillic")

if errors:
    print("Phrase localization audit FAILED:")
    for error in errors:
        print(f"  - {error}")
    raise SystemExit(1)

print(f"Phrase localization audit OK: {len(fields)} groups, {sum(counts.values())} paired variants.")
PY

#!/usr/bin/env bash
# Static integrity check for the Daily Quest V2 catalog.

set -euo pipefail
cd "$(dirname "$0")/.."

catalog="King-of-the-Garbage-Hill/Game/Classes/QuestClass.cs"
characters="King-of-the-Garbage-Hill/DataBase/characters.json"

for file in "$catalog" "$characters"; do
  if [ ! -f "$file" ]; then
    echo "audit-quests: missing input: $file" >&2
    exit 1
  fi
done
if ! command -v python3 >/dev/null 2>&1; then
  echo "audit-quests: python3 is required" >&2
  exit 1
fi

python3 - "$catalog" "$characters" <<'PY'
import json
import re
import sys
from collections import Counter
from pathlib import Path

catalog_path, characters_path = map(Path, sys.argv[1:])
source = catalog_path.read_text(encoding="utf-8-sig")
issues = []

# ID -> lane / aggregation / metric. This catches semantic swaps that retain valid C# types.
expected = {
    "dq_clock_in": ("Anchor", "DailySum", "EligibleMatches"),
    "dq_thick_of_it": ("Skirmish", "DailySum", "ResolvedFights"),
    "dq_throw_hands": ("Skirmish", "DailySum", "FightWins"),
    "dq_rival_tour": ("Skirmish", "BestMatch", "UniqueOpponentsBest"),
    "dq_hot_streak": ("Skirmish", "BestMatch", "ConsecutiveWinsBest"),
    "dq_counterplay": ("Skirmish", "DailySum", "NemesisWins"),
    "dq_still_standing": ("Ambition", "BestMatch", "FinishedAliveBest"),
    "dq_podium": ("Ambition", "BestMatch", "PodiumAliveBest"),
    "dq_claw_back": ("Ambition", "BestMatch", "PlacesClimbedBest"),
    "dq_top_seat": ("Ambition", "BestMatch", "RoundsAtFirstBest"),
    "dq_balanced_scales": ("Ambition", "BestMatch", "JusticeReachedBest"),
    "dq_take_hill": ("Ambition", "BestMatch", "MatchWins"),
}
expected_lanes = {"Anchor": 1, "Skirmish": 5, "Ambition": 6}
expected_metrics = {wiring[2] for wiring in expected.values()}


def fail(message):
    issues.append(message)


def region(start_pattern, end_pattern, label):
    start = re.search(start_pattern, source, re.MULTILINE)
    if not start:
        fail(f"missing {label}")
        return ""
    end = re.search(end_pattern, source[start.end():], re.MULTILINE)
    if not end:
        fail(f"missing end marker for {label}")
        return ""
    return source[start.end():start.end() + end.start()]


catalog_block = region(
    r"\bDailyQuestCatalog\b\s*=\s*new\s+List\s*<\s*QuestDefinition\s*>\s*\{",
    r"\bprivate\s+static\s+readonly\s+IReadOnlyDictionary\s*<\s*string\s*,\s*QuestDefinition\s*>\s+QuestById\b",
    "DailyQuestCatalog",
)

# The V2 catalog deliberately uses literal positional metadata and one direct metric selector.
csharp_string = r'"(?:\\.|[^"\\])*"'
entry_pattern = re.compile(
    rf"""
    \bnew\s*\(\s*
    (?P<id>{csharp_string})\s*,\s*
    (?P<name>{csharp_string})\s*,\s*
    (?P<name_ru>{csharp_string})\s*,\s*
    (?P<description>{csharp_string})\s*,\s*
    (?P<description_ru>{csharp_string})\s*,\s*
    QuestLane\.(?P<lane>[A-Za-z_][A-Za-z0-9_]*)\s*,\s*
    (?P<icon>{csharp_string})\s*,\s*
    QuestAggregation\.(?P<aggregation>[A-Za-z_][A-Za-z0-9_]*)\s*,\s*
    (?P<target>[0-9]+)\s*,\s*
    (?P<zbs>[0-9]+)\s*,\s*
    metrics\s*=>\s*metrics\.(?P<metric>[A-Za-z_][A-Za-z0-9_]*)
    (?:\s*,\s*(?P<loot>[0-9]+))?\s*\)
    """,
    re.DOTALL | re.VERBOSE,
)
entries = [match.groupdict() for match in entry_pattern.finditer(catalog_block)]
raw_definition_count = len(re.findall(r"\bnew\s*\(", catalog_block))
raw_ids = re.findall(rf"\bnew\s*\(\s*({csharp_string})", catalog_block)


def literal(token):
    try:
        return json.loads(token)
    except json.JSONDecodeError:
        return None


ids = [literal(token) for token in raw_ids]
ids = [quest_id for quest_id in ids if quest_id is not None]
duplicates = sorted(quest_id for quest_id, count in Counter(ids).items() if count > 1)
if duplicates:
    fail("duplicate quest IDs: " + ", ".join(duplicates))
missing = sorted(set(expected) - set(ids))
extra = sorted(set(ids) - set(expected))
if missing:
    fail("missing expected quest IDs: " + ", ".join(missing))
if extra:
    fail("unexpected quest IDs: " + ", ".join(extra))
if raw_definition_count != 12:
    fail(f"wrong catalog total: expected 12 definitions, found {raw_definition_count}")
if len(entries) != raw_definition_count:
    fail(f"{raw_definition_count - len(entries)} definition(s) do not match the required V2 metadata/wiring shape")

parsed_by_id = {}
for entry in entries:
    decoded = {field: literal(entry[field]) for field in
               ("id", "name", "name_ru", "description", "description_ru", "icon")}
    quest_id = decoded["id"] or "<malformed-id>"
    parsed_by_id[quest_id] = entry
    for field, value in decoded.items():
        if value is None:
            fail(f"{quest_id}: {field} is not a supported string literal")
        elif not value.strip():
            fail(f"{quest_id}: {field} is empty")
    if decoded["name"] == decoded["name_ru"]:
        fail(f"{quest_id}: English and Russian names are identical")
    if decoded["description"] == decoded["description_ru"]:
        fail(f"{quest_id}: English and Russian descriptions are identical")
    if decoded["name_ru"] and not re.search(r"[А-Яа-яЁё]", decoded["name_ru"]):
        fail(f"{quest_id}: NameRu contains no Cyrillic text")
    if decoded["description_ru"] and not re.search(r"[А-Яа-яЁё]", decoded["description_ru"]):
        fail(f"{quest_id}: DescriptionRu contains no Cyrillic text")
    if int(entry["target"]) <= 0 or int(entry["zbs"]) <= 0:
        fail(f"{quest_id}: target and ZBS reward must be positive")

lane_counts = Counter(entry["lane"] for entry in entries)
for lane, count in expected_lanes.items():
    if lane_counts.get(lane, 0) != count:
        fail(f"wrong {lane} count: expected {count}, found {lane_counts.get(lane, 0)}")
unknown_lanes = sorted(set(lane_counts) - set(expected_lanes))
if unknown_lanes:
    fail("unknown catalog lanes: " + ", ".join(unknown_lanes))

for quest_id, expected_wiring in expected.items():
    entry = parsed_by_id.get(quest_id)
    if not entry:
        continue
    actual_wiring = (entry["lane"], entry["aggregation"], entry["metric"])
    if actual_wiring != expected_wiring:
        fail(
            f"{quest_id}: expected {'/'.join(expected_wiring)}, "
            f"found {'/'.join(actual_wiring)}"
        )

lane_enum = region(r"\bpublic\s+enum\s+QuestLane\s*\{", r"\}", "QuestLane enum")
declared_lanes = set(re.findall(r"\b[A-Za-z_][A-Za-z0-9_]*\b", lane_enum))
if declared_lanes != set(expected_lanes):
    fail("QuestLane enum must contain exactly Anchor, Skirmish and Ambition")

aggregation_enum = region(
    r"\bpublic\s+enum\s+QuestAggregation\s*\{", r"\}", "QuestAggregation enum"
)
declared_aggregations = set(re.findall(r"\b[A-Za-z_][A-Za-z0-9_]*\b", aggregation_enum))
if declared_aggregations != {"DailySum", "BestMatch"}:
    fail("QuestAggregation enum must contain exactly DailySum and BestMatch")

metrics_class = region(
    r"\bpublic\s+class\s+DailyQuestMetrics\s*\{",
    r"\bpublic\s+class\s+DailyQuestState\b",
    "DailyQuestMetrics class",
)
declared_metrics = set(re.findall(
    r"\bpublic\s+int\s+([A-Za-z_][A-Za-z0-9_]*)\s*\{\s*get\s*;\s*set\s*;\s*\}",
    metrics_class,
))
unused_metrics = sorted(declared_metrics - expected_metrics)
undeclared_metrics = sorted(expected_metrics - declared_metrics)
if unused_metrics:
    fail("declared metrics unused by the catalog: " + ", ".join(unused_metrics))
if undeclared_metrics:
    fail("catalog metrics missing from DailyQuestMetrics: " + ", ".join(undeclared_metrics))

track_region = region(
    r"\bpublic\s+static\s+void\s+TrackGameEnd\b",
    r"\bpublic\s+static\s+bool\s+TryRerollDailyQuest\b",
    "TrackGameEnd metric wiring",
)
clone_region = region(
    r"\bprivate\s+static\s+DailyQuestState\s+CloneDailyState\b",
    r"\bpublic\s+static\s+int\s+GetGuaranteedRareIn\b",
    "CloneDailyState metric wiring",
)
for metric in sorted(expected_metrics):
    if not re.search(rf"\bmetrics\.{re.escape(metric)}\b", track_region):
        fail(f"metric `{metric}` is not populated by TrackGameEnd")
    if not re.search(
        rf"\b{re.escape(metric)}\s*=\s*source\.Metrics\.{re.escape(metric)}\b", clone_region
    ):
        fail(f"metric `{metric}` is not preserved by CloneDailyState")

required_wiring = {
    "Anchor selection": r"DailyQuestCatalog\.Single\s*\([^;]*QuestLane\.Anchor",
    "Skirmish selection": r"SelectLaneQuest\s*\(\s*QuestLane\.Skirmish",
    "Ambition selection": r"SelectLaneQuest\s*\(\s*QuestLane\.Ambition",
    "metric evaluation": r"definition\.MetricValue\s*\(\s*metrics\s*\)",
}
for label, pattern in required_wiring.items():
    if not re.search(pattern, source, re.DOTALL):
        fail(f"missing {label} wiring")

try:
    character_data = json.loads(characters_path.read_text(encoding="utf-8-sig"))
except (OSError, json.JSONDecodeError) as error:
    fail(f"could not parse characters.json: {error}")
    character_data = []
character_names = sorted({
    entry.get("Name", "").strip()
    for entry in character_data
    if isinstance(entry, dict) and isinstance(entry.get("Name"), str) and entry["Name"].strip()
})
catalog_without_comments = re.sub(r"//[^\n]*|/\*.*?\*/", "", catalog_block, flags=re.DOTALL)
leaked_characters = [name for name in character_names if name in catalog_without_comments]
if leaked_characters:
    fail("canonical character names appear in DailyQuestCatalog: " + ", ".join(leaked_characters))

for field in ("Name", "NameRu", "Description", "DescriptionRu", "Icon", "MetricValue"):
    if not re.search(rf"\bpublic\s+[^;\n]+\s+{field}\s*\{{", source):
        fail(f"QuestDefinition is missing `{field}`")

if issues:
    for issue in issues:
        print(f"FAIL: {issue}")
    print(f"audit-quests: FAILED ({len(issues)} issue{'s' if len(issues) != 1 else ''})")
    sys.exit(1)

print(
    "audit-quests: 12 definitions "
    "(1 Anchor / 5 Skirmish / 6 Ambition), bilingual metadata, metrics and character neutrality verified."
)
PY

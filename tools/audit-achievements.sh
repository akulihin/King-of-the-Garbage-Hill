#!/usr/bin/env bash
# Static integrity check for the Achievement V2 catalog.

set -euo pipefail
cd "$(dirname "$0")/.."

catalog="King-of-the-Garbage-Hill/Game/Classes/AchievementClass.cs"

expected=$(cat <<'EOF'
c_boys_orders
c_darksci_unstable
c_doom_bfg
c_eren_rumbling
c_geralt_contracts
c_goblin_summit
c_itachi_tax
c_kira_perfect_crime
c_kotiki_reunion
c_kratos_olympus
c_madara_tsukuyomi
c_monster_apocalypse
c_rick_portals
c_saitama_one_punch
c_tigr_six_zero
g_bottom_feeder
g_class_advantage
g_clean_sweep
g_maximum_sentence
g_open_book
g_quad_damage
g_round10_comeback
g_target_routine
g_three_drops
g_twenty_moral
g_untouchable
x_boys_madara
x_deeplist_weedwick
x_itachi_madara
x_kira_kratos
x_monster_witness
x_spartan_dragon
x_spartan_mylorik
EOF
)

definitions=$(
  rg -o 'new\("[gcx]_[a-z0-9_]+"' "$catalog" \
    | sed 's/^new("//' \
    | sed 's/"$//' \
    | sort
)

fail=0

duplicates=$(printf '%s\n' "$definitions" | uniq -d)
if [ -n "$duplicates" ]; then
  printf 'DUPLICATE achievement definitions:\n%s\n' "$duplicates"
  fail=1
fi

missing=$(comm -23 <(printf '%s\n' "$expected" | sort) <(printf '%s\n' "$definitions" | sort -u))
extra=$(comm -13 <(printf '%s\n' "$expected" | sort) <(printf '%s\n' "$definitions" | sort -u))
if [ -n "$missing" ]; then
  printf 'MISSING Achievement V2 definitions:\n%s\n' "$missing"
  fail=1
fi
if [ -n "$extra" ]; then
  printf 'UNEXPECTED Achievement V2 definitions:\n%s\n' "$extra"
  fail=1
fi

while IFS= read -r id; do
  [ -z "$id" ] && continue
  references=$(rg -o "\"${id}\"" "$catalog" | wc -l)
  if [ "$references" -lt 2 ]; then
    echo "UNEVALUATED achievement: $id (definition exists, no evaluator reference)"
    fail=1
  fi
done <<< "$expected"

global_count=$(printf '%s\n' "$definitions" | rg -c '^g_' || true)
character_count=$(printf '%s\n' "$definitions" | rg -c '^c_' || true)
interaction_count=$(printf '%s\n' "$definitions" | rg -c '^x_' || true)
if [ "$global_count" -ne 11 ] || [ "$character_count" -ne 15 ] || [ "$interaction_count" -ne 7 ]; then
  echo "BAD category counts: global=$global_count character=$character_count interaction=$interaction_count (expected 11/15/7)"
  fail=1
fi

for field in NameRu DescriptionRu SecretHintRu CharacterNames RewardZbs RewardLootBoxes; do
  if ! rg -q "public .* ${field} " "$catalog"; then
    echo "MISSING achievement metadata field: $field"
    fail=1
  fi
done

if [ "$fail" -ne 0 ]; then
  echo "audit-achievements: FAILED"
  exit 1
fi

echo "audit-achievements: 33 definitions (11 global / 15 character / 7 interaction), all unique and evaluated."

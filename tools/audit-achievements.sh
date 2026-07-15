#!/usr/bin/env bash
# Static integrity check for the Achievement V2 catalog.

set -euo pipefail
cd "$(dirname "$0")/.."

catalog="King-of-the-Garbage-Hill/Game/Classes/AchievementClass.cs"

expected=$(cat <<'EOF'
c_awdka_mastery
c_awdka_trying
c_boys_orders
c_boys_ultimate
c_crab_fortress
c_crab_shell
c_darksci_stable
c_darksci_unstable
c_deeplist_mockery
c_deeplist_roast
c_doom_bfg
c_doom_loadout
c_dopa_big_brain
c_dopa_foresight
c_eren_rumbling
c_eren_tatake
c_geralt_contracts
c_geralt_path
c_gleb_challenger
c_gleb_return
c_goblin_architect
c_goblin_summit
c_gordon_halflife3
c_gordon_rescue
c_hardkitty_letters
c_hardkitty_love
c_itachi_crows
c_itachi_tax
c_kira_first_name
c_kira_perfect_crime
c_kotiki_one_back
c_kotiki_reunion
c_kratos_olympus
c_kratos_rampage
c_lecrisp_impact
c_lecrisp_legend
c_madara_round_eight
c_madara_tsukuyomi
c_mitsuki_garbage
c_mitsuki_loud
c_monster_apocalypse
c_monster_no_escape
c_mylorik_grudges
c_mylorik_revenge
c_napoleon_alliance
c_napoleon_treaties
c_naruto_harem
c_naruto_rasengan
c_octopus_ink
c_octopus_tour
c_rick_beans
c_rick_portals
c_salldorum_double_cola
c_salldorum_cola
c_saitama_one_punch
c_saitama_serious
c_sakura_first
c_sakura_three
c_seller_market
c_seller_marks
c_shark_apex
c_shark_teeth
c_sirinoks_dragon
c_sirinoks_friends
c_spartan_shame
c_spartan_warrior
c_support_buff
c_support_premade
c_tigr_six_zero
c_tigr_three_zero
c_tolya_accounting
c_tolya_rammus
c_toxic_chain
c_toxic_return
c_vampyr_bites
c_vampyr_feast
c_weedwick_harvest
c_weedwick_smoke
c_young_gleb_meta
c_young_gleb_ward
g_auto_pilot
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
x_deeplist_octopus
x_deeplist_weedwick
x_doom_dragon
x_eren_goblins
x_goblin_bad_architecture
x_gordon_theboys
x_itachi_madara
x_kira_kratos
x_monster_witness
x_rick_most_wanted
x_spartan_dragon
x_spartan_kratos
x_spartan_mylorik
EOF
)

# Every public or post-roll character definition has one approachable card and one hard card.
# Difficulty is a rule-design tier, independent of reward rarity, so existing V2 card rewards stay stable.
character_pairs=$(cat <<'EOF'
TheBoys|c_boys_orders|c_boys_ultimate
Стая Гоблинов|c_goblin_architect|c_goblin_summit
Рик Санчез|c_rick_beans|c_rick_portals
Сайтама|c_saitama_serious|c_saitama_one_punch
Мадара|c_madara_round_eight|c_madara_tsukuyomi
Тигр|c_tigr_three_zero|c_tigr_six_zero
Итачи|c_itachi_crows|c_itachi_tax
Кратос|c_kratos_rampage|c_kratos_olympus
Кира|c_kira_first_name|c_kira_perfect_crime
Монстр без имени|c_monster_no_escape|c_monster_apocalypse
Продавец Сомнительных Тактик|c_seller_marks|c_seller_market
Dopa|c_dopa_foresight|c_dopa_big_brain
Salldorum|c_salldorum_cola|c_salldorum_double_cola
Геральт|c_geralt_contracts|c_geralt_path
Котики|c_kotiki_one_back|c_kotiki_reunion
Toxic Mate|c_toxic_chain|c_toxic_return
Napoleon Wonnafcuk|c_napoleon_alliance|c_napoleon_treaties
Таинственный Суппорт|c_support_buff|c_support_premade
Осьминожка|c_octopus_tour|c_octopus_ink
DeepList|c_deeplist_mockery|c_deeplist_roast
mylorik|c_mylorik_revenge|c_mylorik_grudges
Глеб|c_gleb_return|c_gleb_challenger
LeCrisp|c_lecrisp_impact|c_lecrisp_legend
Толя|c_tolya_rammus|c_tolya_accounting
HardKitty|c_hardkitty_letters|c_hardkitty_love
Sirinoks|c_sirinoks_friends|c_sirinoks_dragon
Злой Школьник|c_mitsuki_loud|c_mitsuki_garbage
AWDKA|c_awdka_trying|c_awdka_mastery
Darksci|c_darksci_stable|c_darksci_unstable
Братишка|c_shark_teeth|c_shark_apex
Загадочный Спартанец в маске|c_spartan_shame|c_spartan_warrior
Вампур|c_vampyr_bites|c_vampyr_feast
Краборак|c_crab_shell|c_crab_fortress
Weedwick|c_weedwick_smoke|c_weedwick_harvest
Молодой Глеб|c_young_gleb_meta|c_young_gleb_ward
Sakura|c_sakura_three|c_sakura_first
DooM Guy|c_doom_loadout|c_doom_bfg
Эрен Йегер|c_eren_tatake|c_eren_rumbling
Наруто|c_naruto_harem|c_naruto_rasengan
Гордон Фримен|c_gordon_rescue|c_gordon_halflife3
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
if [ "$global_count" -ne 12 ] || [ "$character_count" -ne 80 ] || [ "$interaction_count" -ne 14 ]; then
  echo "BAD category counts: global=$global_count character=$character_count interaction=$interaction_count (expected 12/80/14)"
  fail=1
fi

pair_count=$(printf '%s\n' "$character_pairs" | sed '/^$/d' | wc -l)
paired_ids=$(printf '%s\n' "$character_pairs" | awk -F'|' '{ print $2; print $3 }' | sort)
defined_character_ids=$(printf '%s\n' "$definitions" | rg '^c_' | sort)
pair_names=$(printf '%s\n' "$character_pairs" | cut -d'|' -f1 | sort)
roster_names=$(jq -r '.[] | select(.Name != "unknown_bug" and .Name != "Баг") | .Name' King-of-the-Garbage-Hill/DataBase/characters.json | sort)
duplicate_pair_ids=$(printf '%s\n' "$paired_ids" | uniq -d)
unpaired_character_ids=$(comm -23 <(printf '%s\n' "$defined_character_ids") <(printf '%s\n' "$paired_ids"))
unknown_pair_ids=$(comm -13 <(printf '%s\n' "$defined_character_ids") <(printf '%s\n' "$paired_ids"))
missing_roster_pairs=$(comm -23 <(printf '%s\n' "$roster_names") <(printf '%s\n' "$pair_names"))
unknown_pair_names=$(comm -13 <(printf '%s\n' "$roster_names") <(printf '%s\n' "$pair_names"))
if [ "$pair_count" -ne 40 ] || [ -n "$duplicate_pair_ids" ] || [ -n "$unpaired_character_ids" ] \
    || [ -n "$unknown_pair_ids" ] || [ -n "$missing_roster_pairs" ] || [ -n "$unknown_pair_names" ]; then
  echo "BAD normal/hard character pairing coverage: pairs=$pair_count (expected 40)"
  [ -n "$duplicate_pair_ids" ] && printf 'DUPLICATE paired IDs:\n%s\n' "$duplicate_pair_ids"
  [ -n "$unpaired_character_ids" ] && printf 'UNPAIRED character IDs:\n%s\n' "$unpaired_character_ids"
  [ -n "$unknown_pair_ids" ] && printf 'UNKNOWN paired IDs:\n%s\n' "$unknown_pair_ids"
  [ -n "$missing_roster_pairs" ] && printf 'ROSTER NAMES WITHOUT PAIRS:\n%s\n' "$missing_roster_pairs"
  [ -n "$unknown_pair_names" ] && printf 'PAIR NAMES OUTSIDE ROSTER:\n%s\n' "$unknown_pair_names"
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

echo "audit-achievements: 106 definitions (12 global / 80 character / 14 interaction), 40 normal/hard character pairs, all unique and evaluated."

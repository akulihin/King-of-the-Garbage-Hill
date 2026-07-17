# Empire's Endgame — Missing/Unfinished Elements: Gap Analysis & Completion Plan

## Context

Empire's Endgame (`/empires-endgame`, `Web/VueClient/src/features/empires-endgame/`) is a self-contained client-side browser game: Durak vs the God of Gambling alternating with an empire-management phase. The designer's full intent lives in the Discord export `DiscordExports/Empires_Endgame/` (+ `DiscordExports/empire_prompt` = the original build prompt; the `ZBS MAKING` file is **outdated** background — on conflict the main export wins). The July 2026 audit wave (M54–M61, M83–M91, all fixed) made the implementation *honest*: everything not implemented is now explicitly marked `deferredReason` (175 markers) and engine+UI refuse to spend on it. This plan enumerates everything still missing or unfinished vs the design and sequences the work to finish it. **Executor note: read section A as the compressed authoritative design spec; consult the raw export channels named in parentheses when implementing a specific system.**

Scope decisions confirmed by the designer (2026-07-16):
1. **Combat = Tower Defense minigame directly** — no interim abstract combat; army content goes live through TD.
2. **All minigames in scope**: TD, Tetris-alchemy, Tavern + mystic cards, Chess, Tetris-inventory (expedition packing).
3. **Missing numbers**: implement mechanics with configurable defaults in `game-config.json` + maintain a designer-review ledger of every invented number (`docs/EMPIRES-ENDGAME-DESIGN-REVIEW.md`, append-only).
4. **Also in scope**: quest/dialogue engine, God presence (deck memory, anti-bito, God lines, Милость confirmations). **Out of scope**: sound, scripted tutorial staging, lore/secret endings.
5. Everything stays client-side (localStorage), consistent with current architecture.

---

## A. Design source model (compressed from the export; channel names in parentheses)

### A1. Core loop (empire_prompt, общее)
1. **Durak vs God** — 53-card deck (2..A ×4 + Joker "Шут"); card = suit, rank, name, time-cost, value, art, passive; every card has an **inverted form** (taken from God's attack → gothic art, negative mirror passive). Kon = several bouts (configurable); configurable scoring → points; points spent on card upgrade or un-inverting. After kon → **divine gift** draft 1-of-3, value scales with performance.
2. **Empire phase**: hand cards' passives apply; budget 59 days minus hand time-costs; actions cost days; random events. Loop until durak ends or empire dies. 1 kon ≈ 2 empire months.
3. Meta: scripted 3-stage tutorial → roguelike (OUT OF SCOPE); meta-ladder region lore → advisor finales → secret ending (OUT OF SCOPE).

### A2. Cards (карты, персонажи, таверна; ZBS for older card drafts)
- Suit themes: ♥ королевская семья/влияние; ♠ прогресс/науч.советник; ♦ экономика+дипломатия/торг.советник; ♣ народ. Trump crits; trump+advisor same suit = min-max. Крести trump only when Grand Advisor opened.
- Documented cards (details in channels): К♥ Легитимность Томаса; В♥ похищение сына; Т♥ Mr.G/дед-квесты; 7♥ Зазывалы (inverted = Поджог: −1 lvl военного здания, лок на ход, юнит-потери) [executor exists, face deferred until army]; Т♦ банкир/валюта; 8♦ подати; 10♦ сателлит; 3♠ карты мира/логистика; 5♠ Образование; 8♠ 200% ферм; К♠ Конрад (очки улучшений); Д♠ Мария Брауз (порох); В♠ Антон де Лорян (интел; необнаружимый переворот; работает в любой руке); Т♣ Дженна (рождаемость/болезни); 8♣ Стандарт питания ±50%; категории Экономика/Влияние/Народ/Прогресс с нумерованными парами светлая/тёмная.
- Lifecycle: бито = потеря (персонаж выбыл); отдано богу = неактивен; перевёрнута в руке = вредит. Draw from deck → +1 upgrade. Card upgrade example: effect persists 1 round after loss.
- Mystic cards (таверна): Лист/Лорик/Анатолий — без масти/ранга, возвращаются сами перевёрнутыми; Пиковая Дама (спавн после Марии Брауз + комбо 3-7-Т на столе) периодически переворачивает соседние карты.
- God behaviors: face-up shuffle → Том запоминает порядок колоды (deck-memory feature); anti-bito (возврат части бито в колоду если игра кончается слишком быстро); реплики бога; подтверждение траты Божественной Милости с "не показывать больше".

### A3. Map, regions, cities (застройка, дома, регионы, лор)
- 5 regions (N лёд / W лес / S пустыня / E болото / C Тетракор) + 10 subregions; fixed oblique camera; minimap. Resource asymmetry: W/E мало шахт; N/S нет лесопилок; W лошади; S нет воды (кактусовые фермы).
- 13 cities: 2/region периметр (500k) + 4 Тетракор (3M) + столица (8M); перст-губернатор → доп. точки застройки (2>2>1); морские города → слот Морской порт (max 4); столица: Тетракорархос, Форум, Колизей, Военная академия, шахта белого камня.
- Region great houses ×4 с шаблоном черт + выходка-ивент; уникальные расы; региональная лояльность → восстание; late-game предательства → региональные жертвы-дары.

### A4. City economy (застройка, здания, экономика, общее)
- Slots: ферма, лесопилка, шахта, военная кузница (Оружейник/Бронник), казарма, unique 6th, municipal. Food/pop rules (implemented). Busy-locks (implemented for mine/lumber). Seasons: лето/зима ×2 лимит еды; парники выравнивают. 50% населения не работает; worker shortage shutdown mine→lumber→farm by level (implemented).
- **Loyalty**: −9..+9 city + region modifier; effective workforce divisor −9→/19, 0→/9, +9→/1; отрицательная лояльность выключает здания; классы (крестьяне/мещане/дворяне/духовенство) привязаны к зданиям (кузня требует лояльности мещан). **Reputation**: −9..+9, gates trade/unions.
- **Army** (застройка message-d27f1e9af25bf194.txt): 7 типов (регулярка-подписка, пограничные феодалы, региональные феодалы за ресурсы, наёмники, дружина по типам городов, ополчение, пороховые солдаты); прирост через казарма←кузня←шахта; потери → −призыв/прирост (×множители), 10%+ потерь → лояльность −1; % армии от населения 1→5→+5→20; кузнецы: 10/город, годовые объёмы (5000 стрел…5 великих мечей).
- Buildings catalog (здания): Банк (кредит/гонения), Еврейский банк (страховка 3 хода, окружение → самоликвидация), Амбар 2 ступени, Храм (5 веток + тёмные), Алхимическая лавка, Конюшня, Трактир, Ярмарка (Карнавал→Артисты→Табор→барон), Мастерская, Чёрный рынок, Посольство, Внешний рынок, Военная/Медицинская академии, Таможня, Больница, Книгопечатный пресс, Малый храм (реализован), Литейная, Баллиста, Пристань/Морской порт, Столичный Форум (лояльность ×2 в обе стороны), Двор Гвардейской Дружины, Торговый сбор (реализован).

### A5. Tech/doctrines/reforms/steel (Технологии_*)
- 4 ветки + Культура; советники: 3 в начале, "2 казнить 1 помиловать"; теократия→технократия. Rules: техи ≤1/ход/ветвь (реализовано), реформы ≤1/ход/доктрина (реализовано); реформы = технологические + муниципальные.
- **Каждая технология имеет светлую и тёмную сторону**; раскрытие тёмной → падение рейтинга; культурная пассивка отключает тёмные; скрытые комбо (Амбар+Алхимия=чума; +Дрессировщики=чумные крысы; химера).
- Общая ветка: Образование → (Ремесло, Фермерство, Плотничество, Сталелитейное, Рынок, Церковь, Репутация, Посольство, Корабли, Ментовка, Тюрьмы, Храм); разовые: Амбар, Госпиталь, Карантин, Книгопечатанье, Дрессировщики. Логистика вкладка (частично реализована). Кав. таран chain. Гвардейская/стенная ветвь. Мельницы: ветряная vs водяная (реализованы базово; водяная gates кузню 4+; тёмная сторона воробьёв). Именованные реформы: Казначейство (реализована), Принуждение, Геройские похороны, Городские врата, Контроль кузнецов, Фармацевтика.
- **Сталь** (Steel-c748ae22139d6401.txt = latest v; полное дерево): 6 веток оружия (Ударные — закрыта, крадётся; Древковые; Рубящие; Клинковые; Стрелковые; Пудра) + Особые изобретения + 3 ветки брони (Кольчуга/Доспехи/Поддоспешник); развилка = вход в соседнюю ветку, исходная ×2 цены; поколения (−/+ полушаги, + бесплатно через пару ходов); |Элитное| gated военной элитой; gear/method prerequisites (водяной молот и т.д.).
- **Damage-type system** (сталь, тд): ударное/дробящее/рубящее/режущее/колющее с уровнями per weapon; авто-приоритет (режущее по голым; колющее если уровень > общего уровня брони); контр-матрица: Ударные>Кольчуга>Режущие; Рубящее>Бригантина>Ударное; Тканевые>Ударные+Режущие; Эсток/Ледоруб контрят всё; щиты выключают стрелы, топоры опускают щиты; контра выключает пассивки; смешанные не контрятся; двухтиповые (Лютеранский молот) контрятся обеими.

### A6. Minigames (тд, тетрис-алхимия, чо-добавить, шахматы, таверна)
- **ТД**: attack/defense на границах регионов vs Альянс; волны каждые 4 месяца (≈2 кона); башни 4 последовательных грейда (региональный→общий→общий→региональный ультра) × 4 варианта, стакаются; тир-схема (башня/стрелковый тип/снаряды/региональное); регион-правила: болото — недосягаемые вышки, лес — лучники на деревьях, север — только катапульты/требушеты + ТД против кораблей, пустыня — иссушение при дэфе; замковый дэф; юниты на дэф, заслоны, крепость-пост, партизаны/наёмники-кемпы (EE_TD sketch); Эдемская катапульта.
- **Тетрис-алхимия**: Сбор (фигуры с 4 сторон к центральной конструкции; управление ближайшей; нельзя двигать назад; ускорение ×3 к центру) и Разбор; реагенты (убрать цвет, добавить серые, сброс ускорения); ускорение арифм. прогрессией, cap 400% → взрыв → эпидемия/мутанты у лаборатории; poison-craft путь со стенками.
- **Тетрис-инвентарь**: паковка экспедиции, тележка, вещи падают в реальном времени.
- **Шахматы**: важные карты = фигуры (казна/чистые улицы = ладьи, семья = фигуры, Антон = конь под управлением обоих раз в 2 хода); у врага нет короля. Sketch-level.
- **Таверна**: спавн ближе к лейту (не в 1-м прохождении; 100% на 2-м, потом 33%); две секции; найм наёмников, спиртное, слухи; Мария Брауз 33% (карты 2×2 → комбо 3-7-Т → Пиковая Дама).

### A7. Other systems (экспедиции, события, реликвии, божественные-награды, квесты)
- **Экспедиции**: убийство пограничной крепости открывает зону; провизия-экипировка (разовое снижение); жалобы регионов; Ветеран (>50% hp → ветеран; второе ранение → выбывает); враги по типам урона (юг голые, запад кость/кожа, болото твари).
- **События**: reigns-подобные с последствиями; известные: голод (реализовано), северные набеги, концессия, контрабанда, золотой идол, эпидемия у врат, кража коней, страховка, белый камень, ведьмы.
- **Реликвии**: слоты Храма; 25% от эпидемий, боевой дух min 2, десятина +50%, кузня/конюшня без ресурсов, +1 lvl ферм/лесопилок (последняя реализована).
- **Божественные награды**: землетрясение, муссоны/ветер, +макс очко боевого духа, рыбные течения, метеор (реализован) + метеоритное железо, региональные жертвы (реализованы), цунами в пустыне.
- **Боевой дух**: очки на юнитах → повтор активки; источники трактир/вино/реликвии; шкала дезертирства (ZBS, minimal).
- **Квесты**: Палач (готовые интерактивные HTML-демо в экспорте: `Palach*.html`), золотой идол → Маг, крестоносцы-вёдра (6 фаз), воробьи/саранча/Людовик, север↔лес, упыри Аматы, титановая рыба, руда в лесу; лвл-апы регионов.

---

## B. Implementation state (verified 2026-07-16)

Fully client-side Vue 3 + TS; no backend; localStorage saves; serializable seeded RNG; QA autoplay harness (`qa.ts`, `?qa=1&scenario=…`), Vitest suites (engine.spec 2772 lines), Cypress E2E, `tools/test-empires-endgame.sh`. In-app constructor (BuilderDrawer: cards/buildings/tech-node-editor/content/rules/JSON; map object editor; dependency editor). Docs: `docs/WEB-CLIENT.md` §12B; findings M54–M61, M83–M91 all fixed.

**LIVE**: full Durak vs scripted God + inverted cards + points/upgrades + performance-weighted gift draft (incl. meteor, region sacrifices, city resources, farm/lumber relic); 5 regions/13 cities; per-city ledgers, food settlement, starvation, worker shutdown, facility locks; granary/trade-levy/small-temple/treasury; research with per-branch limits; famine event; production boost; deferral contract (`validateDeferredReasons`/`validateLiveEffects`).

**DEFERRED** (explicit, 175 markers): ~90 card faces (live: ♣2N, ♣8 both, ♠5 both, ♠8 both, ♦6 inv, ♦A N, Joker N); gifts 10/17; buildings 16/22 (smithy, barracks, temple, bank, fair, tavern, stable, alchemy, hospital, customs, medical/military academies, foundry, sea-port, jewish-bank, capital-forum); units 4/4; techs 38/62 (ALL 22 steel + war doctrine + 15); events 9/10.

**ABSENT** (no code): combat of any kind, TD, expeditions, epidemics, diplomacy/external world, loyalty/reputation, seasons, advisors/persts, quests/dialogue, tavern/chess/tetris minigames, mystic cards, deck-memory, anti-bito, God dialogue/confirmations, morale, damage-type system, sound (out of scope), tutorial staging (out of scope).

---

## C. Gap analysis → what "fixing" means

Two failure classes:
1. **Deferred content** — schema exists, engine refuses it; fix = build its substrate system, then un-defer (delete `deferredReason`, add executable effects, extend `EMPIRES_LIVE_FLAG_ALLOWLIST`/typed payloads, tests) in the same change-set.
2. **Absent systems** — need new modules + state + config sections: TD+combat, loyalty/reputation, seasons, epidemics, morale/army, quests/dialogue, God presence, mystic cards, expeditions, external world (Альянс/Людовик lite), other minigames.

Design-vs-implementation mismatches found (small but real): deck-memory feature absent (deck data already serialized — UI+gate only); anti-bito absent; Милость confirmation dialog absent; trump/advisor coupling absent (currently `fixedTrumpSuit: "clubs"` — matches tutorial rule only); `boutsPerCon` config (3) vs doc text ("ten bouts") — reconcile.

---

## D. Execution plan (phases = independent change-sets, in dependency order)

Architecture cornerstones (details per phase):
- **One minigame envelope for all five minigames**: `EmpiresMinigameSession {kind, plan, seed, attempt, origin}` / `EmpiresMinigameResult`; new campaign phase `'minigame'`; engine methods `beginMinigame`/`resolveMinigame`/`abortMinigame` (abort = authored penalty, no save-scumming); mid-minigame real-time state is NEVER serialized — a reload restarts from plan+seed with `attempt+1`.
- **Fixed-timestep TD** (deliberate divergence from last-chances' rAF-delta sim): `step()` advances exactly `tickMs`; sim uses only the serialized RNG stream; battle = pure `f(plan, seed, commandLog) → result`; headless QA runs the *same* sim (single resolution path). rAF loop accumulates time; render interpolates.
- **Shared combat module** `features/empires-endgame/combat/` (damage types, armor classes, counter matrix, equipment catalog) consumed by TD, expeditions, events; steel techs pay off as `equipment` entries with tech prerequisites.
- **State discipline**: flags for empire-wide scalars consumed by existing formulas (reputation, seasons via pure `currentSeason()`); first-class typed state for per-entity/lifecycle data (city loyalty, epidemics, army/morale/veterans, quests, minigame session).
- Config `schemaVersion` bumps with a `migrateEmpiresConfig` chain (mirror `migrateLastChancesConfig`); save migrations continue the `validateAndCloneSnapshot` field-normalization style, envelope bump only for semantic moves.

### Phase 0 — Scaffolding + review ledger (no behavior change)
Config schemaVersion 2 + migration chain injecting empty `combat`/`td`/`god`/`quests`/`empire.seasons`/`empire.loyalty` sections; snapshot normalization defaults (`minigame:null`, `minigameResultLog`, `army`, `external`, `epidemics`, `quests`, city `loyalty:0`, `durak.godInterventions:0`). Create `docs/EMPIRES-ENDGAME-DESIGN-REVIEW.md` (invented-numbers ledger, keyed by JSON pointer). Verify: suites green; v1 save import round-trips.

### Phase 1 — Combat core (damage/armor)
`combat/types.ts` + `combat/damage.ts` (pure `resolveDamage`, `autoSelectDamageType`, `isCountered` per A5 matrix) + config `combat` section (damageTypes, armorClasses, counters, equipment[]). Table-driven `combat/damage.spec.ts` from the design counter list. Nothing player-visible yet.

### Phase 2 — TD vertical slice → army goes live
`td/` module (`types.ts`, `engine.ts` fixed-timestep, `qa.ts` headless autoplay with scripted policies, specs), minigame envelope in campaign engine, wave scheduler (`external.allianceThreat`, `waveEveryCons:2` ≈ 4 months), `settleBattleOutcome` (unit losses → recruitment penalties; ≥10% loss → loyalty hook stub; veterans), `TdBattle.vue` (canvas + HUD + grade drawer + QA fast-resolve button). One central battlefield, generic tower 4 grades × 4 choices, castle, Alliance wave table.
**Un-defer**: units ×4, building-barracks, building-smithy (кузнецы → `army.equipmentStock`), doctrine-war, card-hearts-7 both faces (executor already built per M87), gift-combat-spirit (minimal morale scalar).

### Phase 3 — TD regional depth + steel tree
5 battlefields with region rules (болото/лес/север/пустыня per A6), full regional→common→common→ultra grade matrix, assault mode (для экспедиций позже).
**Un-defer**: all 22 steel techs (equipment payoffs, развилка ×2 pricing, −/+ полушаги, |Элитное| gating), building-foundry, building-military-academy, relic-spirit-floor, event-northern-raids.

### Phase 4 — Loyalty, reputation, seasons, tech dark sides
First-class city loyalty + `regionLoyalty` + `applyLoyaltyDelta` funnel + workforce divisor config table + betrayal → `rebelliousRegionIds` (reversible; TD/quest hook); TD-loss→loyalty wiring; seasons (derived from con, `foodMultiplier`) + greenhouse tech; `darkSide` framework on techs + `chronicle` log; new `loyalty` effect kind. Save envelope v3 (flags.loyalty → structured).
**Un-defer**: municipal-capital-forum, ♣2 inverted, Народ-suit loyalty faces defined in the export.

### Phase 5 — Epidemics + medical chain
`epidemics: EmpiresEpidemicState[]` + `settleEpidemics()` ordered before famine roll; protection stack (hospital/quarantine/Фармацевтика/relic, multiplicative); typed `startEpidemic` resolution payload (mirror of `EmpiresGiftResolution`).
**Un-defer**: building-hospital, building-medical-academy, building-alchemy (passive form), relic-epidemic-ward, event-city-gates-epidemic, Дженна (♣A) inverted, hidden combo Амбар+Алхимия→чума.

### Phase 6 — Economy & external-market buildings
External-world lite: Людовик authored recurring trade offers; Alliance threat interplay.
**Un-defer**: bank (credit + гонения via loyalty), jewish-bank (страховка; «окружение» ≈ lost home-region battle — review item), fair (chain Карнавал→Артисты→Табор), tavern (morale source; minigame hook), stable, customs, sea-port, remaining trade techs, gifts tailwind/fish-currents/meteor-iron/desert-tsunami/earthquake, relics tithe/resource-exemption, events lumber-concession/customs-smuggling/horse-theft/bank-insurance/white-stone.

### Phase 7 — Quest/dialogue engine
Config `quests[]` (trigger / stages / dialogue node graph / choices with `requires`/`costs`/`effects`/`goto`; reuse `firstMissingDependency`); state `quests: Record<id,{stageId,nodeId,status,memory}>`; `quests.ts` (`evaluateQuestTriggers` from `startEmpirePhase` + after `resolveMinigame`; `advanceDialogue`); `DialogueOverlay.vue` + `QuestJournal.vue`; порт квеста Палач из HTML-демо; config schemaVersion 3.
**Un-defer**: event-golden-idol, event-witch-apprenticeship.

### Phase 8 — God presence
Deck-memory (`DeckMemoryPanel.vue` + `canInspectDeck()` gate; data already in `state.durak.deck`); anti-bito in `resolveBout` (`applyAntiBito`: return N random discards to deck below `minimumCons`, intervention cap → guaranteed termination); God dialogue lines config (`god.lines` per trigger); Милость confirmation dialog with persisted "don't show again" (UI prefs, not campaign state).

### Phase 9 — Tavern minigame + mystic cards
`'mystic'` suit + rank `'none'` (excluded from legality/trump/win checks); `mysticCardTick()` in `startNextCon`; explicit `handOrder` (Пиковая Дама inverts *neighbors*); tavern spawn rules (late-game, 33%); `tavern/` minigame via envelope (trio + Мария Брауз encounter). Depends on 6+7+8.

### Phase 10 — Tetris-alchemy
`alchemy/` module (fixed-timestep, 4-directional assembly/disassembly, reagents, 400% cap → `startEpidemic` near lab via envelope result); science-branch payoffs; poison-craft path.

### Phase 11 — Expeditions + tetris-inventory
`expeditions` campaign state; TD assault-mode reuse (border forts as map objects); veterans/provision rules; `inventory/` packing minigame → provision efficiency.

### Phase 12 — Chess (sketch-level)
Cards-as-pieces board behind config toggle `chess.enabled`; expect designer redesign; heaviest review-ledger phase.

**Every phase ends with**: `docs/WEB-CLIENT.md` §12B update (+ new findings to `docs/AUDIT-FINDINGS.md` if bugs found), review-ledger append, `bash tools/test-empires-endgame.sh`, `pnpm --dir Web/VueClient build` (NOT type-check — broken env-wide), `bash tools/verify-docs.sh --changed`.

---

## E. Rules for the executor

1. **Never fabricate silently**: mechanic w/o export numbers → implement with configurable default + ledger entry. Mechanic whose *semantics* are undefined → keep/add `deferredReason` and add a designer question to the ledger.
2. **Un-deferral discipline**: substrate + un-deferral + allowlist/typed-payload + tests in one change-set; `validateLiveEffects` must keep rejecting unread flags.
3. **Determinism**: no `Date.now`/`Math.random` in any sim; only serialized RNG streams; minigames replay from `(plan, seed, commandLog)`.
4. **Main export > ZBS** on any conflict; `empire_prompt` defines the core loop.
5. Card passive *texts* for players may stay vague (repo philosophy); exact mechanics documented in WEB-CLIENT §12B.
6. `engine.ts` (2509 lines) — extract internal modules (`engine/` dir) only when a phase already touches that cluster; no big-bang refactor.
7. Git: no commit/push; write commit message per change-set to `docs/commit-messages/<date>.md`.

## F. Critical files

- `Web/VueClient/src/features/empires-endgame/engine.ts` — phase machine, minigame envelope, settlement ordering, migrations (every phase).
- `…/types.ts` — `EMPIRES_PHASES`, new state (`minigame`, `army`, `external`, `epidemics`, `quests`, loyalty), new effect kinds.
- `…/config.ts` — schemaVersion migration chain, `validateLiveEffects` allowlist, section validators.
- `Web/VueClient/public/empires-endgame/game-config.json` — all new sections + every un-deferral.
- `…/qa.ts` + `…/engine.spec.ts` + `cypress/e2e/empires-endgame*.cy.ts` + `tools/test-empires-endgame.sh` — verification per phase.
- New modules: `…/combat/`, `…/td/`, `…/quests.ts`, `…/alchemy/`, `…/tavern/`, `…/inventory/`, `…/chess/`; components `TdBattle.vue`, `DialogueOverlay.vue`, `QuestJournal.vue`, `DeckMemoryPanel.vue`.
- Reference (patterns to reuse, not modify): `features/last-chances/` (real-time canvas game, config migration chain, builder), `EmpireMap.vue` object editor, `TechTree.vue` node editor (reuse for dialogue graphs).

## G. Verification

- Per phase: focused Vitest (`pnpm --dir Web/VueClient run test:empires`), new QA scenarios (`battle-defense`, `battle-assault`, `epidemic-outbreak`, `quest-dialogue`, `mystic-tavern`, `anti-bito`) + QA actions (`resolve-minigame` w/ policy, `advance-dialogue`), Cypress specs per surface via `?qa=1&scenario=…&seed=…` (never plays real-time; asserts HUD + uses QA fast-resolve), `bash tools/test-empires-endgame.sh`, `pnpm build`.
- TD determinism gate: same `(plan, seed, commandLog)` twice → identical result digest (hash), asserted in `td/engine.spec.ts`; headless autoplay terminates under tick cap for 3 seeds × 3 policies.
- Full-campaign autoplay across battles/quests without stalls = standing integration test (digest/trace stall detector already in qa.ts).
- Save-compat: every envelope bump ships a spec restoring a previous-version fixture.

## H. Designer-review ledger seed (known unknowns)

1. Loyalty→workforce divisor curve (/19, /9, /1) — raw note, needs tuning.
2. Wave cadence: "каждые 4 месяца" ⇒ `waveEveryCons:2` — confirm clock.
3. All tower stats (4×4 grades; "208 билдов" combinatorics) — invented defaults.
4. Anti-bito thresholds; deck-memory availability (always vs N/con).
5. Пиковая Дама spawn combo + neighbor-inversion period; hand order becoming gameplay-relevant.
6. Per-weapon damage-type levels — partial in export (карты/сталь channels have some: Клевец 6/4/3 etc.), rest invented.
7. Steel pricing (развилка ×2, элитное gating) — notation complete, numbers absent.
8. Jewish-bank «окружение» proxy (no siege system).
9. Epidemic severity/spread; alchemy explosion consequences.
10. Morale scale (ZBS-era) — minimal scalar + floor now.
11. Alliance strength curve — linear default.
12. Chess — designer's own sketch; expect redesign.
13. Battle-abort penalty values.
14. `boutsPerCon` 3 vs "ten bouts" doc text — reconcile.

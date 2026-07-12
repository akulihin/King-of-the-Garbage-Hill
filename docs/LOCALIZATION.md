# Russian / English localization

> The game supports independent Russian and English presentation per account. Russian remains the canonical language of gameplay state and string dispatch; this is an implementation constraint, not a display-language limitation.

## 1. Non-negotiable invariant

Character names, passive names, action values, custom ids and other stringly typed keys stay exactly as they are in code and `characters.json`. Never translate a value before it reaches game logic. Translation happens only at presentation boundaries (`GameLocalization.cs:16-20`).

This allows one match to contain Russian- and English-speaking players without duplicating `GameClass`, changing passive dispatch, or leaking localized labels back into prediction/draft actions.

## 2. Locale ownership and selection

- `DiscordAccountClass.Language` persists `ru` or `en`; missing/unknown values normalize to Russian (`DiscordAccountClass.cs:27`, `GameLocalization.cs:45-51`). Existing account JSON needs no migration because the property initializer supplies `ru`.
- Discord users switch with the commands **\*язык**, **\*language** or **\*lang** (`General.cs:128-149`).
- The Vue RU/ENG selector defaults to English when there is no saved choice, persists `kotgh_locale`, and remains available on login and authenticated screens (`Web/VueClient/src/i18n.ts`, `App.vue:110-123`).
- `GameHub.SetLanguage` persists the web choice and immediately re-pushes personalized state when the connection is in a game (`GameHub.cs:83-96`). Reconnect authentication is followed by the client setting its current locale again.

## 3. Shared catalogs

`DataBase/localization.en.json` is bundled into both the backend output and the Vue build. Its sections are:

| Section | Purpose |
|---|---|
| `exact` | Russian source phrase → adapted English. Prefer this for full sentences, UI copy, tooltips and dynamic module descriptions. |
| `terms` | Presentation-only names and safe terminology. Replacements use word boundaries for single tokens; action values are never passed through this layer. |
| `russianExact` | English-first legacy UI copy → Russian. This completes Russian presentation on surfaces originally authored in English. |
| `phraseFallbacks` | Passive-aware English adaptations for canonical `|>Phrase<|` flavor-log records. Shared by the backend and Vue replay renderer; every Cyrillic `PhraseClass` identifier is covered. |
| `characters` | English biographies keyed by canonical character name. Contains every character entry. |
| `passives` | English mechanics text keyed by canonical passive name. Contains every unique passive from `characters.json`. |

At startup, the backend joins `characters`/`passives` to the canonical source text from `characters.json`, adding those source texts to its exact catalog (`GameLocalization.cs:247-265`). The client performs the same bidirectional join while bundling (`Web/VueClient/src/i18n.ts:16-31`). The Russian player text in `characters.json` remains untouched.

`DataBase/phrases.en.json` is the backend-owned flavor catalog. It is keyed by the stable C# `PhraseClass` field name; each group stores its canonical/English passive titles plus an ordered array of `{ russian, english }` phrase pairs. It currently covers all 235 groups and all 796 runtime variants. `PhraseLocalization.Populate` fails startup for a missing/extra group, count mismatch, a Russian title/body that differs from the untouched C# source at the same index, an empty adaptation, or Cyrillic in English (`PhraseLocalization.cs:19-69,165-198`). The duplicated Russian fields are validation data for old replays, not a replacement source: `PassiveLogRus` and all Russian producers remain unchanged.

`exact` and `russianExact` entries are also applied longest-first inside composite text, after dynamic templates. This matters for multi-line logs and Vue nodes split around interpolations: a phrase need not be the entire input to localize (`GameLocalization.cs:89-125`; `Web/VueClient/src/i18n.ts:47-61,155-191`).

Markup may split one grammatical phrase into separate tokens (for example `**обычных** очков`), and older replay projections may already contain English around one remaining Russian token (`Class: +2 Cкилла`). Catalog both the smallest safe mixed-script/markup token and any dynamic sentence fragment needed to finish those records. Justice encouragements, AWDKA troll outcomes and the last-second winner line are the reference cases; their canonical Russian producers are not rewritten.

Achievements and Daily Quests use separate typed bilingual catalogs. Achievement definitions carry paired names/descriptions/secret hints (`AchievementClass.cs:12-88`; DTO `GameStateDto.cs:1119-1139`); Daily Quest definitions carry paired names/descriptions plus stable nonlocalized lane/icon/aggregation metadata (`QuestClass.cs:26-68,208-260`; DTO `GameStateDto.cs:1064-1082`). This copy does not belong in `characters.json` or passive descriptions. Locked Achievement masking is applied symmetrically before the DTO leaves the server (`GameHub.cs:1645-1679`).

## 4. Backend boundaries

- All ordinary Discord command text and embeds pass through `ModuleBaseCustom` before sending (`ModuleBaseCustom.cs:14-18`, `ModuleBaseCustom.cs:61-65`).
- In-game embeds, transient messages and Discord component labels/options are localized immediately before build/send; component identifiers and select-option values remain canonical (`HelperFunctions.cs:237-244`, `GameLocalization.cs:198-222`).
- Personalized web logs, score sources, direct messages, media messages and the finished chronicle are projected in `GameStateMapper`; ordinary text is localized there, while bilingual phrase records and paired media fields remain language-neutral for live switching and replay capture (`GameStateMapper.cs:1088-1106`). Opponent/spectator visibility gates are unchanged.
- `PhraseClass` selects one shared RU/EN index and removes both entries together when a phrase is consumed (`CharactersPhrases.cs:1689-1738`). Personal/direct logs store a compact `|>PhraseV2<|` record containing both fully rendered variants; media DTOs carry explicit canonical/English fields. Discord resolves the record before log sorting, Vue resolves it at display time, and game-story input resolves Russian before stripping formatting (`PhraseLocalization.cs:219-309`; `GameUpdateMess.cs:832-837`; `GameStoryService.cs:273-275`). Prefixes, suffixes and target names are rendered into both variants. For old `|>Phrase<|` snapshots, the paired catalog resolves the exact authored body in passive context (including duplicate titles, ASCII-only bodies and multi-line variants) before the broad fallback is considered (`PhraseLocalization.cs:72-163`).

Do not localize inside `CharacterPassives`, `GameReactions`, `DoomsdayMachine` or similar game-state code unless the text is inherently per-user generated (Geralt's hint is the deliberate exception). Store canonical logs whenever possible and localize the viewer projection.

## 5. Vue boundary

The client keeps canonical state values in Pinia and localizes rendered text/accessible attributes through a DOM observer (`Web/VueClient/src/i18n.ts`). It records the original Vue-rendered value, so RU↔EN switching is reversible and later reactive updates are re-localized. Character/passive content comes from untouched `characters.json`, while the same backend-owned paired phrase JSON supplies exact legacy bodies and safe unambiguous fragments (`Web/VueClient/src/i18n.ts:1-93,207-271`). New bilingual phrase records are decoded before ordinary translation and kept opaque while the rest of a log block is processed. Old markers are matched by passive plus exact body before ordinary fragments or `phraseFallbacks`, including multi-line and ASCII-only memes (`i18n.ts:207-271,305-335`). Media cards select their paired fields directly (`MediaMessages.vue:9-22`). Passive descriptions are translated as one complete string before markdown. Input values, ids, object properties and SignalR action arguments are never translated.

Reward components select typed Achievement/Daily Quest pairs directly from `currentLocale` rather than passing dynamic DTO strings through gameplay-state translation (`AchievementBoard.vue:114-132`; `AchievementPopup.vue:47-59`; `DailyQuestBoard.vue:98-166`). Loot rarity, pity, actions and accessibility labels are likewise explicit EN/RU component copy (`LootBox.vue:129-165`). Achievement `CharacterNames` and Daily Quest IDs stay canonical so portrait lookup and reroll actions remain stable (`AchievementClass.cs:29-35`; quest mapping `GameHub.cs:775-803`).

Development builds warn in the console when an English-rendered node still contains Cyrillic (`Web/VueClient/src/i18n.ts`). When adding UI copy:

1. Add an exact catalog entry in both directions when the source surface is English-first.
2. Use `terms` only for names or tokens safe to replace in arbitrary presentation strings.
3. Test switching both ways after the component has reactively updated.

## 6. Generated text

- Game stories ask the LLM for paired RU and EN tagged adaptations and store both in one replay-safe HTML artifact. CSS shows only the active locale; a malformed legacy/single-language response still renders through the old fallback (`GameStoryService.cs:45-77`, `GameStoryService.cs:322-329`, `App.vue:301-304`).
- Geralt hints ask in the owning player's locale. Failures select a canonical Russian hint and project it through the shared exact catalog; there is no second English C# dictionary (`ClaudeHaikuService.cs:37-65`, `Geralt.cs:335-369`, `CP:4657-4713`).

## 7. Verification checklist

For any player-facing change:

1. Keep canonical gameplay identifiers unchanged.
2. Add/adapt English and Russian display text in the shared catalog.
3. Run `jq empty` for both `localization.en.json` and `phrases.en.json`.
4. Run `bash tools/audit-localization.sh`; it also runs `audit-phrases.sh`, which requires the paired schema plus exact field/count parity and rejects missing Russian or empty/Cyrillic English values. Runtime startup additionally checks every Russian title/body against C# by index.
5. Run `dotnet build` and `pnpm build`.
6. Run `bash tools/audit-passives.sh` if any passive-bearing source changed.
7. Run `bash tools/verify-docs.sh --changed` and the standard simulation suite for gameplay-bearing changes.

Both catalogs are copied by the project file (`King-of-the-Garbage-Hill.csproj:22-27`); forgetting either copy makes deployed localization incomplete.

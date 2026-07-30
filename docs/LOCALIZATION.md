# Russian / English localization

> The game supports independent Russian and English presentation per account. Russian remains the canonical language of gameplay state and string dispatch; this is an implementation constraint, not a display-language limitation.

## 1. Non-negotiable invariant

Character names, passive names, action values, custom ids and other stringly typed keys stay exactly as they are in code and `characters.json`. Never translate a value before it reaches game logic. Translation happens only at presentation boundaries (`GameLocalization.cs:16-20`).

This allows one match to contain Russian- and English-speaking players without duplicating `GameClass`, changing passive dispatch, or leaking localized labels back into prediction/draft actions.

## 2. Locale ownership and selection

- `DiscordAccountClass.Language` persists `ru` or `en`; missing/unknown values normalize to Russian (`DiscordAccountClass.cs:27`, `GameLocalization.cs:45-51`). Existing account JSON needs no migration because the property initializer supplies `ru`.
- Discord users switch with the commands **\*язык**, **\*language** or **\*lang** (`General.cs:128-149`).
- The shared Vue locale module defaults to English when `kotgh_locale` has not been saved, persists every browser choice under that key and updates `<html lang>`. The RU/ENG control is present in the authenticated shell and in the router-free 99LC/Empire shell (`Web/VueClient/src/platform/localization/locale.ts`; `App.vue` `changeLocale`; `StandaloneGameShell.vue`).
- Before login, the saved browser choice owns presentation. `GameHub.Authenticate` then returns the account's normalized persisted `language`, and the store applies it instead of writing the browser default back to the account (`GameHub.Authenticate`; `signalr.ts` `AuthenticatedSession`; `store/game.ts` authentication callback).
- `GameHub.SetLanguage` saves the normalized account value under the account monitor before changing the process locale registry. On success it emits `LanguageChanged` to every active connection for that account, returns the saved language and immediately re-pushes the player's active-game projection even when the change came from a lobby tab. On failure it restores the old account/registry values and the client restores its previous selection (`GameHub.SetLanguage`; `App.vue` `changeLocale`).
- Router-free 99LC and Empire's Endgame have no authenticated account connection; their selector changes only the shared browser preference.

## 3. Catalog layers

### 3.1 Structured product catalogs — required for new copy

Root `Localization/*.messages.json` is the single source for new static/reusable RU/EN copy. Catalogs currently exist for `shell`, `kotgh`, `battleship`, `clash`, `last-chances` and `empires-endgame`. Each file has this shape:

```json
{
  "product": "kotgh",
  "messages": {
    "kotgh.title": {
      "visibility": "public",
      "ru": "Король Мусорной Горы",
      "en": "King of the Garbage Hill"
    }
  }
}
```

The contract is:

- the stable key must begin with `<product>.`;
- both `ru` and `en` must be non-empty;
- named `{placeholders}` and their occurrence counts must be identical in both languages;
- `visibility` is explicit: `public` may ship to every browser, `owner` may be rendered only at an authenticated owner boundary, and `server` never leaves the backend as a catalog entry;
- a key is globally unique across product files, and repeated JSON properties within one file are rejected before ordinary deserialization can collapse them.

The backend project copies the catalogs to deployed `Localization/`. Constructing `MessageCatalog` during web startup first performs a duplicate-aware JSON walk, then validates file/schema/key/language/visibility/placeholder-occurrence invariants and fails startup on any error. `LocalizedMessage` carries a stable key and argument map; `MessageCatalog.Render` selects a locale and requires every argument. `LocalizedText {Ru, En}` is the paired boundary for generated or one-off prose that cannot use a reusable key (`King-of-the-Garbage-Hill/Localization/MessageCatalog.cs`; `King-of-the-Garbage-Hill/Localization/LocalizedText.cs`; `King-of-the-Garbage-Hill.csproj`; `Program.cs` `StartWebApi`).

Vite's `messageCatalogPlugin` performs matching build/dev validation and exposes only explicitly `public` definitions through `virtual:message-catalogs`. It watches both the catalog directory and every loaded file, so adding or editing a product catalog invalidates the development module without restarting Vite. `platform/localization/messages.ts` provides `message(key, arguments)` and `localizedText(value)`; components supply only keys/dynamic values, never parallel RU/EN literals. Owner/server definitions are absent from the browser module rather than hidden with CSS or inferred from their wording (`Web/VueClient/vite.config.ts` `messageCatalogPlugin`; `Web/VueClient/src/platform/localization/messages.ts`).

### 3.2 Legacy compatibility catalogs

`DataBase/localization.en.json`, `DataBase/phrases.en.json`, `GameLocalization` and the Vue DOM/arbitrary-string translator remain supported for existing gameplay producers, stored logs and replays. They are compatibility input, not the extension point for new static UI copy. `localization.en.json` is copied into backend output and transformed into the sanitized `virtual:public-localization` browser module. Its sections are:

| Section | Purpose |
|---|---|
| `exact` | Russian source phrase → adapted English. Prefer this for full sentences, UI copy, tooltips and dynamic module descriptions. |
| `terms` | Presentation-only names and safe terminology. Replacements use word boundaries for single tokens; action values are never passed through this layer. |
| `russianExact` | English-first legacy UI copy → Russian. This completes Russian presentation on surfaces originally authored in English. |
| `phraseFallbacks` | Passive-aware English adaptations for canonical `|>Phrase<|` flavor-log records. Shared by the backend and Vue replay renderer; every Cyrillic `PhraseClass` identifier is covered. |
| `characters` | English biographies keyed by canonical character name. Complete key coverage is enforced; an empty canonical biography remains empty in English rather than gaining invented text. |
| `passives` | English mechanics text keyed by canonical passive name. Complete key coverage is enforced for every canonical passive. |
| `browserPrivate` | Section/key metadata for backend translations that must not enter the public legacy Vue catalog. Vite rejects unknown sections, duplicate/stale keys and removes every listed record before serialization. |

The legacy sheet audit covers all 47 character definitions, 216 passive definitions and 132 `PhraseClass` display/fallback names. M198 completed the recent Homelander, Omni-man, TheBoys, ScamRat and Cthulhu-era gap without changing any canonical Russian source. The two empty Homelander/Omni-man biographies remain empty; Cthulhu's reversed clue remains reversed in English and its deliberately corrupted horror text uses Latin-base distortion so the presentation stays unreadable without leaking Cyrillic. Cthulhu-only prompts and old plain-log lines are ordinary `exact` records listed under `browserPrivate.exact`: the backend can localize them, but `publicLocalizationPlugin` excludes them even when their wording contains no private character/passive term. The live private sheet carries optional paired display fields beside untouched canonical character/passive identifiers; owner-only headings/actions and the public Thing board/fight labels use the same server-derived paired boundary. Vue selects those pairs through the shared locale state instead of depending on the static legacy catalog.

At startup, the backend joins `characters`/`passives` to the canonical source text from `characters.json`, adding those source texts to its legacy exact catalog (`GameLocalization.cs:247-265`). The client performs the same bidirectional join while bundling (`Web/VueClient/src/i18n.ts:16-31`). The Russian player text in `characters.json` remains untouched.

`DataBase/phrases.en.json` is the backend-owned flavor catalog. It is keyed by the stable C# `PhraseClass` field name; each group stores its canonical/English passive titles plus an ordered array of `{ russian, english }` phrase pairs. It currently covers all **253 groups** and **834 runtime variants**. `PhraseLocalization.Populate` fails startup for a missing/extra group, count mismatch, a Russian title/body that differs from the untouched C# source at the same index, an empty adaptation, or Cyrillic in English (`PhraseLocalization.cs:19-69,165-198`). The duplicated Russian fields are validation data for old replays, not a replacement source: `PassiveLogRus` and all Russian producers remain unchanged.

The ScamRat/Madara additions use five stable groups with exact source attribution:

- `ScamRatGpuSale` — `Сделка` / `Deal`: `Вероятность взрыва всего лишь один процент. Но не волнуйтесь, я уже 99 продал - ни одна не взорвалась. Это сотая. Берите, очень рекомендую!` / `The chance of an explosion is only one percent. Do not worry: I have already sold 99 and not one has exploded. This is the hundredth. Take it; I highly recommend it!`.
- `MadaraTwoFights` through `MadaraFiveFights` — `Бог шиноби` / `God of Shinobi`: two fights emit `Шаринган!` / `Sharingan!`; three append `Огненный шторм!` / `Firestorm!`; four append `Частичное Сусано!` / `Partial Susanoo!`; five or more append `Риннеган!` / `Rinnegan!`. Each group stores the complete cumulative newline-separated body, not only its newly appended line.

Gordon's removed H.E.V. Justice-threshold phrases no longer have a `PhraseClass` or `phrases.en.json` group. His owner-only round-3/`Молчание` lines, public release/failure math and decisive final sales line are emitted as bilingual `PhrasePayload` records directly from `GordonFreeman`, preserving exact Russian copy and replay-safe English without adding a broad fallback.

Jon's fixed defeat, Server-King, Castle entry/final and upper/lower-side transition phrases use seven paired `PhraseClass` groups. Difficulty-enemy text, the redirected enemy nickname, both equiprobable one-shot resurrection lines and all post-resurrection `I dun wan it` / `She muh queen` replacements carry their dynamic RU/EN bodies directly through `PhrasePayload`. `localization.en.json` contains Jon's canonical character/passive presentation entries, while the hidden resurrection description remains empty and no pre-trigger widget copy reveals it (`JonSnow.cs`; `CharactersPhrases.cs`; `PlayerCard.vue`).

`exact` and `russianExact` entries are also applied longest-first inside composite text, after dynamic templates. This matters for multi-line logs and Vue nodes split around interpolations: a phrase need not be the entire input to localize (`GameLocalization.cs:89-125`; `Web/VueClient/src/i18n.ts:47-61,155-191`).

Markup may split one grammatical phrase into separate tokens (for example `**обычных** очков`), and older replay projections may already contain English around one remaining Russian token (`Class: +2 Cкилла`). Catalog both the smallest safe mixed-script/markup token and any dynamic sentence fragment needed to finish those records. Justice encouragements, AWDKA troll outcomes and the last-second winner line are the reference cases; their canonical Russian producers are not rewritten.

Dynamic templates must also accept already-partially-localized replay forms. Nemesis defeat connectors therefore match both `вас обогнал` and `вас overtook`, then render `overtook you` without matching or rewriting the preceding player name. Block/Auto Move accept canonical and mixed action names; Rumbling's place and Mitsuki's point total capture only their dynamic numbers. Darksci's global tilt line follows the same idempotent pattern (`GameLocalization.cs:34-55`; `Web/VueClient/src/i18n.ts:104-125`).

Deterministic hardcoded character lines that do not pass through `PhraseClass` still belong in `localization.en.json`, not in a second frontend dictionary. Catalog both the canonical Markdown form and a markup-stripped replay form when a renderer may remove emphasis first; Armin's Rumbling warning is the reference case.

Gordon's presentation follows that split without changing any canonical identifier. `localization.en.json` adds `exact` adaptations for the headcrab loss, wake action, Halflife 3 announcements, failure/result labels, three staged choice pairs and freeze outcomes; `terms` maps `Гордон Фримен`, all four passive names and the headcrab/zombie nouns; and `passives` contains the four complete English mechanics descriptions, with the removed H.E.V. sentence absent from Монтировка.

Jon follows the same canonical-key rule: `terms` maps `Джон Сноу`, all six passive identifiers and королевские terminology; `characters`/`passives` contain the supplied public sheet text plus the hidden Server-King replacement, while `Мой дозор окончен` deliberately has an empty description. The generated English phrase groups and direct payloads translate presentation only; Russian identifiers remain the dispatch source.

Achievements and Daily Quests use separate typed bilingual catalogs. Achievement definitions carry paired names/descriptions/secret hints (`AchievementClass.cs:12-88`; DTO `GameStateDto.cs:1122-1142`); Daily Quest definitions carry paired names/descriptions plus stable nonlocalized lane/icon/aggregation metadata (`QuestClass.cs:26-68,208-260`; DTO `GameStateDto.cs:1067-1085`). This copy does not belong in `characters.json` or passive descriptions. Locked Achievement masking is applied symmetrically before the DTO leaves the server (`GameHub.cs:1645-1679`).

## 4. Backend boundaries

- Reusable new server-authored copy is represented as `LocalizedMessage`, then resolved by `MessageCatalog` at a presentation boundary after viewer/visibility selection. Generated prose that cannot use a key crosses as paired `LocalizedText`. Do not render a structured owner/server definition into a public DTO.
- All ordinary Discord command text and embeds pass through `ModuleBaseCustom` before sending (`ModuleBaseCustom.cs:14-18`, `ModuleBaseCustom.cs:61-65`).
- In-game embeds, transient messages and Discord component labels/options are localized immediately before build/send; component identifiers and select-option values remain canonical (`HelperFunctions.cs:237-244`, `GameLocalization.cs:198-222`).
- Personalized legacy web logs, score sources, direct messages, media messages and the finished chronicle are projected in `GameStateMapper`; arbitrary historic text is localized there, while bilingual phrase records and paired media fields remain language-neutral for live switching and replay capture (`GameStateMapper.cs:1095-1113`). Opponent/spectator visibility gates are unchanged.
- `PhraseClass` selects one shared RU/EN index and removes both entries together when a phrase is consumed (`CharactersPhrases.cs:1706-1755`). Personal/direct logs store a compact `|>PhraseV2<|` record containing both fully rendered variants; media DTOs carry explicit canonical/English fields. Discord resolves the record before log sorting, Vue resolves it at display time, and game-story input resolves Russian before stripping formatting (`PhraseLocalization.cs:219-309`; `GameUpdateMess.cs:834-839`; `GameStoryService.cs:571-578`). Prefixes, suffixes and target names are rendered into both variants. For old `|>Phrase<|` snapshots, the paired catalog resolves the exact authored body in passive context (including duplicate titles, ASCII-only bodies and multi-line variants) before the broad fallback is considered (`PhraseLocalization.cs:72-163`).

Do not localize inside `CharacterPassives`, `GameReactions`, `DoomsdayMachine` or similar game-state code unless the text is inherently per-user generated (Geralt's hint is the deliberate exception). Store canonical logs whenever possible and localize the viewer projection.

## 5. Vue boundary

`main.ts` selects one of five lazy application roots, and `platform/bootstrap.ts` gives each a fresh Vue/Pinia instance plus its optional product router. All five consume the same `platform/localization` locale and structured-message API. KOTGH, Battleship and network Clash each own a router; 99LC and Empire are router-free roots. Cross-product navigation reloads the root, while the persisted locale remains shared (`Web/VueClient/src/apps/registry.ts`; `Web/VueClient/src/platform/bootstrap.ts`; `Web/VueClient/src/platform/localization/`).

New component copy calls `message('product.stableKey', arguments)` or selects a typed `LocalizedText`; it does not embed parallel `currentLocale === ...` branches or add arbitrary text-replacement rules. `App.vue`, product navigation and `StandaloneGameShell.vue` are the migrated reference surfaces.

The client still keeps canonical gameplay state values in Pinia and installs the old DOM observer from `i18n.ts` after every root mounts. The observer owns that root's document body so Vue Teleport dialogs receive the same compatibility translation. This is deliberately a legacy compatibility adapter: it records the original Vue-rendered value so old RU↔EN surfaces and later reactive updates remain reversible. Character/passive content comes from untouched `characters.json`, while the backend-owned paired phrase JSON supplies exact historic bodies and safe unambiguous fragments (`Web/VueClient/src/i18n.ts:1-93,207-271`). New bilingual phrase records are decoded before ordinary translation and kept opaque while the rest of a log block is processed. Old markers are matched by passive plus exact body before ordinary fragments or `phraseFallbacks`, including multi-line and ASCII-only memes (`i18n.ts:207-271,305-335`). Media cards select their paired fields directly (`MediaMessages.vue:9-22`). Passive descriptions are translated as one complete string before markdown. Input values, ids, object properties and SignalR action arguments are never translated.

Reward components select typed Achievement/Daily Quest pairs directly from `currentLocale` rather than passing dynamic DTO strings through gameplay-state translation (`AchievementBoard.vue:114-132`; `AchievementPopup.vue:47-59`; `DailyQuestBoard.vue:98-166`). Loot rarity, pity, actions and accessibility labels are likewise explicit EN/RU component copy (`LootBox.vue:129-165`). Achievement `CharacterNames` and Daily Quest IDs stay canonical so portrait lookup and reroll actions remain stable (`AchievementClass.cs:29-35`; quest mapping `GameHub.cs:775-803`).

Development builds warn in the console when an English-rendered legacy node still contains Cyrillic (`Web/VueClient/src/i18n.ts`). When adding UI copy:

1. Add one product-prefixed key with `ru`, `en`, explicit visibility and matching placeholders to the relevant `Localization/*.messages.json`.
2. Resolve the key through `message()` (or use paired `LocalizedText` for genuinely generated prose); do not add another component-local RU/EN table.
3. Use the old `exact`/`terms`/DOM path only when preserving or repairing a historic log/replay form that cannot yet be keyed.
4. Test switching both ways after the component has reactively updated, and verify that owner/server messages are absent from browser output.

The Chronicle is a special HTML-rendering boundary: `FightAnimation.formatLetopis` localizes the complete Discord-markdown string before replacing bold/emphasis markers with HTML. This ordering is required for templates that span formatted dynamic names, such as `Они скинули **Darksci**! Сволочи!` and target-class rewards (`FightAnimation.vue:1024-1039`).

## 6. Generated text

- Game stories make independent RU and EN requests and store the results in one replay-safe HTML artifact. Each prompt requests plain prose in exactly one language, so Story does not depend on model-authored wrapper tags; empty/error results are isolated and cannot discard a successful sibling (`GameStoryService.cs:65-141,335-492`). Both prompts instruct the model to stay within 250 words / 1,700 characters; output is trusted without a second trimming pass (`GameStoryService.cs:448-492`). Both locale containers are always present: a failed or configuration-disabled side contains a localized unavailable message, and the global language CSS selects the visible side (`GameStoryService.cs:637-647`; `App.vue:371-373`). Turning off `GameStoryEnglishEnabled` is the explicit testing exception to generating both full stories; it skips the English API call without changing the Russian path (`Config.cs:27-29`; `GameStoryService.cs:65-71`).
- Geralt hints use the same paired generation contract in one Haiku request. Valid generated adaptations—or a paired static fallback on any failure—are embedded into one `PhraseV2` personal-log record, making arbitrary AI prose switchable in live web, Discord and replays without a client lookup (`ClaudeHaikuService.cs:34-108`; `CP:4791-4826`).

## 7. Verification checklist

For any player-facing change:

1. Keep canonical gameplay identifiers unchanged.
2. Add/adapt both languages and explicit visibility in the relevant structured product catalog. Keep placeholder names identical.
3. Run `jq empty Localization/*.messages.json`; backend startup and `pnpm build` independently validate the structured schema, while Vite proves that browser code can see only public entries.
4. If a legacy producer/replay form changed, also run `jq empty` for `localization.en.json` and `phrases.en.json`, then `bash tools/audit-localization.sh`. It runs `audit-phrases.sh`, requires paired field/count parity, rejects missing Russian or empty/Cyrillic English values, and validates every `browserPrivate` section/key exclusion. The complete character/passive/display inventory must pass; do not weaken the audit to hide a future gap.
5. Run `dotnet build` and `pnpm build`.
6. Run `bash tools/audit-passives.sh` if any passive-bearing source changed.
7. Run `bash tools/verify-docs.sh --changed`; this also runs the Empire-only automated-test policy audit. Run the standard simulation suite for gameplay-bearing KOTGH changes.

The project copies both legacy catalogs into `DataBase/` and structured catalogs into `Localization/` (`King-of-the-Garbage-Hill.csproj`); forgetting either deployment group makes its compatibility layer incomplete.

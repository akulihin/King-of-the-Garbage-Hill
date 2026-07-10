# Russian / English localization

> The game supports independent Russian and English presentation per account. Russian remains the canonical language of gameplay state and string dispatch; this is an implementation constraint, not a display-language limitation.

## 1. Non-negotiable invariant

Character names, passive names, action values, custom ids and other stringly typed keys stay exactly as they are in code and `characters.json`. Never translate a value before it reaches game logic. Translation happens only at presentation boundaries (`GameLocalization.cs:16-20`).

This allows one match to contain Russian- and English-speaking players without duplicating `GameClass`, changing passive dispatch, or leaking localized labels back into prediction/draft actions.

## 2. Locale ownership and selection

- `DiscordAccountClass.Language` persists `ru` or `en`; missing/unknown values normalize to Russian (`DiscordAccountClass.cs:27`, `GameLocalization.cs:45-51`). Existing account JSON needs no migration because the property initializer supplies `ru`.
- Discord users switch with the commands **\*язык**, **\*language** or **\*lang** (`General.cs:128-149`).
- The Vue selector auto-defaults from `navigator.language`, persists `kotgh_locale`, and remains available on login and authenticated screens (`Web/VueClient/src/i18n.ts`, `App.vue:110-113`).
- `GameHub.SetLanguage` persists the web choice and immediately re-pushes personalized state when the connection is in a game (`GameHub.cs:83-96`). Reconnect authentication is followed by the client setting its current locale again.

## 3. Shared catalog

`DataBase/localization.en.json` is bundled into both the backend output and the Vue build. Its sections are:

| Section | Purpose |
|---|---|
| `exact` | Russian source phrase → adapted English. Prefer this for full sentences, UI copy, tooltips and dynamic module descriptions. |
| `terms` | Presentation-only names and safe terminology. Replacements use word boundaries for single tokens; action values are never passed through this layer. |
| `russianExact` | English-first legacy UI copy → Russian. This completes Russian presentation on surfaces originally authored in English. |
| `characters` | English biographies keyed by canonical character name. Contains every character entry. |
| `passives` | English mechanics text keyed by canonical passive name. Contains every unique passive from `characters.json`. |

At startup, the backend joins `characters`/`passives` to the canonical source text from `characters.json`, adding those source texts to its exact catalog (`GameLocalization.cs:225-285`). The client performs the same join while bundling (`Web/VueClient/src/i18n.ts`). The Russian player text in `characters.json` remains untouched.

## 4. Backend boundaries

- All ordinary Discord command text and embeds pass through `ModuleBaseCustom` before sending (`ModuleBaseCustom.cs:14-18`, `ModuleBaseCustom.cs:61-65`).
- In-game embeds, transient messages and Discord component labels/options are localized immediately before build/send; component identifiers and select-option values remain canonical (`HelperFunctions.cs:237-244`, `GameLocalization.cs:198-222`).
- Personalized web logs, score sources, direct messages and media messages are localized in `GameStateMapper`; opponent/spectator visibility gates are unchanged (`GameStateMapper.cs:1159-1171`).
- Character flavor has hundreds of historical Russian quips. Exact/adapted text is used when catalogued; otherwise `PhraseForUser` selects a character/passive-aware English fallback so English never depends on understanding the Russian joke (`GameLocalization.cs:63-178`). Russian retains the original full phrase pool.

Do not localize inside `CharacterPassives`, `GameReactions`, `DoomsdayMachine` or similar game-state code unless the text is inherently per-user generated (Geralt's hint is the deliberate exception). Store canonical logs whenever possible and localize the viewer projection.

## 5. Vue boundary

The client keeps canonical state values in Pinia and localizes rendered text/accessible attributes through a DOM observer (`Web/VueClient/src/i18n.ts`). It records the original Vue-rendered value, so RU↔EN switching is reversible and later reactive updates are re-localized. It never touches input values, select values, ids, object properties or SignalR action arguments.

Development builds warn in the console when an English-rendered node still contains Cyrillic (`Web/VueClient/src/i18n.ts`). When adding UI copy:

1. Add an exact catalog entry in both directions when the source surface is English-first.
2. Use `terms` only for names or tokens safe to replace in arbitrary presentation strings.
3. Test switching both ways after the component has reactively updated.

## 6. Generated text

- Game stories ask the LLM for paired RU and EN tagged adaptations and store both in one replay-safe HTML artifact. CSS shows only the active locale; a malformed legacy/single-language response still renders through the old fallback (`GameStoryService.cs:45-77`, `GameStoryService.cs:322-329`, `App.vue:282-285`).
- Geralt hints ask in the owning player's locale. Failures select from matching static Russian/English dictionaries (`ClaudeHaikuService.cs:37-65`, `Geralt.cs:336-402`, `CP:4657-4688`).

## 7. Verification checklist

For any player-facing change:

1. Keep canonical gameplay identifiers unchanged.
2. Add/adapt English and Russian display text in the shared catalog.
3. Run `jq empty King-of-the-Garbage-Hill/DataBase/localization.en.json`.
4. Run `bash tools/audit-localization.sh` (coverage plus English-Cyrillic leak check).
5. Run `dotnet build` and `pnpm build`.
6. Run `bash tools/audit-passives.sh` if any passive-bearing source changed.
7. Run `bash tools/verify-docs.sh --changed` and the standard simulation suite for gameplay-bearing changes.

The catalog is copied by the project file (`King-of-the-Garbage-Hill.csproj:22-26`); forgetting that copy would make deployed Discord fall back to canonical text even while local source runs appear correct.

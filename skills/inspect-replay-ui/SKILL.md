---
name: inspect-replay-ui
description: Drive the rendered King of the Garbage Hill replay page in a real browser and collect screenshots, visible gameplay text, DOM state, interaction inventory, layout-overflow findings, console errors, and failed requests. Use for replay links, replay UI/UX review, player-perspective or round/fight inspection, visual regressions, and gameplay bug reports where the displayed cards, leaderboard, fights, passives, or logs must be examined instead of relying only on replay JSON.
---

# Inspect Replay UI

Inspect the same Vue gameplay components a player sees. Treat screenshots and rendered text as the primary evidence; use captured replay JSON only as an optional comparison layer.

## Run the probe

Resolve `scripts/inspect-replay-ui.mjs` relative to this `SKILL.md`, then run it from any directory:

```bash
node <skill-directory>/scripts/inspect-replay-ui.mjs \
  'https://kotgh.ozvmusic.com/replay/dbb5ca44?round=10&player=3' \
  --action 'tab:Все бои' \
  --action 'snapshot:all-fights' \
  --include-json
```

The tool uses the repository's existing Cypress dependency and a real browser. The matching Cypress browser binary and its OS libraries must already be installed. It authenticates with inert numeric viewer ID `1` because the application shell gates even public replay routes behind SignalR authentication; this does not create an account or mutate a game.

Useful options:

- `--action round:10` — reach a round through the visible arrow controls.
- `--action player:3` — click the zero-based player avatar used by replay URLs.
- `--action player-name:NAME` — click a player by rendered username.
- `--action tab:Все бои|Бои раунда|Летопись|История` — click a fight-panel tab.
- `--action fight:0` or `--action all-fight:0` — select a rendered fight.
- `--action play`, `--action skip`, `--action speed:4` — drive fight playback.
- `--action click:TEXT` or `--action selector:CSS` — click another visible control.
- `--action wait:1500` and `--action snapshot:NAME` — pause or preserve an intermediate view.
- `--viewport 1440x1000`, `--browser chrome`, `--headed`, `--output PATH`, `--viewer-id ID` — change the run environment.
- `--include-json` — preserve the replay API response for UI-versus-source comparison.

Run `node <skill-directory>/scripts/inspect-replay-ui.mjs --help` for the complete syntax.

## Inspect evidence

Read `summary.json` and `visible-text.txt`, then open the full-page and component screenshots with the image viewer. `summary.json` records the selected round/player, gameplay-region text, available controls, overflow candidates, console messages, failed requests, executed actions, and final URL. Use `page.html` only when the summary lacks a selector or attribute needed to explain the behavior.

Iterate with a smaller action sequence when an animation or tab hides the relevant state. Prefer one evidence directory per hypothesis so before/after states are not overwritten.

## Diagnose gameplay reports

1. Reproduce the exact URL before changing controls.
2. Capture the selected player's card, leaderboard, fight panel, skills, and logs.
3. Switch to `Все бои`, inspect each relevant fight, then use `Бои раунда` and `skip` to reveal final fight factors.
4. Compare another player perspective when visibility or ownership may matter.
5. Use `replay.json` only after documenting what the UI actually displayed.
6. Trace the displayed passive/stat/log through `docs/CHARACTERS.md`, `docs/GAME-DESIGN.md`, and their anchored code. If the result is a game bug, invoke the repository's `fix-finding` skill; do not silently edit gameplay from this inspection skill.

If the browser cannot start or reach the host, read `failure.json` and, when Cypress was launched, `browser-run.log`; report the infrastructure restriction and retain that evidence. Do not substitute a raw JSON-only conclusion for the missing UI reproduction.

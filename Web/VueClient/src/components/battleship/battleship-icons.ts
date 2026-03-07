// Inline SVG icon definitions for the Battleship UI.
// All icons use a 24x24 viewBox, 2px stroke, currentColor fill/stroke.

const svg = (inner: string, fill = 'none'): string =>
  `<svg xmlns="http://www.w3.org/2000/svg" width="24" height="24" viewBox="0 0 24 24" fill="${fill}" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">${inner}</svg>`

const svgFilled = (inner: string): string =>
  `<svg xmlns="http://www.w3.org/2000/svg" width="24" height="24" viewBox="0 0 24 24" fill="currentColor" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">${inner}</svg>`

// ---------------------------------------------------------------------------
// Cell States
// ---------------------------------------------------------------------------

const hit = svg(
  '<line x1="6" y1="6" x2="18" y2="18"/><line x1="18" y1="6" x2="6" y2="18"/>'
)

const miss = svg(
  '<circle cx="12" cy="12" r="4"/>'
)

const destroyed = svg(
  '<line x1="4" y1="4" x2="20" y2="20" stroke-width="3"/>' +
  '<line x1="20" y1="4" x2="4" y2="20" stroke-width="3"/>' +
  '<line x1="12" y1="2" x2="12" y2="6"/>' +
  '<line x1="12" y1="18" x2="12" y2="22"/>'
)

const burning = svgFilled(
  '<path d="M12 2c0 4-4 6-4 10a4 4 0 0 0 8 0c0-4-4-6-4-10z"/>' +
  '<path d="M12 8c0 2-1.5 3-1.5 5a1.5 1.5 0 0 0 3 0c0-2-1.5-3-1.5-5z" fill="none"/>'
)

const frozen = svg(
  '<line x1="12" y1="2" x2="12" y2="22"/>' +
  '<line x1="2" y1="12" x2="22" y2="12"/>' +
  '<line x1="5" y1="5" x2="19" y2="19"/>' +
  '<line x1="19" y1="5" x2="5" y2="19"/>' +
  '<line x1="12" y1="2" x2="9" y2="5"/><line x1="12" y1="2" x2="15" y2="5"/>' +
  '<line x1="12" y1="22" x2="9" y2="19"/><line x1="12" y1="22" x2="15" y2="19"/>'
)

const devastated = svgFilled(
  '<circle cx="9" cy="10" r="1.5" fill="none" stroke-width="2"/>' +
  '<circle cx="15" cy="10" r="1.5" fill="none" stroke-width="2"/>' +
  '<path d="M4 12c0-4.4 3.6-8 8-8s8 3.6 8 8c0 2-1 4-2 5l-1 3H7l-1-3c-1-1-2-3-2-5z" fill="none"/>' +
  '<line x1="8" y1="16" x2="16" y2="16"/>'
)

const captured = svg(
  '<line x1="6" y1="3" x2="6" y2="21"/>' +
  '<path d="M6 3h12l-3 4 3 4H6" fill="currentColor"/>'
)

const scratched = svg(
  '<line x1="5" y1="5" x2="19" y2="19" stroke-width="2.5"/>' +
  '<line x1="7" y1="3" x2="21" y2="17"/>'
)

const firePermanent = svgFilled(
  '<path d="M9 22c-2-2-4-5-4-9 0-5 4-7 4-11 1 2 2 3 3 4 1-3 3-5 5-6 0 4-1 7-1 10 1-1 2-2 2-4 2 2 3 5 3 7 0 4-2 7-4 9"/>' +
  '<path d="M12 22c-1-1-2-3-2-5s1-3 2-5c1 2 2 3 2 5s-1 4-2 5z" fill="none"/>'
)

// ---------------------------------------------------------------------------
// Summons
// ---------------------------------------------------------------------------

const ram = svg(
  '<circle cx="12" cy="6" r="2"/>' +
  '<line x1="12" y1="8" x2="12" y2="20"/>' +
  '<path d="M8 12h8"/>' +
  '<path d="M6 20c2-3 3-5 6-8"/><path d="M18 20c-2-3-3-5-6-8"/>'
)

const scout = svg(
  '<circle cx="10" cy="10" r="6"/>' +
  '<line x1="14.5" y1="14.5" x2="21" y2="21"/>' +
  '<line x1="21" y1="21" x2="19" y2="19" stroke-width="3"/>'
)

const brander = svg(
  '<circle cx="12" cy="14" r="7"/>' +
  '<path d="M12 7V3"/>' +
  '<path d="M12 3c2 0 3 1 4 2" fill="none"/>'
)

const cursedBoat = svgFilled(
  '<circle cx="9" cy="9" r="1.5" fill="none"/>' +
  '<circle cx="15" cy="9" r="1.5" fill="none"/>' +
  '<path d="M4 11c0-4.4 3.6-8 8-8s8 3.6 8 8c0 1.5-.5 3-1 4H5c-.5-1-1-2.5-1-4z" fill="none"/>' +
  '<path d="M7 15l-2 4h14l-2-4" fill="none"/>' +
  '<line x1="9" y1="19" x2="7" y2="22"/><line x1="15" y1="19" x2="17" y2="22"/>' +
  '<line x1="8" y1="12" x2="16" y2="12"/>'
)

const pirateBoat = svg(
  '<line x1="5" y1="5" x2="19" y2="19" stroke-width="2.5"/>' +
  '<line x1="19" y1="5" x2="5" y2="19" stroke-width="2.5"/>' +
  '<line x1="5" y1="5" x2="8" y2="5"/><line x1="5" y1="5" x2="5" y2="8"/>' +
  '<line x1="19" y1="5" x2="16" y2="5"/><line x1="19" y1="5" x2="19" y2="8"/>' +
  '<line x1="5" y1="19" x2="8" y2="19"/><line x1="5" y1="19" x2="5" y2="16"/>' +
  '<line x1="19" y1="19" x2="16" y2="19"/><line x1="19" y1="19" x2="19" y2="16"/>'
)

// ---------------------------------------------------------------------------
// Ships (silhouettes for ship cells)
// ---------------------------------------------------------------------------

const ship1 = svgFilled(
  '<circle cx="12" cy="12" r="4"/>'
)

const ship2 = svgFilled(
  '<rect x="4" y="9" width="16" height="6" rx="3"/>'
)

const ship3 = svgFilled(
  '<rect x="2" y="9" width="20" height="6" rx="3"/>' +
  '<circle cx="12" cy="7" r="1.5"/>'
)

const ship4 = svgFilled(
  '<path d="M2 12l3-4h14l3 4-3 4H5l-3-4z"/>' +
  '<circle cx="8" cy="12" r="1" fill="none" stroke-width="1.5"/>' +
  '<circle cx="16" cy="12" r="1" fill="none" stroke-width="1.5"/>'
)

// ---------------------------------------------------------------------------
// Weapons
// ---------------------------------------------------------------------------

const ballista = svg(
  '<path d="M4 12h14" stroke-width="2.5"/>' +
  '<path d="M18 12l4-1v2l-4-1z" fill="currentColor"/>' +
  '<path d="M6 6c3 2 4 4 4 6s-1 4-4 6" fill="none"/>' +
  '<line x1="2" y1="8" x2="2" y2="16"/>'
)

const catapult = svg(
  '<line x1="4" y1="20" x2="14" y2="20"/>' +
  '<line x1="6" y1="20" x2="6" y2="14"/>' +
  '<line x1="12" y1="20" x2="12" y2="14"/>' +
  '<line x1="6" y1="14" x2="16" y2="4"/>' +
  '<circle cx="17" cy="3" r="2"/>'
)

const incendiary = svg(
  '<path d="M8 20a4 4 0 0 1 0-8V8a2 2 0 0 1 4 0v4a4 4 0 0 1 0 8z" fill="none"/>' +
  '<path d="M10 8V5"/>' +
  '<path d="M10 3c1-1 2-1 3 0" fill="none"/>' +
  '<path d="M13 3c0-1 1-2 2-1" fill="none"/>'
)

const greekFire = svgFilled(
  '<circle cx="12" cy="12" r="10" fill="none" stroke-width="1.5" stroke-dasharray="3 2"/>' +
  '<path d="M12 4c0 3-3 5-3 8a3 3 0 0 0 6 0c0-3-3-5-3-8z"/>' +
  '<path d="M12 8c0 1.5-1.5 2.5-1.5 4a1.5 1.5 0 0 0 3 0c0-1.5-1.5-2.5-1.5-4z" fill="none"/>'
)

const whiteStone = svg(
  '<circle cx="12" cy="12" r="8"/>' +
  '<path d="M8 9c1-1 3-1 4 0" fill="none"/>' +
  '<path d="M14 10l2-2" fill="none"/>'
)

const buckshot = svgFilled(
  '<circle cx="12" cy="6" r="2"/>' +
  '<circle cx="7" cy="11" r="2"/>' +
  '<circle cx="17" cy="11" r="2"/>' +
  '<circle cx="9" cy="17" r="2"/>' +
  '<circle cx="15" cy="17" r="2"/>'
)

// ---------------------------------------------------------------------------
// UI
// ---------------------------------------------------------------------------

const anchor = svg(
  '<circle cx="12" cy="6" r="3"/>' +
  '<line x1="12" y1="9" x2="12" y2="21"/>' +
  '<path d="M5 18c0-4 3.5-7 7-7s7 3 7 7" fill="none"/>' +
  '<line x1="12" y1="21" x2="8" y2="21"/><line x1="12" y1="21" x2="16" y2="21"/>' +
  '<line x1="9" y1="13" x2="15" y2="13"/>'
)

const compass = svg(
  '<circle cx="12" cy="12" r="9"/>' +
  '<polygon points="12,3 14,11 12,9 10,11" fill="currentColor"/>' +
  '<polygon points="12,21 10,13 12,15 14,13" fill="currentColor"/>' +
  '<polygon points="3,12 11,10 9,12 11,14" fill="none"/>' +
  '<polygon points="21,12 13,14 15,12 13,10" fill="none"/>'
)

const cannon = svg(
  '<rect x="10" y="8" width="12" height="5" rx="1"/>' +
  '<circle cx="8" cy="17" r="4"/>' +
  '<line x1="8" y1="13" x2="10" y2="10"/>' +
  '<circle cx="8" cy="17" r="1.5" fill="currentColor"/>'
)

const skull = svg(
  '<path d="M4 12c0-4.4 3.6-8 8-8s8 3.6 8 8c0 2-1 4-2.5 5.5L16 21H8l-1.5-3.5C5 16 4 14 4 12z" fill="none"/>' +
  '<circle cx="9" cy="11" r="2"/>' +
  '<circle cx="15" cy="11" r="2"/>' +
  '<line x1="10" y1="16" x2="14" y2="16"/>'
)

const crossbones = svg(
  '<line x1="4" y1="4" x2="20" y2="20" stroke-width="2.5"/>' +
  '<line x1="20" y1="4" x2="4" y2="20" stroke-width="2.5"/>' +
  '<circle cx="4" cy="4" r="2" fill="currentColor"/>' +
  '<circle cx="20" cy="4" r="2" fill="currentColor"/>' +
  '<circle cx="4" cy="20" r="2" fill="currentColor"/>' +
  '<circle cx="20" cy="20" r="2" fill="currentColor"/>'
)

const flag = svg(
  '<line x1="5" y1="2" x2="5" y2="22"/>' +
  '<path d="M5 2h14l-3 5 3 5H5" fill="currentColor"/>'
)

const spyglass = svg(
  '<rect x="2" y="10" width="12" height="4" rx="2"/>' +
  '<circle cx="18" cy="12" r="4"/>' +
  '<circle cx="18" cy="12" r="2"/>'
)

const wheel = svg(
  '<circle cx="12" cy="12" r="8"/>' +
  '<circle cx="12" cy="12" r="3"/>' +
  '<line x1="12" y1="1" x2="12" y2="4"/>' +
  '<line x1="12" y1="20" x2="12" y2="23"/>' +
  '<line x1="1" y1="12" x2="4" y2="12"/>' +
  '<line x1="20" y1="12" x2="23" y2="12"/>' +
  '<line x1="4.2" y1="4.2" x2="6.3" y2="6.3"/>' +
  '<line x1="17.7" y1="17.7" x2="19.8" y2="19.8"/>' +
  '<line x1="19.8" y1="4.2" x2="17.7" y2="6.3"/>' +
  '<line x1="6.3" y1="17.7" x2="4.2" y2="19.8"/>'
)

const stun = svgFilled(
  '<polygon points="13,2 3,14 11,14 10,22 21,10 13,10"/>'
)

const penalty = svg(
  '<circle cx="12" cy="12" r="9"/>' +
  '<line x1="6" y1="6" x2="18" y2="18" stroke-width="2.5"/>'
)

// ---------------------------------------------------------------------------
// Exports
// ---------------------------------------------------------------------------

export const ICONS: Record<string, string> = {
  // Cell states
  hit,
  miss,
  destroyed,
  burning,
  frozen,
  devastated,
  captured,
  scratched,
  firePermanent,

  // Summons
  ram,
  scout,
  brander,
  cursedBoat,
  pirateBoat,

  // Ships
  ship1,
  ship2,
  ship3,
  ship4,

  // Weapons
  ballista,
  catapult,
  incendiary,
  greekFire,
  whiteStone,
  buckshot,

  // UI
  anchor,
  compass,
  cannon,
  skull,
  crossbones,
  flag,
  spyglass,
  wheel,
  stun,
  penalty,
}

/**
 * Returns an inline SVG string for the given icon key.
 * If `size` is provided and differs from the default 24, the width/height
 * attributes are replaced accordingly.
 * Returns an empty string when the key is not found.
 */
export function renderIcon(key: string, size?: number): string {
  const raw = ICONS[key]
  if (!raw) return ''
  if (size != null && size !== 24) {
    return raw
      .replace(/width="24"/, `width="${size}"`)
      .replace(/height="24"/, `height="${size}"`)
  }
  return raw
}

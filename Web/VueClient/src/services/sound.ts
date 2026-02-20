const SOUND_BASE_URL = 'https://r2.ozvmusic.com/kotgh/sound/'
const DEFAULT_BUTTON_SKIP_ATTR = 'data-sfx-skip-default'
const UTILITY_ATTR = 'data-sfx-utility'
const SOUND_CACHE_NAME = 'kotgh-sound-cache-v1'
const SOUND_CACHE_META_KEY = 'kotgh-sound-cache-meta-v1'
const SOUND_CACHE_TTL_MS = 24 * 60 * 60 * 1000

type StatKey = 'intelligence' | 'strength' | 'speed' | 'psyche'
type PlaybackChannel = 'lvl-up-extra'
type BlobEntry = { objectUrl: string; cachedAt: number }

// ── Volume config types ──────────────────────────────────────────────

type VolumeGroup =
  | 'buttons' | 'mainMenu' | 'utility'
  | 'attack' | 'levelUp' | 'moralExchange'
  | 'combo' | 'comboHype' | 'justice'
  | 'points' | 'doomsDay' | 'doomsDayWinLose'
  | 'doomsDayScrolls'

interface VolumeConfig {
  masterVolume: number
  groups: Record<VolumeGroup, number>
}

const DEFAULT_VOLUME_CONFIG: VolumeConfig = {
  masterVolume: 0.8,
  groups: {
    buttons: 0.7, mainMenu: 0.7, utility: 0.6,
    attack: 0.9, levelUp: 0.8, moralExchange: 0.8,
    combo: 0.85, comboHype: 0.9, justice: 0.85,
    points: 0.8, doomsDay: 0.9, doomsDayWinLose: 0.85,
    doomsDayScrolls: 0.7,
  },
}

const MASTER_VOLUME_STORAGE_KEY = 'kotgh-master-volume'

let cachedVolumeConfig: VolumeConfig | null = null
let volumeConfigPromise: Promise<VolumeConfig> | null = null
/** User override — applied on top of the config file's masterVolume */
let userMasterVolume: number | null = null

function loadUserMasterVolume(): number | null {
  try {
    const raw = localStorage.getItem(MASTER_VOLUME_STORAGE_KEY)
    if (raw === null) return null
    const val = parseFloat(raw)
    if (Number.isFinite(val) && val >= 0 && val <= 1) return val
    return null
  } catch {
    return null
  }
}

async function getVolumeConfig(): Promise<VolumeConfig> {
  if (cachedVolumeConfig) return cachedVolumeConfig
  if (volumeConfigPromise) return volumeConfigPromise
  volumeConfigPromise = (async () => {
    try {
      const resp = await fetch('/sound-config.json', { cache: 'no-store' })
      if (!resp.ok) return DEFAULT_VOLUME_CONFIG
      const json = await resp.json()
      const config: VolumeConfig = {
        masterVolume: typeof json.masterVolume === 'number' ? json.masterVolume : DEFAULT_VOLUME_CONFIG.masterVolume,
        groups: { ...DEFAULT_VOLUME_CONFIG.groups },
      }
      if (json.groups && typeof json.groups === 'object') {
        for (const key of Object.keys(DEFAULT_VOLUME_CONFIG.groups) as VolumeGroup[]) {
          if (typeof json.groups[key] === 'number') {
            config.groups[key] = json.groups[key]
          }
        }
      }
      cachedVolumeConfig = config
      return config
    } catch {
      return DEFAULT_VOLUME_CONFIG
    }
  })()
  return volumeConfigPromise
}

/** Effective master volume: user override (localStorage) takes priority over config file */
async function getEffectiveMasterVolume(): Promise<number> {
  if (userMasterVolume === null) {
    userMasterVolume = loadUserMasterVolume()
  }
  if (userMasterVolume !== null) return userMasterVolume
  const config = await getVolumeConfig()
  return config.masterVolume
}

/** Get current master volume (0–1). Returns cached/stored value synchronously when available. */
export function getMasterVolume(): number {
  if (userMasterVolume === null) {
    userMasterVolume = loadUserMasterVolume()
  }
  if (userMasterVolume !== null) return userMasterVolume
  if (cachedVolumeConfig) return cachedVolumeConfig.masterVolume
  return DEFAULT_VOLUME_CONFIG.masterVolume
}

/** Set master volume (0–1). Persisted to localStorage. */
export function setMasterVolume(vol: number): void {
  const clamped = Math.max(0, Math.min(1, vol))
  userMasterVolume = clamped
  try {
    localStorage.setItem(MASTER_VOLUME_STORAGE_KEY, String(clamped))
  } catch {
    // ignore storage errors
  }
}

// ── Sound context ────────────────────────────────────────────────────

export type SoundContext = 'game' | 'menu'
let currentSoundContext: SoundContext = 'menu'

export function setSoundContext(ctx: SoundContext): void {
  currentSoundContext = ctx
}

// ── Internal state ───────────────────────────────────────────────────

const channelAudio = new Map<PlaybackChannel, HTMLAudioElement>()
const soundBlobCache = new Map<string, BlobEntry>()
const pendingSrcResolve = new Map<string, Promise<string | null>>()

let pluckAudio: HTMLAudioElement | null = null
let pluckStopTimer: ReturnType<typeof setTimeout> | null = null
let didStorageSweep = false

function toSoundUrl(relativePath: string): string {
  return `${SOUND_BASE_URL}${relativePath}`
}

function nowMs(): number {
  return Date.now()
}

function isFresh(cachedAt: number): boolean {
  return nowMs() - cachedAt <= SOUND_CACHE_TTL_MS
}

function isBrowserStorageAvailable(): boolean {
  return typeof window !== 'undefined' && typeof caches !== 'undefined' && typeof localStorage !== 'undefined'
}

function readCacheMeta(): Record<string, number> {
  if (!isBrowserStorageAvailable()) return {}
  try {
    const raw = localStorage.getItem(SOUND_CACHE_META_KEY)
    if (!raw) return {}
    const parsed = JSON.parse(raw) as Record<string, unknown>
    const sanitized: Record<string, number> = {}
    for (const [url, value] of Object.entries(parsed)) {
      if (typeof value === 'number' && Number.isFinite(value)) {
        sanitized[url] = value
      }
    }
    return sanitized
  }
  catch {
    return {}
  }
}

function writeCacheMeta(meta: Record<string, number>): void {
  if (!isBrowserStorageAvailable()) return
  try {
    localStorage.setItem(SOUND_CACHE_META_KEY, JSON.stringify(meta))
  }
  catch {
    // ignore quota/storage errors and keep runtime functional
  }
}

function revokeBlobEntry(url: string): void {
  const entry = soundBlobCache.get(url)
  if (!entry) return
  URL.revokeObjectURL(entry.objectUrl)
  soundBlobCache.delete(url)
}

async function getSoundCacheStore(): Promise<Cache | null> {
  if (!isBrowserStorageAvailable()) return null
  try {
    return await caches.open(SOUND_CACHE_NAME)
  }
  catch {
    return null
  }
}

async function sweepExpiredCacheEntries(cacheStore: Cache, meta: Record<string, number>): Promise<void> {
  const expiredUrls = Object.entries(meta)
    .filter(([, cachedAt]) => !isFresh(cachedAt))
    .map(([url]) => url)

  if (expiredUrls.length === 0) return

  await Promise.all(expiredUrls.map(async (url) => {
    await cacheStore.delete(url)
    revokeBlobEntry(url)
    delete meta[url]
  }))
}

async function getOrFetchSoundBlobUrl(sourceUrl: string): Promise<string | null> {
  if (!isBrowserStorageAvailable()) return sourceUrl

  const existingResolve = pendingSrcResolve.get(sourceUrl)
  if (existingResolve) return existingResolve

  const resolvePromise = (async () => {
    const inMemory = soundBlobCache.get(sourceUrl)
    if (inMemory && isFresh(inMemory.cachedAt)) {
      return inMemory.objectUrl
    }
    if (inMemory && !isFresh(inMemory.cachedAt)) {
      revokeBlobEntry(sourceUrl)
    }

    const cacheStore = await getSoundCacheStore()
    if (!cacheStore) return sourceUrl

    const meta = readCacheMeta()
    if (!didStorageSweep) {
      await sweepExpiredCacheEntries(cacheStore, meta)
      didStorageSweep = true
      writeCacheMeta(meta)
    }

    const cachedAt = meta[sourceUrl]
    if (typeof cachedAt === 'number' && isFresh(cachedAt)) {
      const cachedResponse = await cacheStore.match(sourceUrl)
      if (cachedResponse) {
        const cachedBlob = await cachedResponse.blob()
        const objectUrl = URL.createObjectURL(cachedBlob)
        soundBlobCache.set(sourceUrl, { objectUrl, cachedAt })
        return objectUrl
      }
    }

    const response = await fetch(sourceUrl, { cache: 'no-store' }).catch(() => null)
    if (!response || !response.ok) return null

    const cachedNow = nowMs()
    await cacheStore.put(sourceUrl, response.clone())
    meta[sourceUrl] = cachedNow
    writeCacheMeta(meta)

    const freshBlob = await response.blob()
    const objectUrl = URL.createObjectURL(freshBlob)
    soundBlobCache.set(sourceUrl, { objectUrl, cachedAt: cachedNow })
    return objectUrl
  })()

  pendingSrcResolve.set(sourceUrl, resolvePromise)
  try {
    return await resolvePromise
  }
  finally {
    pendingSrcResolve.delete(sourceUrl)
  }
}

// ── Kill switch: detect runaway sounds ───────────────────────────────

const KILL_SWITCH_LIMIT = 15
const KILL_SWITCH_WINDOW_MS = 10_000
const recentPlays = new Map<string, number[]>()

function checkKillSwitch(relativePath: string): boolean {
  const now = Date.now()
  let timestamps = recentPlays.get(relativePath)
  if (!timestamps) {
    timestamps = []
    recentPlays.set(relativePath, timestamps)
  }
  // Prune entries outside the window
  while (timestamps.length > 0 && now - timestamps[0] > KILL_SWITCH_WINDOW_MS) {
    timestamps.shift()
  }
  timestamps.push(now)
  if (timestamps.length >= KILL_SWITCH_LIMIT) {
    console.error(`[SoundKillSwitch] "${relativePath}" played ${timestamps.length} times in ${KILL_SWITCH_WINDOW_MS / 1000}s — blocking further playback`)
    return true
  }
  return false
}

// ── Core playback ────────────────────────────────────────────────────

interface PlayClipOptions {
  channel?: PlaybackChannel
  group?: VolumeGroup
}

async function playClip(relativePath: string, options?: PlayClipOptions): Promise<boolean> {
  if (checkKillSwitch(relativePath)) return false
  const sourceUrl = await getOrFetchSoundBlobUrl(toSoundUrl(relativePath))
  if (!sourceUrl) return false

  const audio = new Audio(sourceUrl)
  audio.preload = 'auto'

  // Apply volume
  if (options?.group) {
    const masterVol = await getEffectiveMasterVolume()
    const config = await getVolumeConfig()
    audio.volume = masterVol * (config.groups[options.group] ?? 1)
  }

  if (options?.channel) {
    const previous = channelAudio.get(options.channel)
    if (previous) {
      previous.pause()
      previous.currentTime = 0
    }
    channelAudio.set(options.channel, audio)
  }

  try {
    await audio.play()
    return true
  }
  catch {
    return false
  }
}

// ── Helpers ──────────────────────────────────────────────────────────

function statSoundSuffix(stat: StatKey): 'int' | 'str' | 'spd' | 'psy' {
  switch (stat) {
    case 'intelligence': return 'int'
    case 'strength': return 'str'
    case 'speed': return 'spd'
    case 'psyche': return 'psy'
  }
}

function comboHypePath(comboSize: number): string | null {
  if (comboSize === 2) return 'ui_ux/combo/hype/combo_2.mp3'
  if (comboSize === 3) return 'ui_ux/combo/hype/combo_3.mp3'
  if (comboSize === 4 || comboSize === 5) return 'ui_ux/combo/hype/combo_5_ULTRA.mp3'
  if (comboSize >= 6) return 'ui_ux/combo/hype/combo_6_plus_ULTRA.mp3'
  return null
}

function sanitizeAttackCharacterName(characterName: string): string {
  return characterName.trim()
}

function weightedRandomButtonSound(): string {
  // everything_1 has 10x weight vs everything_2 and everything_3
  const roll = Math.random() * 12 // 10 + 1 + 1
  if (roll < 10) return 'buttons/everything_1.mp3'
  if (roll < 11) return 'buttons/everything_2.mp3'
  return 'buttons/everything_3.mp3'
}

function randomJusticeUpVariant(): string {
  const idx = Math.floor(Math.random() * 5) + 1
  return `ui_ux/justice/justice_up_${idx}.mp3`
}

// ── Exported sound functions ─────────────────────────────────────────

export function playDefaultButtonSound(): void {
  void playClip(weightedRandomButtonSound(), { group: 'buttons' })
}

export function playMainMenuButtonSound(): void {
  void playClip('buttons/main_menu.mp3', { group: 'mainMenu' })
}

export function playUtilitySound(): void {
  void playClip('buttons/utility.mp3', { group: 'utility' })
}

export function installGlobalButtonSound(): () => void {
  const onClick = (event: MouseEvent) => {
    const target = event.target
    if (!(target instanceof Element)) return
    const button = target.closest('button')
    if (!(button instanceof HTMLButtonElement)) return
    if (button.disabled) return
    if (button.getAttribute(DEFAULT_BUTTON_SKIP_ATTR) === 'true') return

    // Utility buttons (fight tabs, speed, thumbnails, predict)
    if (button.getAttribute(UTILITY_ATTR) === 'true') {
      playUtilitySound()
      return
    }

    // Context-aware: menu vs game
    if (currentSoundContext === 'menu') {
      playMainMenuButtonSound()
      return
    }

    playDefaultButtonSound()
  }

  document.addEventListener('click', onClick, true)
  return () => document.removeEventListener('click', onClick, true)
}

export async function playAttackSelection(characterName?: string): Promise<void> {
  // Always play attack_click simultaneously
  void playClip('buttons/attack/attack_click.mp3', { group: 'attack' })

  if (characterName) {
    const normalized = sanitizeAttackCharacterName(characterName)
    if (normalized && normalized !== '???') {
      const specialPath = `buttons/attack/special/attack_for_${encodeURIComponent(normalized)}.mp3`
      const played = await playClip(specialPath, { group: 'attack' })
      if (played) return
    }
  }

  const variants = ['buttons/attack/attack_1.mp3', 'buttons/attack/attack_2.mp3', 'buttons/attack/attack_3.mp3']
  const randomPath = variants[Math.floor(Math.random() * variants.length)]
  void playClip(randomPath, { group: 'attack' })
}

export function playBlockSound(): void {
  void playClip('buttons/attack/block.mp3', { group: 'attack' })
}

export function playLevelUpDefaultSound(): void {
  void playClip('buttons/lvl_up/lvl_up_default.mp3', { group: 'levelUp' })
}

export function playLevelUpStatSound(stat: StatKey, isMax: boolean): void {
  const suffix = statSoundSuffix(stat)
  const path = isMax ? `buttons/lvl_up/lvl_up_${suffix}_max.mp3` : `buttons/lvl_up/lvl_up_${suffix}.mp3`
  void playClip(path, { channel: 'lvl-up-extra', group: 'levelUp' })
}

export function playMoralForPointsSound(): void {
  void playClip('buttons/moral_exchange/moral_for_points.mp3', { group: 'moralExchange' })
}

export function playMoralForSkillSound(): void {
  void playClip('buttons/moral_exchange/moral_for_skill.mp3', { group: 'moralExchange' })
}

export function playJusticeResetSound(): void {
  void playClip('ui_ux/justice/justice_reset.mp3', { group: 'justice' })
}

export function playJusticeUpSound(): void {
  void playClip(randomJusticeUpVariant(), { group: 'justice' })
}

export function playPointsIncreaseSound(pointsDelta: number): void {
  if (pointsDelta <= 0) return
  if (pointsDelta >= 10) {
    void playClip('ui_ux/points/points_up_10_plus.mp3', { group: 'points' })
    return
  }
  if (pointsDelta >= 5) {
    void playClip('ui_ux/points/points_up_5_plus.mp3', { group: 'points' })
    return
  }
  void playClip('ui_ux/points/points_up_1_plus.mp3', { group: 'points' })
}

export function playComboPluck(comboSize: number): void {
  const safeCombo = Math.max(1, comboSize)
  const durationSec = Math.min(10, safeCombo * 0.85)

  void (async () => {
    const masterVol = await getEffectiveMasterVolume()
    const config = await getVolumeConfig()
    const vol = masterVol * config.groups.combo

    const sourceUrl = await getOrFetchSoundBlobUrl(toSoundUrl('ui_ux/combo/pluck.mp3'))
    if (!sourceUrl) return

    if (!pluckAudio) {
      pluckAudio = new Audio(sourceUrl)
      pluckAudio.preload = 'auto'
    }
    else if (pluckAudio.src !== sourceUrl) {
      pluckAudio.pause()
      pluckAudio = new Audio(sourceUrl)
      pluckAudio.preload = 'auto'
    }

    if (pluckStopTimer) {
      clearTimeout(pluckStopTimer)
      pluckStopTimer = null
    }

    pluckAudio.volume = vol
    pluckAudio.pause()
    pluckAudio.currentTime = 0
    void pluckAudio.play().catch(() => undefined)

    pluckStopTimer = setTimeout(() => {
      if (!pluckAudio) return
      pluckAudio.pause()
    }, durationSec * 1000)
  })()
}

export function playComboHype(comboSize: number): void {
  const path = comboHypePath(comboSize)
  if (!path) return

  const delayMs = comboSize >= 7 ? 200 : 0
  if (delayMs > 0) {
    setTimeout(() => { void playClip(path, { group: 'comboHype' }) }, delayMs)
    return
  }
  void playClip(path, { group: 'comboHype' })
}

// ── Dooms Day sound system ───────────────────────────────────────────

const FIGHT_SOUND_COUNT = 14
const FIGHT_FIN_COUNT = 3

/**
 * Pool of fight sounds: random excluding already-played, resets when exhausted.
 * 1/100 chance to play the DV variant instead.
 */
export class FightSoundPool {
  private remaining: number[] = []

  constructor() {
    this.reset()
  }

  reset(): void {
    this.remaining = Array.from({ length: FIGHT_SOUND_COUNT }, (_, i) => i + 1)
  }

  /** Pick a random fight sound for a regular factor step */
  next(): string {
    // 1/100 chance for DV variant
    if (Math.random() < 0.01) {
      return 'dooms_day/round_1/fight_dv/Fght DV.mp3'
    }

    if (this.remaining.length === 0) {
      this.reset()
    }
    const idx = Math.floor(Math.random() * this.remaining.length)
    const num = this.remaining.splice(idx, 1)[0]
    return `dooms_day/round_1/fight/fght_${num}.mp3`
  }

  /** Pick a random fight_fin sound for the LAST factor in a fight */
  nextFin(): string {
    const num = Math.floor(Math.random() * FIGHT_FIN_COUNT) + 1
    return `dooms_day/round_1/fight_fin/Fght Fin ${num}.mp3`
  }
}

/**
 * Resolve the correct win_lose file path based on the accumulated round results.
 *
 * roundResults: array of 'w' or 'l' for each completed round so far
 * isFinal: true when this is the final outcome of the entire fight
 * isAbsolute: true when this is the last fight of the turn AND all rounds same outcome
 */
export function doomsDayWinLosePath(
  roundResults: readonly ('w' | 'l')[],
  isFinal: boolean,
  isAbsolute: boolean,
): string {
  const len = roundResults.length
  if (len === 0) return 'dooms_day/win_lose/1_w.mp3' // fallback

  if (isFinal) {
    // Final outcome
    const allWin = roundResults.every(r => r === 'w')
    const allLose = roundResults.every(r => r === 'l')

    if (isAbsolute && allWin) return 'dooms_day/win_lose/f_ww_absolute.mp3'
    if (isAbsolute && allLose) return 'dooms_day/win_lose/f_ll_absolute.mp3'

    // Non-absolute finals
    if (len === 2) {
      const seq = roundResults.join('')
      if (seq === 'ww') return 'dooms_day/win_lose/f_ww.mp3'
      // any loss final
      return 'dooms_day/win_lose/f_any_lose.mp3'
    }
    if (len >= 3) {
      const first2 = roundResults.slice(0, 2).join('')
      if (first2 === 'lw') return 'dooms_day/win_lose/f_lww.mp3'
      if (first2 === 'wl') return 'dooms_day/win_lose/f_wlw.mp3'
      return 'dooms_day/win_lose/f_any_lose.mp3'
    }
    // len === 1 final (no R2/R3) — use the round 1 file
    return `dooms_day/win_lose/1_${roundResults[0]}.mp3`
  }

  // Non-final: per-round sounds
  if (len === 1) {
    return `dooms_day/win_lose/1_${roundResults[0]}.mp3`
  }
  if (len === 2) {
    const seq = roundResults.join('')
    return `dooms_day/win_lose/2_${seq}.mp3`
  }
  if (len >= 3) {
    // Round 3 result: first char is round number, then full sequence
    const seq = roundResults.join('')
    return `dooms_day/win_lose/3_${seq}.mp3`
  }

  return 'dooms_day/win_lose/1_w.mp3'
}

export function playDoomsDayFight(pool: FightSoundPool, isLastFactor: boolean): void {
  const path = isLastFactor ? pool.nextFin() : pool.next()
  void playClip(path, { group: 'doomsDay' })
}

export function playDoomsDayWinLose(
  roundResults: readonly ('w' | 'l')[],
  isFinal: boolean,
  isAbsolute: boolean,
): void {
  const path = doomsDayWinLosePath(roundResults, isFinal, isAbsolute)
  void playClip(path, { group: 'doomsDayWinLose' })
}

export function playDoomsDayDraw(): void {
  void playClip('dooms_day/round_2/draw.mp3', { group: 'doomsDayWinLose' })
}

export function playDoomsDayRndRoll(): void {
  void playClip('dooms_day/round_3/rnd_roll.mp3', { group: 'doomsDay' })
}

export function playDoomsDayScroll(): void {
  const num = Math.random() < 0.5 ? 1 : 2
  void playClip(`dooms_day/scrolls/scroll_${num}.mp3`, { group: 'doomsDayScrolls' })
}

export function playDoomsDayNoFights(): void {
  void playClip('dooms_day/no_fights_this_turn.mp3', { group: 'doomsDay' })
}

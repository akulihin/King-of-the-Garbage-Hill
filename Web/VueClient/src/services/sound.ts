const SOUND_BASE_URL = 'https://r2.ozvmusic.com/kotgh/sound/'
const DEFAULT_BUTTON_SKIP_ATTR = 'data-sfx-skip-default'
const SOUND_CACHE_NAME = 'kotgh-sound-cache-v1'
const SOUND_CACHE_META_KEY = 'kotgh-sound-cache-meta-v1'
const SOUND_CACHE_TTL_MS = 24 * 60 * 60 * 1000

type StatKey = 'intelligence' | 'strength' | 'speed' | 'psyche'
type PlaybackChannel = 'lvl-up-extra'
type BlobEntry = { objectUrl: string; cachedAt: number }

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

async function playClip(relativePath: string, channel?: PlaybackChannel): Promise<boolean> {
  const sourceUrl = await getOrFetchSoundBlobUrl(toSoundUrl(relativePath))
  if (!sourceUrl) return false

  const audio = new Audio(sourceUrl)
  audio.preload = 'auto'

  if (channel) {
    const previous = channelAudio.get(channel)
    if (previous) {
      previous.pause()
      previous.currentTime = 0
    }
    channelAudio.set(channel, audio)
  }

  try {
    await audio.play()
    return true
  }
  catch {
    return false
  }
}

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

export function playDefaultButtonSound(): void {
  void playClip('buttons/everything.mp3')
}

export function installGlobalButtonSound(): () => void {
  const onClick = (event: MouseEvent) => {
    const target = event.target
    if (!(target instanceof Element)) return
    const button = target.closest('button')
    if (!(button instanceof HTMLButtonElement)) return
    if (button.disabled) return
    if (button.getAttribute(DEFAULT_BUTTON_SKIP_ATTR) === 'true') return
    playDefaultButtonSound()
  }

  document.addEventListener('click', onClick, true)
  return () => document.removeEventListener('click', onClick, true)
}

export async function playAttackSelection(characterName?: string): Promise<void> {
  if (characterName) {
    const normalized = sanitizeAttackCharacterName(characterName)
    if (normalized && normalized !== '???') {
      const specialPath = `buttons/attack/special/attack_for_${encodeURIComponent(normalized)}.mp3`
      if (normalized === 'LeCrisp') {
        void playClip(specialPath)
        return
      }
      const played = await playClip(specialPath)
      if (played) return
    }
  }

  const variants = ['buttons/attack/attack_1.mp3', 'buttons/attack/attack_2.mp3', 'buttons/attack/attack_3.mp3']
  const randomPath = variants[Math.floor(Math.random() * variants.length)]
  void playClip(randomPath)
}

export function playBlockSound(): void {
  void playClip('buttons/attack/block.mp3')
}

export function playLevelUpDefaultSound(): void {
  void playClip('buttons/lvl_up/lvl_up_default.mp3')
}

export function playLevelUpStatSound(stat: StatKey, isMax: boolean): void {
  const suffix = statSoundSuffix(stat)
  const path = isMax ? `buttons/lvl_up/lvl_up_${suffix}_max.mp3` : `buttons/lvl_up/lvl_up_${suffix}.mp3`
  void playClip(path, 'lvl-up-extra')
}

export function playMoralForPointsSound(): void {
  void playClip('buttons/moral_exchange/moral_for_points.mp3')
}

export function playMoralForSkillSound(): void {
  void playClip('buttons/moral_exchange/moral_for_skill.mp3')
}

export function playJusticeResetSound(): void {
  void playClip('ui_ux/justice/justice_reset.mp3')
}

export function playJusticeUpSound(): void {
  void playClip('ui_ux/justice/justice_up.mp3')
}

export function playPointsIncreaseSound(pointsDelta: number): void {
  if (pointsDelta <= 0) return
  if (pointsDelta >= 10) {
    void playClip('ui_ux/points/points_up_10_plus.mp3')
    return
  }
  if (pointsDelta >= 5) {
    void playClip('ui_ux/points/points_up_5_plus.mp3')
    return
  }
  void playClip('ui_ux/points/points_up_1_plus.mp3')
}

export function playComboPluck(comboSize: number): void {
  const safeCombo = Math.max(1, comboSize)
  const durationSec = Math.min(10, safeCombo * 0.85)

  void (async () => {
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
    setTimeout(() => { void playClip(path) }, delayMs)
    return
  }
  void playClip(path)
}

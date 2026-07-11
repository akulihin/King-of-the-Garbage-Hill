<script setup lang="ts">
import { computed, nextTick, onMounted, onUnmounted, ref, watch, type CSSProperties } from 'vue'
import {
  CalendarClock,
  ChevronRight,
  CircleAlert,
  Gift,
  LoaderCircle,
  Package,
  PackageOpen,
  ShieldCheck,
  Sparkles,
  WalletCards,
} from 'lucide-vue-next'
import { currentLocale } from 'src/i18n'
import type { LootBoxOdds, LootBoxResult } from 'src/services/signalr'
import { playLootBoxOpeningSound, playLootBoxRevealSound } from 'src/services/sound'

const props = defineProps<{
  result: LootBoxResult | null
  odds: LootBoxOdds[]
  zbsBalance: number
  pendingLootBoxes: number
  lootBoxPity: number
  guaranteedRareIn: number
  isSaving: boolean
  saveError: string | null
}>()

const emit = defineEmits<{
  continue: [openingId: string]
  openAnother: [openingId: string]
}>()

const phase = ref<'opening' | 'reveal'>('opening')
const overlayRef = ref<HTMLElement | null>(null)
const dialogRef = ref<HTMLElement | null>(null)
const openingStartedAt = Date.now()
const reducedMotion = window.matchMedia('(prefers-reduced-motion: reduce)').matches
let revealTimer: ReturnType<typeof setTimeout> | null = null
let previousBodyOverflow = ''
let previouslyFocusedElement: HTMLElement | null = null
const isolatedBodyChildren: Array<{ element: HTMLElement; wasInert: boolean }> = []

const rarityClass = computed(() => phase.value === 'reveal'
  ? rarityKey(props.result?.rarity ?? 'common')
  : 'sealed')
const currentPity = computed(() => props.result?.lootBoxPity ?? props.lootBoxPity)
const currentGuaranteedRareIn = computed(() => props.result?.guaranteedRareIn ?? props.guaranteedRareIn)
const currentBalance = computed(() => props.result?.zbsBalance ?? props.zbsBalance)
const currentRemaining = computed(() => props.result?.remainingLootBoxes ?? props.pendingLootBoxes)
const pityThreshold = computed(() => Math.max(1, currentPity.value + currentGuaranteedRareIn.value))
const pityPercent = computed(() => Math.max(0, Math.min(100, (currentPity.value / pityThreshold.value) * 100)))

function focusableElements(): HTMLElement[] {
  if (!dialogRef.value) return []
  return Array.from(dialogRef.value.querySelectorAll<HTMLElement>(
    'button:not([disabled]), summary, [href], input:not([disabled]), select:not([disabled]), textarea:not([disabled]), [tabindex]:not([tabindex="-1"])',
  )).filter(element => element.getClientRects().length > 0)
}

function focusFirstControl(): void {
  const first = focusableElements()[0]
  const target = first ?? dialogRef.value
  target?.focus({ preventScroll: true })
}

function isolateBackground(): void {
  const overlay = overlayRef.value
  if (!overlay) return
  for (const child of Array.from(document.body.children)) {
    if (!(child instanceof HTMLElement) || child === overlay || child.contains(overlay)) continue
    isolatedBodyChildren.push({ element: child, wasInert: child.inert })
    child.inert = true
  }
}

function restoreBackground(): void {
  for (const { element, wasInert } of isolatedBodyChildren.splice(0)) {
    element.inert = wasInert
  }
}

function keepFocusInside(event: FocusEvent): void {
  if (!(event.target instanceof Node) || dialogRef.value?.contains(event.target)) return
  focusFirstControl()
}

onMounted(async () => {
  previouslyFocusedElement = document.activeElement instanceof HTMLElement ? document.activeElement : null
  previousBodyOverflow = document.body.style.overflow
  document.body.style.overflow = 'hidden'
  isolateBackground()
  document.addEventListener('focusin', keepFocusInside)
  if (!props.result) playLootBoxOpeningSound()
  await nextTick()
  focusFirstControl()
})

onUnmounted(() => {
  document.removeEventListener('focusin', keepFocusInside)
  restoreBackground()
  document.body.style.overflow = previousBodyOverflow
  if (revealTimer) clearTimeout(revealTimer)
  if (previouslyFocusedElement?.isConnected) {
    previouslyFocusedElement.focus({ preventScroll: true })
  }
})

watch(() => props.result?.openingId, (openingId) => {
  if (!openingId || !props.result) return
  if (revealTimer) clearTimeout(revealTimer)
  const minimumOpeningMs = reducedMotion ? 0 : props.result ? 1150 : 0
  const delay = Math.max(reducedMotion ? 0 : 160, minimumOpeningMs - (Date.now() - openingStartedAt))
  revealTimer = setTimeout(async () => {
    phase.value = 'reveal'
    playLootBoxRevealSound(props.result?.rarity ?? 'common')
    await nextTick()
    focusFirstControl()
  }, delay)
}, { immediate: true })

watch(() => props.isSaving, async (isSaving) => {
  await nextTick()
  if (isSaving) dialogRef.value?.focus({ preventScroll: true })
  else focusFirstControl()
})

function t(english: string, russian: string): string {
  return currentLocale.value === 'ru' ? russian : english
}

function rarityKey(rarity: string): string {
  const key = rarity.toLocaleLowerCase()
  return ['common', 'uncommon', 'rare', 'epic', 'legendary'].includes(key) ? key : 'common'
}

function rarityLabel(rarity: string): string {
  const labels: Record<string, [string, string]> = {
    common: ['Common', 'Обычная'],
    uncommon: ['Uncommon', 'Необычная'],
    rare: ['Rare', 'Редкая'],
    epic: ['Epic', 'Эпическая'],
    legendary: ['Legendary', 'Легендарная'],
  }
  const label = labels[rarityKey(rarity)] ?? labels.common
  return t(label[0], label[1])
}

function chanceText(chance: number): string {
  return `${new Intl.NumberFormat(currentLocale.value === 'ru' ? 'ru-RU' : 'en-CA', {
    maximumFractionDigits: 2,
  }).format(chance)}%`
}

function openedDate(value: string): string {
  const date = new Date(value)
  if (Number.isNaN(date.getTime())) return ''
  return new Intl.DateTimeFormat(currentLocale.value === 'ru' ? 'ru-RU' : 'en-CA', {
    day: 'numeric',
    month: 'short',
    hour: '2-digit',
    minute: '2-digit',
  }).format(date)
}

function continueFlow(): void {
  if (phase.value !== 'reveal' || !props.result || props.isSaving) return
  emit('continue', props.result.openingId)
}

function openAnother(): void {
  if (phase.value !== 'reveal' || !props.result || props.isSaving || props.result.remainingLootBoxes <= 0) return
  emit('openAnother', props.result.openingId)
}

function onDialogKeydown(event: KeyboardEvent): void {
  if (event.key === 'Escape' && phase.value === 'reveal' && !props.isSaving) {
    event.preventDefault()
    continueFlow()
    return
  }
  if (event.key !== 'Tab' || !dialogRef.value) return
  const focusable = focusableElements()
  if (focusable.length === 0) {
    event.preventDefault()
    dialogRef.value.focus()
    return
  }
  const first = focusable[0]
  const last = focusable[focusable.length - 1]
  const active = document.activeElement
  if (event.shiftKey && (active === first || active === dialogRef.value || !dialogRef.value.contains(active))) {
    event.preventDefault()
    last.focus()
  }
  else if (!event.shiftKey && (active === last || active === dialogRef.value || !dialogRef.value.contains(active))) {
    event.preventDefault()
    first.focus()
  }
}

function sparkStyle(index: number): CSSProperties {
  const angle = (index * 47) % 360
  return {
    '--spark-angle': `${angle}deg`,
    '--spark-distance': `${72 + (index % 4) * 16}px`,
    '--spark-delay': `${(index % 5) * 70}ms`,
  } as CSSProperties
}
</script>

<template>
  <Teleport to="body">
    <Transition name="loot-overlay" appear>
      <div ref="overlayRef" class="lootbox-overlay">
        <section
          ref="dialogRef"
          class="lootbox-dialog"
          :class="[`rarity-${rarityClass}`, `phase-${phase}`]"
          role="dialog"
          aria-modal="true"
          :aria-busy="isSaving"
          :aria-labelledby="phase === 'opening' ? 'loot-opening-title' : 'loot-result-title'"
          tabindex="-1"
          @keydown="onDialogKeydown"
        >
          <div class="loot-aurora" aria-hidden="true" />
          <div class="loot-grid" aria-hidden="true" />

          <Transition name="loot-phase" mode="out-in">
            <div v-if="phase === 'opening'" key="opening" class="opening-phase" role="status" aria-live="polite">
              <span class="loot-kicker"><Sparkles :size="14" aria-hidden="true" /> {{ t('Reward chamber', 'Камера наград') }}</span>
              <h2 id="loot-opening-title">{{ t('Opening loot box', 'Открываем лутбокс') }}</h2>
              <p>{{ t('Your server-secured reward is being revealed…', 'Показываем награду, уже определённую сервером…') }}</p>

              <div class="crate-stage" aria-hidden="true">
                <span class="crate-orbit orbit-one" />
                <span class="crate-orbit orbit-two" />
                <span class="crate-shadow" />
                <span class="loot-crate">
                  <span class="crate-band crate-band-left" />
                  <span class="crate-band crate-band-right" />
                  <Package :size="58" :stroke-width="1.4" />
                  <span class="crate-lock"><Gift :size="17" /></span>
                </span>
                <span class="opening-sparks">
                  <i v-for="index in 18" :key="index" :style="sparkStyle(index)" />
                </span>
              </div>

              <div class="opening-meta">
                <span><PackageOpen :size="15" aria-hidden="true" /> {{ t(`${pendingLootBoxes} available`, `Доступно: ${pendingLootBoxes}`) }}</span>
                <span><ShieldCheck :size="15" aria-hidden="true" /> {{ t(`Rare+ in ${Math.max(1, guaranteedRareIn)}`, `Редкая+ через ${Math.max(1, guaranteedRareIn)}`) }}</span>
              </div>
            </div>

            <div v-else-if="result" key="reveal" class="reveal-phase" aria-live="assertive">
              <span v-if="result.wasPityUpgrade" class="pity-upgrade">
                <ShieldCheck :size="15" aria-hidden="true" />
                {{ t('Pity upgraded this reward', 'Счётчик удачи улучшил награду') }}
              </span>
              <span v-else class="loot-kicker"><Sparkles :size="14" aria-hidden="true" /> {{ t('Loot revealed', 'Награда открыта') }}</span>

              <div class="reveal-icon-stage" aria-hidden="true">
                <span class="reveal-halo" />
                <span class="revealed-box"><PackageOpen :size="64" :stroke-width="1.35" /></span>
                <span class="reveal-sparks">
                  <i v-for="index in 22" :key="index" :style="sparkStyle(index)" />
                </span>
              </div>

              <span class="revealed-rarity">{{ rarityLabel(result.rarity) }}</span>
              <h2 id="loot-result-title">{{ t('ZBS cache', 'Тайник ZBS') }}</h2>
              <div class="reward-amount">
                <img :src="'/art/emojis/zbs.png'" alt="ZBS">
                <strong>+{{ result.zbsAmount }}</strong>
                <span>ZBS</span>
              </div>

              <div class="reward-summary">
                <div>
                  <WalletCards :size="18" aria-hidden="true" />
                  <span><small>{{ t('New balance', 'Новый баланс') }}</small><strong>{{ currentBalance }} ZBS</strong></span>
                </div>
                <div>
                  <PackageOpen :size="18" aria-hidden="true" />
                  <span><small>{{ t('Boxes left', 'Осталось лутбоксов') }}</small><strong>{{ currentRemaining }}</strong></span>
                </div>
                <div v-if="openedDate(result.openedAt)">
                  <CalendarClock :size="18" aria-hidden="true" />
                  <span><small>{{ t('Opened', 'Открыто') }}</small><strong>{{ openedDate(result.openedAt) }}</strong></span>
                </div>
              </div>

              <div class="pity-panel">
                <div class="pity-copy">
                  <span>{{ t('Rare+ guarantee', 'Гарантия редкой+') }}</span>
                  <strong>{{ currentGuaranteedRareIn > 0 ? t(`${currentGuaranteedRareIn} boxes`, `${currentGuaranteedRareIn} лутб.`) : t('Next box', 'Следующий лутбокс') }}</strong>
                </div>
                <div class="pity-track" role="progressbar" :aria-valuenow="Math.round(pityPercent)" aria-valuemin="0" aria-valuemax="100">
                  <span :style="{ width: `${pityPercent}%` }" />
                </div>
                <small>{{ t(`Pity counter: ${currentPity}`, `Счётчик удачи: ${currentPity}`) }}</small>
              </div>

              <details v-if="odds.length" class="odds-panel">
                <summary>{{ t('View base drop rates', 'Показать базовые шансы') }}</summary>
                <div class="odds-list">
                  <div v-for="entry in odds" :key="entry.rarity" :class="`odds-${rarityKey(entry.rarity)}`">
                    <span class="odds-rarity">{{ rarityLabel(entry.rarity) }}</span>
                    <strong>{{ chanceText(entry.chance) }}</strong>
                    <span>{{ entry.minZbs === entry.maxZbs ? `${entry.minZbs} ZBS` : `${entry.minZbs}–${entry.maxZbs} ZBS` }}</span>
                  </div>
                </div>
                <p class="odds-note">
                  {{ t(
                    'On a guaranteed box, a Common or Uncommon roll upgrades to Rare.',
                    'В гарантированном лутбоксе Обычный или Необычный результат повышается до Редкого.',
                  ) }}
                </p>
              </details>

              <div v-if="isSaving" class="loot-save-feedback saving" role="status" aria-live="polite">
                <LoaderCircle :size="17" aria-hidden="true" />
                <span>{{ t('Confirming your reveal…', 'Подтверждаем просмотр…') }}</span>
              </div>
              <div v-else-if="saveError" class="loot-save-feedback error" role="alert">
                <CircleAlert :size="17" aria-hidden="true" />
                <span>
                  <strong>{{ t(
                    'Could not confirm this reveal. Your reward is safe; try again.',
                    'Не удалось подтвердить просмотр. Награда сохранена — попробуйте ещё раз.',
                  ) }}</strong>
                  <small>{{ saveError }}</small>
                </span>
              </div>

              <div class="loot-actions">
                <button class="btn btn-ghost" type="button" :disabled="isSaving" @click="continueFlow">
                  {{ isSaving ? t('Confirming…', 'Подтверждаем…') : t('Continue', 'Продолжить') }}
                </button>
                <button
                  v-if="result.remainingLootBoxes > 0"
                  class="btn open-another"
                  type="button"
                  :disabled="isSaving"
                  @click="openAnother"
                >
                  <PackageOpen :size="17" aria-hidden="true" />
                  {{ isSaving ? t('Confirming…', 'Подтверждаем…') : t('Open another', 'Открыть ещё') }}
                  <ChevronRight :size="16" aria-hidden="true" />
                </button>
              </div>
            </div>
          </Transition>
        </section>
      </div>
    </Transition>
  </Teleport>
</template>

<style scoped>
.lootbox-overlay {
  position: fixed;
  inset: 0;
  z-index: 3400;
  display: flex;
  align-items: center;
  justify-content: center;
  padding: 18px;
  background: radial-gradient(circle at 50% 42%, rgba(32, 30, 42, 0.78), rgba(4, 4, 7, 0.95) 70%);
  backdrop-filter: blur(11px);
}

.lootbox-dialog {
  --rarity: #b5bcc4;
  position: relative;
  isolation: isolate;
  width: min(520px, 100%);
  max-height: calc(100svh - 28px);
  overflow: hidden auto;
  padding: 31px 32px 26px;
  border: 1px solid color-mix(in srgb, var(--rarity) 48%, var(--border-subtle));
  border-radius: 22px;
  outline: none;
  background: linear-gradient(155deg, color-mix(in srgb, var(--rarity) 8%, #2d2933), #17161c 70%);
  box-shadow: 0 30px 100px rgba(0, 0, 0, 0.72), 0 0 58px color-mix(in srgb, var(--rarity) 15%, transparent), inset 0 1px 0 rgba(255, 255, 255, 0.08);
}
.rarity-common { --rarity: #b5bcc4; }
.rarity-sealed { --rarity: #aaa4bd; }
.rarity-uncommon { --rarity: #67d391; }
.rarity-rare { --rarity: #69adff; }
.rarity-epic { --rarity: #c68cff; }
.rarity-legendary { --rarity: #f3c85b; }
.loot-aurora { position: absolute; z-index: -2; inset: -40% -20% 48%; background: radial-gradient(ellipse, color-mix(in srgb, var(--rarity) 32%, transparent), transparent 68%); filter: blur(24px); animation: loot-aurora 2.3s ease-in-out infinite; }
.loot-grid { position: absolute; z-index: -3; inset: 0; opacity: 0.15; background-image: linear-gradient(rgba(255,255,255,.025) 1px, transparent 1px), linear-gradient(90deg, rgba(255,255,255,.025) 1px, transparent 1px); background-size: 22px 22px; mask-image: linear-gradient(black, transparent 76%); }
.opening-phase,
.reveal-phase { display: flex; align-items: center; flex-direction: column; text-align: center; }
.loot-kicker,
.pity-upgrade { display: inline-flex; align-items: center; gap: 7px; min-height: 25px; padding: 3px 9px; color: var(--rarity); border: 1px solid color-mix(in srgb, var(--rarity) 25%, transparent); border-radius: 12px; background: color-mix(in srgb, var(--rarity) 7%, transparent); font-size: 9px; font-weight: 900; letter-spacing: 1.5px; text-transform: uppercase; }
.pity-upgrade { color: var(--accent-gold); border-color: rgba(240, 200, 80, 0.28); background: rgba(240, 200, 80, 0.08); }
.opening-phase h2,
.reveal-phase h2 { margin: 9px 0 3px; color: var(--text-primary); font-size: 28px; font-weight: 900; line-height: 1.15; }
.opening-phase > p { color: var(--text-muted); font-size: 11px; }

.crate-stage { position: relative; width: 240px; height: 220px; display: grid; place-items: center; margin: 6px 0 2px; }
.crate-orbit { position: absolute; width: 168px; height: 168px; border: 1px solid color-mix(in srgb, var(--rarity) 30%, transparent); border-radius: 50%; animation: crate-orbit 2.4s linear infinite; }
.orbit-two { width: 205px; height: 205px; border-style: dashed; opacity: 0.5; animation-duration: 7s; animation-direction: reverse; }
.crate-shadow { position: absolute; bottom: 29px; width: 130px; height: 24px; border-radius: 50%; background: rgba(0, 0, 0, 0.48); filter: blur(8px); animation: crate-shadow 0.72s ease-in-out infinite; }
.loot-crate { position: relative; width: 116px; height: 105px; display: grid; place-items: center; color: var(--rarity); border: 2px solid color-mix(in srgb, var(--rarity) 70%, white); border-radius: 16px; background: linear-gradient(145deg, color-mix(in srgb, var(--rarity) 18%, #39343e), #1c1a20 64%); box-shadow: 0 18px 35px rgba(0, 0, 0, 0.45), 0 0 34px color-mix(in srgb, var(--rarity) 22%, transparent), inset 0 1px 0 rgba(255, 255, 255, 0.12); animation: crate-shake 0.72s ease-in-out infinite; }
.crate-band { position: absolute; top: 0; bottom: 0; width: 9px; background: color-mix(in srgb, var(--rarity) 45%, #222); box-shadow: inset 1px 0 rgba(255, 255, 255, 0.08); }
.crate-band-left { left: 18px; }
.crate-band-right { right: 18px; }
.crate-lock { position: absolute; bottom: -10px; left: 50%; width: 31px; height: 31px; display: grid; place-items: center; transform: translateX(-50%); color: #17161c; border: 2px solid #26232a; border-radius: 9px; background: var(--rarity); }
.opening-sparks,
.reveal-sparks { position: absolute; top: 50%; left: 50%; width: 1px; height: 1px; }
.opening-sparks i,
.reveal-sparks i { --spark-angle: 0deg; --spark-distance: 90px; --spark-delay: 0ms; position: absolute; width: 5px; height: 5px; border-radius: 50%; background: var(--rarity); box-shadow: 0 0 7px var(--rarity); animation: opening-spark 1.15s ease-out var(--spark-delay) infinite; }
.opening-meta { display: flex; justify-content: center; flex-wrap: wrap; gap: 7px; }
.opening-meta span { display: inline-flex; align-items: center; gap: 5px; padding: 5px 8px; color: var(--text-muted); border: 1px solid var(--glass-border); border-radius: 8px; background: rgba(255, 255, 255, 0.03); font-size: 9px; font-weight: 750; }

.reveal-icon-stage { position: relative; width: 170px; height: 150px; display: grid; place-items: center; margin: 5px 0 -2px; }
.reveal-halo { position: absolute; width: 124px; height: 124px; border-radius: 50%; background: radial-gradient(circle, color-mix(in srgb, var(--rarity) 28%, transparent), transparent 68%); box-shadow: 0 0 45px color-mix(in srgb, var(--rarity) 25%, transparent); animation: reveal-halo 1.8s ease-in-out infinite; }
.revealed-box { position: relative; z-index: 2; width: 100px; height: 100px; display: grid; place-items: center; color: var(--rarity); border: 2px solid color-mix(in srgb, var(--rarity) 72%, white); border-radius: 27px; background: linear-gradient(145deg, color-mix(in srgb, var(--rarity) 19%, #3c3742), #1d1b21); box-shadow: 0 0 42px color-mix(in srgb, var(--rarity) 28%, transparent), inset 0 1px 0 rgba(255,255,255,.13); animation: revealed-box-in 0.7s var(--ease-spring) both; }
.reveal-sparks i { animation-iteration-count: 1; animation-duration: 1s; }
.revealed-rarity { color: var(--rarity); font-size: 10px; font-weight: 900; letter-spacing: 3px; text-transform: uppercase; }
.reward-amount { display: flex; align-items: center; justify-content: center; gap: 8px; margin: 8px 0 16px; padding: 8px 18px; color: var(--accent-green); border: 1px solid rgba(63, 167, 61, 0.18); border-radius: 14px; background: rgba(63, 167, 61, 0.08); animation: reward-pop 0.58s var(--ease-spring) 0.25s both; }
.reward-amount img { width: 35px; height: 35px; object-fit: contain; filter: drop-shadow(0 0 8px rgba(63, 167, 61, 0.3)); }
.reward-amount strong { font: 950 34px/1 var(--font-mono); }
.reward-amount span { align-self: flex-end; margin-bottom: 4px; font-size: 10px; font-weight: 900; }
.reward-summary { width: 100%; display: grid; grid-template-columns: repeat(3, minmax(0, 1fr)); overflow: hidden; border: 1px solid var(--glass-border); border-radius: 12px; background: rgba(0, 0, 0, 0.16); }
.reward-summary > div { min-width: 0; display: flex; align-items: center; gap: 8px; padding: 10px; color: var(--text-muted); border-right: 1px solid var(--glass-border); text-align: left; }
.reward-summary > div:last-child { border-right: 0; }
.reward-summary > div > span { min-width: 0; display: flex; flex-direction: column; }
.reward-summary small { color: var(--text-dim); font-size: 8px; font-weight: 800; letter-spacing: 0.4px; text-transform: uppercase; }
.reward-summary strong { overflow: hidden; color: var(--text-secondary); font: 800 10px/1.4 var(--font-mono); text-overflow: ellipsis; white-space: nowrap; }
.pity-panel { width: 100%; margin-top: 9px; padding: 9px 11px; border: 1px solid var(--glass-border); border-radius: 10px; background: rgba(255, 255, 255, 0.025); text-align: left; }
.pity-copy { display: flex; justify-content: space-between; margin-bottom: 6px; color: var(--text-muted); font-size: 9px; font-weight: 750; }
.pity-copy strong { color: var(--accent-purple); }
.pity-track { height: 4px; overflow: hidden; border-radius: 3px; background: var(--bg-inset); }
.pity-track span { display: block; height: 100%; border-radius: inherit; background: linear-gradient(90deg, var(--accent-purple), var(--accent-gold)); transition: width 0.55s ease; }
.pity-panel > small { display: block; margin-top: 5px; color: var(--text-dim); font-size: 8px; }
.odds-panel { width: 100%; margin-top: 8px; border: 1px solid var(--glass-border); border-radius: 10px; background: rgba(0, 0, 0, 0.12); text-align: left; }
.odds-panel summary { min-height: 38px; display: flex; align-items: center; padding: 7px 11px; color: var(--text-muted); cursor: pointer; font-size: 9px; font-weight: 800; }
.odds-list { padding: 0 10px 9px; }
.odds-list > div { --odds-color: var(--text-muted); display: grid; grid-template-columns: 1fr auto 90px; gap: 8px; align-items: center; padding: 5px 2px; border-top: 1px solid var(--glass-border); font-size: 9px; }
.odds-list strong { color: var(--odds-color); font-family: var(--font-mono); }
.odds-list > div > span:last-child { color: var(--text-dim); text-align: right; }
.odds-rarity { color: var(--odds-color); font-weight: 800; }
.odds-note { margin: 0 10px 10px; color: var(--text-dim); font-size: 8px; line-height: 1.45; }
.odds-uncommon { --odds-color: #67d391 !important; }
.odds-rare { --odds-color: #69adff !important; }
.odds-epic { --odds-color: #c68cff !important; }
.odds-legendary { --odds-color: #f3c85b !important; }
.loot-save-feedback { width: 100%; min-height: 42px; display: flex; align-items: center; justify-content: center; gap: 8px; margin-top: 9px; padding: 8px 10px; border: 1px solid var(--glass-border); border-radius: 10px; font-size: 9px; font-weight: 750; }
.loot-save-feedback.saving { color: var(--accent-purple); background: rgba(180, 150, 255, 0.07); }
.loot-save-feedback.saving svg { animation: loot-save-spin 0.85s linear infinite; }
.loot-save-feedback.error { align-items: flex-start; color: var(--accent-red); border-color: rgba(239, 128, 128, 0.22); background: rgba(239, 128, 128, 0.07); text-align: left; }
.loot-save-feedback.error > span { min-width: 0; display: flex; flex-direction: column; gap: 2px; }
.loot-save-feedback.error strong { color: var(--text-secondary); font-size: 9px; font-weight: 800; }
.loot-save-feedback.error small { overflow-wrap: anywhere; color: var(--text-dim); font-size: 8px; line-height: 1.4; }
.loot-actions { width: 100%; display: flex; justify-content: center; gap: 8px; margin-top: 13px; }
.loot-actions .btn { min-height: 43px; flex: 1; }
.open-another { color: #17161c; background: linear-gradient(135deg, color-mix(in srgb, var(--rarity) 75%, white), var(--rarity)); box-shadow: 0 7px 20px color-mix(in srgb, var(--rarity) 17%, transparent); }

@keyframes crate-shake { 0%, 100% { transform: translateY(0) rotate(-1deg); } 30% { transform: translateY(-6px) rotate(2deg); } 60% { transform: translateY(1px) rotate(-2deg); } }
@keyframes crate-shadow { 0%, 100% { transform: scaleX(1); opacity: .55; } 50% { transform: scaleX(.82); opacity: .35; } }
@keyframes crate-orbit { to { transform: rotate(360deg); } }
@keyframes opening-spark { from { opacity: 0; transform: rotate(var(--spark-angle)) translateX(30px) scale(.4); } 30% { opacity: 1; } to { opacity: 0; transform: rotate(var(--spark-angle)) translateX(var(--spark-distance)) scale(0); } }
@keyframes loot-aurora { 0%, 100% { opacity: .5; transform: scale(.9); } 50% { opacity: .9; transform: scale(1.08); } }
@keyframes revealed-box-in { from { opacity: 0; transform: scale(.25) rotate(-22deg); } 65% { transform: scale(1.12) rotate(4deg); } to { opacity: 1; transform: scale(1) rotate(0); } }
@keyframes reveal-halo { 0%, 100% { transform: scale(.88); opacity: .55; } 50% { transform: scale(1.1); opacity: 1; } }
@keyframes reward-pop { from { opacity: 0; transform: scale(.7) translateY(8px); } to { opacity: 1; transform: scale(1) translateY(0); } }
@keyframes loot-save-spin { to { transform: rotate(360deg); } }

.loot-phase-enter-active,
.loot-phase-leave-active { transition: opacity .22s ease, transform .22s ease; }
.loot-phase-enter-from { opacity: 0; transform: translateY(8px) scale(.98); }
.loot-phase-leave-to { opacity: 0; transform: translateY(-8px) scale(.98); }
.loot-overlay-enter-active,
.loot-overlay-leave-active { transition: opacity .25s ease; }
.loot-overlay-enter-from,
.loot-overlay-leave-to { opacity: 0; }

@media (max-width: 560px) {
  .lootbox-overlay { align-items: flex-end; padding: 8px; }
  .lootbox-dialog { width: 100%; max-height: calc(100svh - 8px); padding: 25px 16px 18px; border-radius: 20px 20px 12px 12px; }
  .crate-stage { height: 198px; }
  .reward-summary { grid-template-columns: 1fr 1fr; }
  .reward-summary > div { border-bottom: 1px solid var(--glass-border); }
  .reward-summary > div:nth-child(2) { border-right: 0; }
  .reward-summary > div:last-child { grid-column: 1 / -1; border-bottom: 0; }
  .loot-actions { flex-direction: column-reverse; }
  .loot-actions .btn { min-height: 46px; width: 100%; }
}

@media (prefers-reduced-motion: reduce) {
  .loot-aurora,
  .crate-orbit,
  .crate-shadow,
  .loot-crate,
  .opening-sparks i,
  .reveal-sparks i,
  .reveal-halo,
  .revealed-box,
  .reward-amount { animation: none; }
  .opening-sparks,
  .reveal-sparks { display: none; }
  .loot-save-feedback.saving svg { animation: none; }
  .pity-track span,
  .loot-phase-enter-active,
  .loot-phase-leave-active,
  .loot-overlay-enter-active,
  .loot-overlay-leave-active { transition: none; }
}
</style>

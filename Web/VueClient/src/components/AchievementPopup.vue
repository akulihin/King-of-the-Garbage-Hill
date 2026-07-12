<script setup lang="ts">
import { computed, nextTick, onMounted, onUnmounted, ref, watch, type CSSProperties } from 'vue'
import { Check, ChevronRight, CircleAlert, Gift, LoaderCircle, Sparkles, X } from 'lucide-vue-next'
import AchievementIcon from 'src/components/achievements/AchievementIcon.vue'
import { useFocusTrapDialog } from 'src/composables/useFocusTrapDialog'
import { currentLocale } from 'src/i18n'
import type { AchievementEntry } from 'src/services/signalr'
import { playAchievementUnlockSound } from 'src/services/sound'
import { useGameStore } from 'src/store/game'

const props = defineProps<{
  achievements: AchievementEntry[]
  isSaving: boolean
  saveError: string | null
}>()

const emit = defineEmits<{ dismiss: [] }>()
const store = useGameStore()
const currentIndex = ref(0)
const { overlayRef, dialogRef, focusFirstControl, trapTabKey } = useFocusTrapDialog()
let isDialogMounted = false

const current = computed(() => props.achievements[currentIndex.value] ?? null)
const hasNext = computed(() => currentIndex.value < props.achievements.length - 1)
const total = computed(() => props.achievements.length)

onMounted(() => {
  isDialogMounted = true
  if (store.characterList.length === 0) void store.fetchCharacterList()
})

onUnmounted(() => {
  isDialogMounted = false
})

watch(() => props.achievements, () => {
  currentIndex.value = 0
})

watch(() => current.value?.id, async () => {
  if (!current.value) return
  playAchievementUnlockSound(current.value.rarity)
  if (!isDialogMounted) return
  await nextTick()
  focusFirstControl()
}, { immediate: true })

watch(() => props.isSaving, async (isSaving) => {
  await nextTick()
  if (isSaving) dialogRef.value?.focus({ preventScroll: true })
  else focusFirstControl()
})

function t(english: string, russian: string): string {
  return currentLocale.value === 'ru' ? russian : english
}

function localizedName(achievement: AchievementEntry): string {
  return currentLocale.value === 'ru' ? achievement.nameRu || achievement.name : achievement.name
}

function localizedDescription(achievement: AchievementEntry): string {
  return currentLocale.value === 'ru'
    ? achievement.descriptionRu || achievement.description
    : achievement.description
}

function rarityKey(rarity: string): string {
  const key = rarity.toLocaleLowerCase()
  return ['common', 'uncommon', 'rare', 'epic', 'legendary'].includes(key) ? key : 'common'
}

function rarityLabel(rarity: string): string {
  const labels: Record<string, [string, string]> = {
    common: ['Common', 'Обычное'],
    uncommon: ['Uncommon', 'Необычное'],
    rare: ['Rare', 'Редкое'],
    epic: ['Epic', 'Эпическое'],
    legendary: ['Legendary', 'Легендарное'],
  }
  const label = labels[rarityKey(rarity)] ?? labels.common
  return t(label[0], label[1])
}

function characterAvatar(name: string): string {
  return store.characterList.find(character => character.name === name)?.avatar ?? ''
}

function next(): void {
  if (props.isSaving) return
  if (hasNext.value) currentIndex.value++
  else emit('dismiss')
}

function skipAll(): void {
  if (props.isSaving) return
  emit('dismiss')
}

function onDialogKeydown(event: KeyboardEvent): void {
  if (event.key === 'Escape') {
    event.preventDefault()
    if (!props.isSaving) skipAll()
    return
  }
  trapTabKey(event)
}

function particleStyle(index: number): CSSProperties {
  const angle = (index * 137.5) % 360
  const distance = 105 + (index % 5) * 24
  return {
    '--particle-angle': `${angle}deg`,
    '--particle-distance': `${distance}px`,
    '--particle-delay': `${(index % 6) * 45}ms`,
    '--particle-size': `${4 + (index % 3) * 2}px`,
  } as CSSProperties
}
</script>

<template>
  <Teleport to="body">
    <Transition name="achievement-celebration" appear>
      <div v-if="current" ref="overlayRef" class="celebration-overlay">
        <section
          ref="dialogRef"
          class="celebration-card"
          :class="`rarity-${rarityKey(current.rarity)}`"
          role="dialog"
          aria-modal="true"
          :aria-busy="isSaving"
          :aria-labelledby="`achievement-title-${current.id}`"
          :aria-describedby="`achievement-description-${current.id}`"
          tabindex="-1"
          @keydown="onDialogKeydown"
        >
          <div class="celebration-aurora" aria-hidden="true" />
          <div class="celebration-rays" aria-hidden="true" />
          <div class="celebration-particles" aria-hidden="true">
            <i v-for="index in 24" :key="index" :style="particleStyle(index)" />
          </div>

          <button
            class="skip-button"
            type="button"
            :aria-label="total > 1 ? t('Skip all achievement celebrations', 'Пропустить все поздравления') : t('Close', 'Закрыть')"
            :disabled="isSaving"
            @click="skipAll"
          >
            <X :size="18" aria-hidden="true" />
          </button>

          <div class="celebration-content" aria-live="polite">
            <div class="unlock-kicker">
              <Sparkles :size="15" aria-hidden="true" />
              {{ t('Achievement unlocked', 'Достижение открыто') }}
              <Sparkles :size="15" aria-hidden="true" />
            </div>

            <div class="unlock-icon-stage" aria-hidden="true">
              <span class="icon-orbit icon-orbit-one" />
              <span class="icon-orbit icon-orbit-two" />
              <span class="unlock-icon">
                <AchievementIcon :icon="current.icon" :size="54" :stroke-width="1.55" />
                <span class="unlock-check"><Check :size="15" :stroke-width="3.5" /></span>
              </span>
            </div>

            <div class="rarity-label">{{ rarityLabel(current.rarity) }}</div>
            <h2 :id="`achievement-title-${current.id}`">{{ localizedName(current) }}</h2>
            <p :id="`achievement-description-${current.id}`">{{ localizedDescription(current) }}</p>

            <div v-if="current.characterNames.length" class="unlock-characters">
              <span v-for="name in current.characterNames" :key="name" class="unlock-character">
                <span class="unlock-avatar">
                  <img v-if="characterAvatar(name)" :src="characterAvatar(name)" :alt="name">
                  <span v-else>{{ name.slice(0, 1) }}</span>
                </span>
                <strong>{{ name }}</strong>
              </span>
            </div>

            <div
              v-if="current.rewardZbs > 0 || current.rewardLootBoxes > 0"
              class="unlock-reward-panel"
            >
              <span class="reward-heading">{{ t('Reward claimed', 'Награда получена') }}</span>
              <span v-if="current.rewardZbs > 0" class="unlock-reward reward-zbs">
                <img :src="'/art/emojis/zbs.png'" alt="ZBS">
                <strong>+{{ current.rewardZbs }}</strong>
                <span>ZBS</span>
              </span>
              <span v-if="current.rewardLootBoxes > 0" class="unlock-reward reward-box">
                <Gift :size="21" aria-hidden="true" />
                <strong>+{{ current.rewardLootBoxes }}</strong>
                <span>{{ t('Loot box', 'Лутбокс') }}</span>
              </span>
            </div>

            <div v-if="total > 1" class="celebration-progress" :aria-label="t('Unlock progress', 'Прогресс поздравлений')">
              <span
                v-for="index in total"
                :key="index"
                :class="{ active: index - 1 <= currentIndex }"
              />
              <strong>{{ currentIndex + 1 }} / {{ total }}</strong>
            </div>

            <div v-if="isSaving" class="acknowledge-feedback saving" role="status" aria-live="polite">
              <LoaderCircle :size="17" aria-hidden="true" />
              <span>{{ t('Saving this celebration…', 'Сохраняем это поздравление…') }}</span>
            </div>
            <div v-else-if="saveError" class="acknowledge-feedback error" role="alert">
              <CircleAlert :size="17" aria-hidden="true" />
              <span>
                <strong>{{ t(
                  'Could not confirm this celebration. Your reward is safe — try again.',
                  'Не удалось подтвердить поздравление. Награда сохранена — попробуйте ещё раз.',
                ) }}</strong>
                <small>{{ saveError }}</small>
              </span>
            </div>

            <div class="celebration-actions">
              <button v-if="total > 1" class="btn btn-ghost skip-all" type="button" :disabled="isSaving" @click="skipAll">
                {{ t('Skip all', 'Пропустить все') }}
              </button>
              <button class="btn celebration-next" type="button" :disabled="isSaving" @click="next">
                {{ isSaving ? t('Saving…', 'Сохраняем…') : hasNext ? t('Next achievement', 'Следующее достижение') : t('Continue', 'Продолжить') }}
                <ChevronRight :size="17" aria-hidden="true" />
              </button>
            </div>
          </div>
        </section>
      </div>
    </Transition>
  </Teleport>
</template>

<style scoped>
.celebration-overlay {
  position: fixed;
  inset: 0;
  z-index: 3300;
  display: flex;
  align-items: center;
  justify-content: center;
  padding: 20px;
  background:
    radial-gradient(circle at 50% 42%, rgba(35, 31, 46, 0.72), rgba(5, 5, 8, 0.94) 68%),
    rgba(0, 0, 0, 0.88);
  backdrop-filter: blur(10px);
}

.celebration-card {
  --rarity: #b5bcc4;
  position: relative;
  isolation: isolate;
  width: min(520px, 100%);
  max-height: calc(100svh - 32px);
  overflow: hidden auto;
  padding: 34px 34px 28px;
  color: var(--text-primary);
  border: 1px solid color-mix(in srgb, var(--rarity) 60%, transparent);
  border-radius: 22px;
  outline: none;
  background: linear-gradient(155deg, color-mix(in srgb, var(--rarity) 10%, #28252e), #18171d 68%);
  box-shadow:
    0 28px 90px rgba(0, 0, 0, 0.7),
    0 0 55px color-mix(in srgb, var(--rarity) 20%, transparent),
    inset 0 1px 0 rgba(255, 255, 255, 0.09);
  animation: celebration-card-in 0.7s var(--ease-spring) both;
}

.rarity-common { --rarity: #b5bcc4; }
.rarity-uncommon { --rarity: #67d391; }
.rarity-rare { --rarity: #69adff; }
.rarity-epic { --rarity: #c68cff; }
.rarity-legendary { --rarity: #f3c85b; }

.celebration-aurora {
  position: absolute;
  z-index: -2;
  inset: -35% -20% 35%;
  background: radial-gradient(ellipse, color-mix(in srgb, var(--rarity) 34%, transparent), transparent 68%);
  filter: blur(18px);
  animation: aurora-breathe 2.8s ease-in-out infinite;
}

.celebration-rays {
  position: absolute;
  z-index: -1;
  top: -160px;
  left: 50%;
  width: 420px;
  height: 420px;
  transform: translateX(-50%);
  opacity: 0.16;
  background: repeating-conic-gradient(from 0deg, var(--rarity) 0deg 2deg, transparent 2deg 18deg);
  mask-image: radial-gradient(circle, black 5%, transparent 68%);
  animation: rays-turn 22s linear infinite;
}

.skip-button {
  position: absolute;
  z-index: 5;
  top: 12px;
  right: 12px;
  width: 38px;
  height: 38px;
  display: grid;
  place-items: center;
  color: var(--text-muted);
  border: 1px solid var(--glass-border);
  border-radius: 50%;
  background: rgba(0, 0, 0, 0.2);
}
.skip-button:hover { color: var(--text-primary); border-color: var(--border-color); background: rgba(255, 255, 255, 0.05); }
.skip-button:disabled { opacity: 0.45; cursor: wait; }

.celebration-content { position: relative; z-index: 2; display: flex; align-items: center; flex-direction: column; text-align: center; }
.unlock-kicker { display: inline-flex; align-items: center; gap: 8px; color: var(--rarity); font-size: 11px; font-weight: 900; letter-spacing: 2.5px; text-transform: uppercase; animation: content-rise 0.45s ease 0.2s both; }
.unlock-icon-stage { position: relative; width: 148px; height: 148px; display: grid; place-items: center; margin: 18px 0 10px; }
.icon-orbit { position: absolute; inset: 8px; border: 1px solid color-mix(in srgb, var(--rarity) 36%, transparent); border-radius: 50%; animation: orbit-pulse 1.9s ease-in-out infinite; }
.icon-orbit-two { inset: 0; border-style: dashed; opacity: 0.5; animation-delay: -0.8s; animation-direction: reverse; }
.unlock-icon { position: relative; width: 96px; height: 96px; display: grid; place-items: center; color: var(--rarity); border: 2px solid color-mix(in srgb, var(--rarity) 70%, #fff); border-radius: 25px; background: linear-gradient(145deg, color-mix(in srgb, var(--rarity) 18%, #302d36), #1d1b22); box-shadow: 0 0 35px color-mix(in srgb, var(--rarity) 30%, transparent), inset 0 1px 0 rgba(255, 255, 255, 0.12); animation: icon-land 0.75s var(--ease-spring) 0.12s both; }
.unlock-check { position: absolute; right: -7px; bottom: -7px; width: 30px; height: 30px; display: grid; place-items: center; color: #102417; border: 3px solid #1d1b22; border-radius: 50%; background: var(--accent-green); }
.rarity-label { color: var(--rarity); font-size: 10px; font-weight: 900; letter-spacing: 3px; text-transform: uppercase; animation: content-rise 0.4s ease 0.35s both; }
.celebration-content h2 { max-width: 430px; margin: 5px 0 7px; color: var(--text-primary); font-size: clamp(25px, 6vw, 35px); font-weight: 950; line-height: 1.12; text-shadow: 0 0 18px color-mix(in srgb, var(--rarity) 24%, transparent); animation: content-rise 0.45s ease 0.42s both; }
.celebration-content > p { max-width: 410px; color: var(--text-muted); font-size: 12px; line-height: 1.6; animation: content-rise 0.45s ease 0.5s both; }

.unlock-characters { display: flex; flex-wrap: wrap; justify-content: center; gap: 7px; margin-top: 14px; animation: content-rise 0.45s ease 0.56s both; }
.unlock-character { display: inline-flex; align-items: center; gap: 7px; padding: 3px 10px 3px 3px; color: var(--text-secondary); border: 1px solid var(--glass-border); border-radius: 18px; background: rgba(255, 255, 255, 0.04); }
.unlock-avatar { width: 29px; height: 29px; display: grid; place-items: center; overflow: hidden; color: var(--rarity); border-radius: 50%; background: var(--bg-inset); font-size: 11px; font-weight: 900; }
.unlock-avatar img { width: 100%; height: 100%; object-fit: cover; }
.unlock-character strong { font-size: 10px; font-weight: 800; }

.unlock-reward-panel { display: flex; align-items: center; justify-content: center; flex-wrap: wrap; gap: 8px; width: 100%; margin-top: 17px; padding: 11px; border: 1px solid color-mix(in srgb, var(--rarity) 18%, var(--glass-border)); border-radius: 12px; background: rgba(0, 0, 0, 0.18); animation: reward-arrive 0.55s var(--ease-spring) 0.62s both; }
.reward-heading { width: 100%; color: var(--text-dim); font-size: 8px; font-weight: 850; letter-spacing: 1.5px; text-transform: uppercase; }
.unlock-reward { display: inline-flex; align-items: center; gap: 6px; min-height: 32px; padding: 5px 10px; border-radius: 9px; }
.unlock-reward img { width: 22px; height: 22px; object-fit: contain; }
.unlock-reward strong { font: 900 17px/1 var(--font-mono); }
.unlock-reward span { font-size: 9px; font-weight: 800; text-transform: uppercase; }
.reward-zbs { color: var(--accent-green); background: rgba(63, 167, 61, 0.1); }
.reward-box { color: var(--accent-purple); background: rgba(180, 150, 255, 0.1); }

.celebration-progress { width: 100%; display: flex; align-items: center; justify-content: center; gap: 4px; margin-top: 16px; }
.celebration-progress > span { width: 19px; max-width: 42px; height: 3px; border-radius: 2px; background: rgba(255, 255, 255, 0.08); }
.celebration-progress > span.active { background: var(--rarity); box-shadow: 0 0 7px color-mix(in srgb, var(--rarity) 50%, transparent); }
.celebration-progress strong { margin-left: 5px; color: var(--text-dim); font: 700 9px/1 var(--font-mono); }
.acknowledge-feedback { width: 100%; min-height: 42px; display: flex; align-items: center; justify-content: center; gap: 8px; margin-top: 13px; padding: 8px 10px; border: 1px solid var(--glass-border); border-radius: 10px; font-size: 9px; font-weight: 750; }
.acknowledge-feedback.saving { color: var(--rarity); background: color-mix(in srgb, var(--rarity) 7%, transparent); }
.acknowledge-feedback.saving svg { animation: acknowledge-spin 0.85s linear infinite; }
.acknowledge-feedback.error { align-items: flex-start; color: var(--accent-red); border-color: rgba(239, 128, 128, 0.22); background: rgba(239, 128, 128, 0.07); text-align: left; }
.acknowledge-feedback.error > span { min-width: 0; display: flex; flex-direction: column; gap: 2px; }
.acknowledge-feedback.error strong { color: var(--text-secondary); font-size: 9px; font-weight: 800; }
.acknowledge-feedback.error small { overflow-wrap: anywhere; color: var(--text-dim); font-size: 8px; line-height: 1.4; }
.celebration-actions { display: flex; justify-content: center; gap: 8px; width: 100%; margin-top: 17px; animation: content-rise 0.45s ease 0.72s both; }
.celebration-actions .btn { min-height: 42px; }
.celebration-next { min-width: 190px; color: #17151b; background: linear-gradient(135deg, color-mix(in srgb, var(--rarity) 78%, white), var(--rarity)); box-shadow: 0 7px 22px color-mix(in srgb, var(--rarity) 20%, transparent); }
.celebration-next:hover { filter: brightness(1.08); transform: translateY(-1px); }

.celebration-particles { position: absolute; z-index: 1; top: 105px; left: 50%; width: 1px; height: 1px; pointer-events: none; }
.celebration-particles i { --particle-angle: 0deg; --particle-distance: 120px; --particle-delay: 0ms; --particle-size: 5px; position: absolute; width: var(--particle-size); height: var(--particle-size); border-radius: 50%; background: var(--rarity); box-shadow: 0 0 8px var(--rarity); animation: particle-burst 1.1s ease-out var(--particle-delay) both; }

@keyframes celebration-card-in { from { opacity: 0; transform: scale(0.72) translateY(26px); } 65% { transform: scale(1.025) translateY(-3px); } to { opacity: 1; transform: scale(1) translateY(0); } }
@keyframes icon-land { from { opacity: 0; transform: scale(0.25) rotate(-18deg); } 65% { transform: scale(1.12) rotate(3deg); } to { opacity: 1; transform: scale(1) rotate(0); } }
@keyframes content-rise { from { opacity: 0; transform: translateY(10px); } to { opacity: 1; transform: translateY(0); } }
@keyframes reward-arrive { from { opacity: 0; transform: scale(0.82); } to { opacity: 1; transform: scale(1); } }
@keyframes orbit-pulse { 0%, 100% { transform: scale(0.92) rotate(0); opacity: 0.25; } 50% { transform: scale(1.06) rotate(12deg); opacity: 0.7; } }
@keyframes aurora-breathe { 0%, 100% { opacity: 0.5; transform: scale(0.9); } 50% { opacity: 0.9; transform: scale(1.08); } }
@keyframes rays-turn { to { transform: translateX(-50%) rotate(360deg); } }
@keyframes particle-burst { from { opacity: 1; transform: rotate(var(--particle-angle)) translateX(30px) scale(1); } to { opacity: 0; transform: rotate(var(--particle-angle)) translateX(var(--particle-distance)) scale(0); } }
@keyframes acknowledge-spin { to { transform: rotate(360deg); } }

.achievement-celebration-enter-active { transition: opacity 0.3s ease; }
.achievement-celebration-leave-active { transition: opacity 0.25s ease; }
.achievement-celebration-enter-from,
.achievement-celebration-leave-to { opacity: 0; }

@media (max-width: 560px) {
  .celebration-overlay { align-items: flex-end; padding: 8px; }
  .celebration-card { width: 100%; max-height: calc(100svh - 8px); padding: 28px 18px 20px; border-radius: 20px 20px 12px 12px; }
  .unlock-icon-stage { width: 125px; height: 125px; margin-top: 12px; }
  .unlock-icon { width: 84px; height: 84px; }
  .celebration-content h2 { font-size: 27px; }
  .celebration-actions { align-items: stretch; flex-direction: column-reverse; }
  .celebration-actions .btn { min-height: 46px; width: 100%; }
}

@media (prefers-reduced-motion: reduce) {
  .celebration-card,
  .celebration-aurora,
  .celebration-rays,
  .unlock-kicker,
  .unlock-icon,
  .rarity-label,
  .celebration-content h2,
  .celebration-content > p,
  .unlock-characters,
  .unlock-reward-panel,
  .celebration-actions,
  .icon-orbit,
  .celebration-particles i { animation: none; }
  .acknowledge-feedback.saving svg { animation: none; }
  .celebration-particles { display: none; }
  .achievement-celebration-enter-active,
  .achievement-celebration-leave-active { transition: none; }
}
</style>

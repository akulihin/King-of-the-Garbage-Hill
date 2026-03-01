<script setup lang="ts">
import { ref, watch, onUnmounted } from 'vue'

export interface ToastEvent {
  id: number
  avatar: string
  text: string
  color: string
  characterName: string
}

const props = defineProps<{
  globalLogs: string
  players: { character: { name: string; avatar: string; avatarCurrent?: string } }[]
}>()

const toasts = ref<ToastEvent[]>([])
let toastId = 0
let lastLogSnapshot = ''
let initialized = false

/** Character accent colors for toast left border */
const charColors: Record<string, string> = {
  'Глеб': '#9b59b6',
  'DeepList': '#e74c3c',
  'Рик': '#2ecc71',
  'Тигр': '#e67e22',
  'Сайтама': '#f1c40f',
  'Кратос': '#c0392b',
  'mylorik': '#e74c3c',
  'Акула': '#3498db',
  'Продавец': '#f39c12',
  'Кира': '#8e44ad',
  'LeCrisp': '#1abc9c',
  'Дракон': '#e67e22',
  'Котики': '#e91e63',
  'HardKitty': '#ff69b4',
  'Стая Гоблинов': '#27ae60',
  'Weedwick': '#2ecc71',
  'Братишка': '#3498db',
  'Spartan': '#c0392b',
  'Vampire': '#9b59b6',
  'Darksci': '#34495e',
  'Баг': '#00ff41',
  'Дезморалист': '#e74c3c',
  'Допа': '#f39c12',
  'Geralt': '#95a5a6',
  'Shen': '#e67e22',
  'Йохан': '#34495e',
  'Mitsuki': '#9b59b6',
}

/** Key phrases in global logs mapped to toast-worthy events.
 *  These must match the EXACT Russian text from the C# backend's AddGlobalLogs calls. */
const eventTriggers: { pattern: RegExp; getText: (match: RegExpMatchArray) => string; char?: string }[] = [
  // Saitama one-punch
  { pattern: /ONE PUUUUUUNCH/i, getText: () => 'ONE PUUUUUUNCH!!!', char: 'Сайтама' },
  // Saitama despair / rage
  { pattern: /у меня горит!/i, getText: () => 'Saitama is on fire!', char: 'Сайтама' },
  { pattern: /Нахуй эту игру/i, getText: () => 'Saitama rage quit!', char: 'Сайтама' },
  // Kratos / Itachi resurrection
  { pattern: /вернулся к жизни/i, getText: () => 'Back from the dead!' },
  { pattern: /Изанаги/i, getText: () => 'Izanagi! Resurrection!' },
  // Kill events
  { pattern: /\*\*УБИЛ\*\*/, getText: () => 'KILL!' },
  // Kira end-game events
  { pattern: /Теперь я\.\.\. Бог/i, getText: () => 'Kira became God!', char: 'Кира' },
  { pattern: /записал себя в тетрадь/i, getText: () => 'Kira wrote his own name...', char: 'Кира' },
  // Trolling events
  { pattern: /Произошел Троллинг.*Спящее/i, getText: () => 'Z-z-z... Gleb got trolled in his sleep', char: 'Глеб' },
  { pattern: /Произошел Троллинг.*Молодой Глеб/i, getText: () => 'Young Gleb got trolled!', char: 'Глеб' },
  { pattern: /Произошел Троллинг.*Тигр/i, getText: () => 'Tiger got trolled!', char: 'Тигр' },
  { pattern: /Произошел Троллинг.*Лист/i, getText: () => 'DeepList got trolled, heh', char: 'DeepList' },
  { pattern: /Произошел Троллинг.*Лорик/i, getText: () => 'mylorik got trolled, MMM!', char: 'mylorik' },
  { pattern: /Произошел Троллинг.*Сайтама/i, getText: () => 'Saitama got one-punch trolled!', char: 'Сайтама' },
  { pattern: /Произошел Троллинг.*Рик/i, getText: () => 'Rick got trolled! Wubba Lubba...', char: 'Рик' },
  { pattern: /Произошел Троллинг.*Кира/i, getText: () => 'Kira wrote himself in the notebook...', char: 'Кира' },
  { pattern: /Произошел Троллинг/i, getText: () => 'Someone got trolled!' },
  // DragonSlayer kills dragon
  { pattern: /Я DRAGONSLAYER/i, getText: () => 'DRAGONSLAYER! Dragon is dead!', char: 'Дракон' },
  { pattern: /Дракон под защитой/i, getText: () => 'The Dragon is protected!' },
  // HardKitty win
  { pattern: /HarDKitty больше не одинок/i, getText: () => 'HardKitty is no longer alone!', char: 'HardKitty' },
  // Goblin Ziggurat
  { pattern: /Зиккурат/i, getText: () => 'Goblins built a Ziggurat!', char: 'Стая Гоблинов' },
  // Johan / Monster
  { pattern: /Пейзаж конца света/i, getText: () => 'The Landscape of the End...', char: 'Йохан' },
  { pattern: /у Монстра нет имени/i, getText: () => 'The Monster has no name...' },
  // Mitsuki stealing
  { pattern: /Mitsuki отнял/i, getText: () => 'Mitsuki stole points!' },
  // History rewrite
  { pattern: /переписал историю/i, getText: () => 'History has been rewritten!' },
  // Sparta / Kratos charge
  { pattern: /познают войну/i, getText: () => 'They shall know WAR!' },
  // Kratos passage
  { pattern: /идёт Кратос/i, getText: () => 'Kratos is coming! Run!', char: 'Кратос' },
  // OPEN MID
  { pattern: /OPEN MID/i, getText: () => 'OPEN MID! +20 points!' },
  // All gods dead
  { pattern: /коробка Пандоры/i, getText: () => 'Pandora\'s Box has been opened...' },
  // All players left
  { pattern: /Все игроки покинули/i, getText: () => 'All players left the game.' },
  // Game psyche rage
  { pattern: /психанул/i, getText: () => 'Rage quit!' },
  // Ничья (draw)
  { pattern: /\*\*Ничья\*\*/i, getText: () => 'Draw!' },
  // Witcher death
  { pattern: /подняли.*на вилы|Ведьмак мёртв/i, getText: () => 'The Witcher has fallen!', char: 'Geralt' },
  // Spartan end quote
  { pattern: /умер как.*Воин.*вернулся как.*Бог/i, getText: () => 'Died a Warrior, returned a God, ended a King!' },
]

function findCharAvatar(charName: string): string {
  const player = props.players.find(p => p.character.name === charName)
  return player?.character.avatarCurrent || player?.character.avatar || '/art/avatars/guess.png'
}

function findCharInLine(line: string): string {
  for (const name of Object.keys(charColors)) {
    if (line.includes(name)) return name
  }
  return ''
}

function pushToast(avatar: string, text: string, color: string, characterName: string) {
  const id = ++toastId
  toasts.value.push({ id, avatar, text, color, characterName })
  // Auto-dismiss after 4s
  setTimeout(() => {
    toasts.value = toasts.value.filter(t => t.id !== id)
  }, 4000)
  // Cap at 5 visible toasts
  if (toasts.value.length > 5) {
    toasts.value = toasts.value.slice(-5)
  }
}

watch(() => props.globalLogs, (newLogs) => {
  if (!newLogs || newLogs === lastLogSnapshot) return

  // On first load, just snapshot the current state — don't spam toasts for existing logs
  if (!initialized) {
    lastLogSnapshot = newLogs
    initialized = true
    return
  }

  // Only process new lines
  const newPart = lastLogSnapshot ? newLogs.slice(lastLogSnapshot.length) : newLogs
  lastLogSnapshot = newLogs

  if (!newPart.trim()) return

  const lines = newPart.split('\n').filter(l => l.trim())
  for (const line of lines) {
    // Skip round headers and fight result lines (not interesting events)
    if (/^__\*\*Раунд #\d+\*\*__/.test(line)) continue
    if (/⟶|→/.test(line) && /<:war:/.test(line)) continue
    for (const trigger of eventTriggers) {
      const match = line.match(trigger.pattern)
      if (match) {
        // Use the trigger's explicit character, or try to find one in the line
        const charName = trigger.char || findCharInLine(line)
        const avatar = charName ? findCharAvatar(charName) : '/art/avatars/guess.png'
        const color = charColors[charName] || '#f0c850'
        pushToast(avatar, trigger.getText(match), color, charName)
        break // Only one toast per line
      }
    }
  }
})

function dismissToast(id: number) {
  toasts.value = toasts.value.filter(t => t.id !== id)
}

onUnmounted(() => {
  toasts.value = []
})
</script>

<template>
  <Teleport to="body">
    <TransitionGroup name="toast" tag="div" class="event-toasts">
      <div
        v-for="toast in toasts"
        :key="toast.id"
        class="event-toast"
        :style="{ '--toast-color': toast.color }"
        @click="dismissToast(toast.id)"
      >
        <div class="toast-accent" />
        <img :src="toast.avatar" :alt="toast.characterName" class="toast-avatar" />
        <div class="toast-body">
          <span v-if="toast.characterName" class="toast-char">{{ toast.characterName }}</span>
          <span class="toast-text">{{ toast.text }}</span>
        </div>
      </div>
    </TransitionGroup>
  </Teleport>
</template>

<style scoped>
.event-toasts {
  position: fixed;
  top: 56px;
  right: 16px;
  z-index: 9000;
  display: flex;
  flex-direction: column;
  gap: 6px;
  pointer-events: none;
  max-width: 320px;
}

.event-toast {
  display: flex;
  align-items: center;
  gap: 8px;
  padding: 8px 12px;
  background: var(--glass-bg-heavy, rgba(26, 24, 32, 0.92));
  backdrop-filter: blur(12px);
  -webkit-backdrop-filter: blur(12px);
  border: 1px solid var(--glass-border, rgba(255,255,255,0.06));
  border-radius: var(--radius-lg, 10px);
  box-shadow: 0 4px 20px rgba(0, 0, 0, 0.5);
  cursor: pointer;
  pointer-events: auto;
  position: relative;
  overflow: hidden;
}

.toast-accent {
  position: absolute;
  left: 0;
  top: 0;
  bottom: 0;
  width: 3px;
  background: var(--toast-color, #f0c850);
  border-radius: 3px 0 0 3px;
  box-shadow: 0 0 8px var(--toast-color, #f0c850);
}

.toast-avatar {
  width: 28px;
  height: 28px;
  border-radius: 50%;
  object-fit: cover;
  border: 1.5px solid var(--toast-color, #f0c850);
  flex-shrink: 0;
}

.toast-body {
  display: flex;
  flex-direction: column;
  gap: 1px;
  min-width: 0;
}

.toast-char {
  font-size: 9px;
  font-weight: 800;
  color: var(--toast-color, #f0c850);
  text-transform: uppercase;
  letter-spacing: 0.5px;
  opacity: 0.8;
}

.toast-text {
  font-size: 12px;
  font-weight: 600;
  color: var(--text-primary, #f3f3ea);
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}

/* Transition animations */
.toast-enter-active {
  transition: all 0.35s cubic-bezier(0.34, 1.56, 0.64, 1);
}
.toast-leave-active {
  transition: all 0.25s ease-in;
}
.toast-enter-from {
  opacity: 0;
  transform: translateX(80px) scale(0.8);
}
.toast-leave-to {
  opacity: 0;
  transform: translateX(60px) scale(0.9);
}
.toast-move {
  transition: transform 0.3s ease;
}

/* Mobile responsive */
@media (max-width: 768px) {
  .event-toasts {
    right: 8px;
    left: 8px;
    max-width: none;
  }
  .event-toast {
    padding: 10px 14px;
  }
  .toast-text {
    white-space: normal;
  }
}
</style>

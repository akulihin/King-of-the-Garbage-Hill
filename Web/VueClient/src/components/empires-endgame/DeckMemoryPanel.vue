<script setup lang="ts">
import { nextTick, ref, watch } from 'vue'
import { Brain, RotateCcw, X } from 'lucide-vue-next'
import type { EmpiresDeckMemoryCard } from '../../features/empires-endgame/types'

const props = defineProps<{
  open: boolean
  cards: readonly EmpiresDeckMemoryCard[]
  remainingInspections: number | null
}>()

const emit = defineEmits<{ close: [] }>()
const closeButton = ref<HTMLButtonElement | null>(null)

watch(() => props.open, async (open) => {
  if (!open) return
  await nextTick()
  closeButton.value?.focus()
}, { immediate: true })

function suitLabel(suit: EmpiresDeckMemoryCard['suit']): string {
  return ({ clubs: '♣', diamonds: '♦', hearts: '♥', spades: '♠', joker: '★' } as const)[suit]
}

function rankLabel(rank: EmpiresDeckMemoryCard['rank']): string {
  return ({ jack: 'J', queen: 'Q', king: 'K', ace: 'A', joker: 'Шут' } as Record<string, string>)[rank] ?? rank
}
</script>

<template>
  <div v-if="open" class="memory-backdrop" data-testid="deck-memory-panel" @click.self="emit('close')">
    <section
      class="memory-panel"
      role="dialog"
      aria-modal="true"
      aria-labelledby="deck-memory-title"
      aria-describedby="deck-memory-description"
      @keydown.esc="emit('close')"
    >
      <header>
        <span class="memory-sigil" aria-hidden="true"><Brain :size="22" /></span>
        <div>
          <span>Феноменальная память Тома</span>
          <h2 id="deck-memory-title">Порядок оставшейся колоды</h2>
        </div>
        <button ref="closeButton" type="button" aria-label="Закрыть память колоды" @click="emit('close')">
          <X :size="18" />
        </button>
      </header>

      <p id="deck-memory-description">
        Позиция 1 будет добрана следующей. Список повторяет фактический порядок колоды и не меняет его.
      </p>
      <p v-if="remainingInspections !== null" class="memory-limit">
        После открытия осталось проверок в этом коне: <b>{{ remainingInspections }}</b>
      </p>

      <ol class="memory-list" aria-label="Карты в порядке следующего добора">
        <li v-for="card in cards" :key="card.instanceId" :class="{ inverted: card.inverted }">
          <span class="position">{{ card.position }}</span>
          <span class="identity"><b>{{ suitLabel(card.suit) }} {{ rankLabel(card.rank) }}</b>{{ card.name }}</span>
          <span class="orientation"><RotateCcw v-if="card.inverted" :size="14" />{{ card.inverted ? 'Перевёрнута' : 'Прямая' }}</span>
        </li>
      </ol>
    </section>
  </div>
</template>

<style scoped>
.memory-backdrop { position:fixed; z-index:80; inset:0; display:grid; place-items:center; padding:18px; background:rgba(4,8,8,.78); backdrop-filter:blur(7px); }
.memory-panel { width:min(720px,100%); max-height:min(760px,90vh); overflow:hidden; border:1px solid rgba(204,180,117,.35); border-radius:16px; color:#efe4cd; background:#151a17; box-shadow:0 30px 100px rgba(0,0,0,.6); }
header { display:grid; grid-template-columns:auto 1fr auto; align-items:center; gap:12px; padding:18px; border-bottom:1px solid rgba(211,186,127,.14); background:linear-gradient(100deg,#20251d,#151b1d); }
.memory-sigil { display:grid; width:42px; height:42px; place-items:center; border:1px solid rgba(217,190,120,.34); border-radius:50%; color:#dec67e; background:rgba(211,181,105,.08); }
header span { color:#9e8a5e; font:800 .56rem/1 monospace; letter-spacing:.1em; text-transform:uppercase; }
h2 { margin:5px 0 0; font:700 1.45rem/1 Georgia,serif; }
header button { display:grid; width:36px; height:36px; place-items:center; border:1px solid rgba(222,202,155,.22); border-radius:7px; color:#e8dcc4; background:#202620; cursor:pointer; }
.memory-panel > p { margin:14px 18px 0; color:rgba(239,228,205,.62); font-size:.72rem; line-height:1.5; }
.memory-limit b { color:#dec67e; }
.memory-list { max-height:58vh; margin:14px 0 0; padding:0 18px 18px; overflow:auto; list-style:none; }
.memory-list li { display:grid; grid-template-columns:34px minmax(0,1fr) auto; align-items:center; gap:10px; min-height:50px; padding:7px 10px; border-bottom:1px solid rgba(221,199,148,.1); }
.memory-list li.inverted { color:#e4c9d3; background:linear-gradient(90deg,rgba(116,46,72,.18),transparent); }
.position { display:grid; width:28px; height:28px; place-items:center; border-radius:50%; color:#242117; background:#d0b568; font:900 .66rem/1 monospace; }
.identity { display:grid; gap:3px; font-size:.68rem; }
.identity b { color:#f0ddaa; font-size:.77rem; }
.orientation { display:inline-flex; align-items:center; gap:5px; color:rgba(239,228,205,.52); font:700 .57rem/1 monospace; }
button:focus-visible { outline:2px solid #eed381; outline-offset:3px; }
@media (max-width:560px) { .memory-list li { grid-template-columns:30px 1fr; }.orientation { grid-column:2; }.memory-panel { max-height:94vh; } }
</style>

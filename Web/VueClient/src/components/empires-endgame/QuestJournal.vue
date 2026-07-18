<script setup lang="ts">
import { CheckCircle2, CircleDashed, PauseCircle, ScrollText, XCircle } from 'lucide-vue-next'
import { nextTick, onBeforeUnmount, ref, watch } from 'vue'

export interface QuestJournalEntryView {
  id: string
  name: string
  description: string
  stageName: string
  status: 'active' | 'completed' | 'failed' | 'suspended'
  startedAtCon: number
  finishedAtCon: number | null
  memory: Array<{ label: string, value: string }>
  compatibilityReason?: string
}

const props = defineProps<{
  open: boolean
  entries: QuestJournalEntryView[]
}>()

const emit = defineEmits<{ close: [] }>()
const journal = ref<HTMLElement | null>(null)
let previouslyFocused: HTMLElement | null = null

watch(() => props.open, async (open) => {
  if (!open) {
    previouslyFocused?.focus()
    previouslyFocused = null
    return
  }
  previouslyFocused = document.activeElement instanceof HTMLElement ? document.activeElement : null
  await nextTick()
  journal.value?.focus()
}, { immediate: true })

onBeforeUnmount(() => previouslyFocused?.focus())

function trapFocus(event: KeyboardEvent) {
  const button = journal.value?.querySelector<HTMLElement>('button:not([disabled])')
  if (!button) return
  if (event.shiftKey ? document.activeElement === button : document.activeElement === button) {
    event.preventDefault()
    button.focus()
  }
}

function statusLabel(status: QuestJournalEntryView['status']) {
  if (status === 'active') return 'Активно'
  if (status === 'completed') return 'Выполнено'
  if (status === 'failed') return 'Провалено'
  return 'Приостановлено'
}

function statusCon(entry: QuestJournalEntryView) {
  return entry.status === 'completed' || entry.status === 'failed'
    ? entry.finishedAtCon ?? entry.startedAtCon
    : entry.startedAtCon
}
</script>

<template>
  <Teleport to="body">
    <Transition name="journal">
      <div v-if="open" class="journal-backdrop" @click.self="$emit('close')">
        <aside ref="journal" class="quest-journal" role="dialog" aria-modal="true" aria-labelledby="quest-journal-title" tabindex="-1" @keydown.tab="trapFocus" @keydown.esc="emit('close')">
          <header><ScrollText :size="21" /><div><span>Летопись решений</span><h2 id="quest-journal-title">Журнал заданий</h2></div><button type="button" aria-label="Закрыть журнал" @click="emit('close')">×</button></header>
          <div v-if="entries.length" class="journal-list">
            <article v-for="entry in entries" :key="entry.id" :class="`status-${entry.status}`">
              <span class="status-icon">
                <CircleDashed v-if="entry.status === 'active'" :size="18" />
                <CheckCircle2 v-else-if="entry.status === 'completed'" :size="18" />
                <XCircle v-else-if="entry.status === 'failed'" :size="18" />
                <PauseCircle v-else :size="18" />
              </span>
              <div><span>{{ statusLabel(entry.status) }} · кон {{ statusCon(entry) }}</span><h3>{{ entry.name }}</h3><p>{{ entry.description }}</p><strong>{{ entry.stageName }}</strong>
                <dl v-if="entry.memory.length"><div v-for="memory in entry.memory" :key="memory.label"><dt>{{ memory.label }}</dt><dd>{{ memory.value }}</dd></div></dl>
                <em v-if="entry.compatibilityReason">{{ entry.compatibilityReason }}</em>
              </div>
            </article>
          </div>
          <p v-else class="empty">Задания ещё не попадали в летопись.</p>
        </aside>
      </div>
    </Transition>
  </Teleport>
</template>

<style scoped>
.journal-backdrop{position:fixed;z-index:1200;display:flex;inset:0;justify-content:flex-end;background:rgba(4,6,5,.58);backdrop-filter:blur(4px)}.quest-journal{width:min(520px,100%);height:100%;overflow:auto;border-left:1px solid rgba(220,190,121,.25);color:#eee4cf;background:#121610;box-shadow:-24px 0 80px rgba(0,0,0,.55)}header{position:sticky;z-index:1;top:0;display:grid;grid-template-columns:auto 1fr auto;align-items:center;gap:10px;padding:19px;border-bottom:1px solid rgba(220,190,121,.16);background:rgba(18,22,16,.97)}header>svg{color:#d2b367}header span{color:#af965d;font:800 .55rem/1 monospace;text-transform:uppercase}h2{margin:4px 0 0;font:700 1.45rem/1 Georgia,serif}header button{width:34px;height:34px;border:1px solid rgba(220,190,121,.18);border-radius:7px;color:#ddd0b7;background:transparent;cursor:pointer}.journal-list{display:grid;gap:9px;padding:14px}.journal-list article{display:grid;grid-template-columns:auto 1fr;gap:10px;padding:14px;border:1px solid rgba(220,190,121,.12);border-radius:10px;background:rgba(255,255,255,.02)}.status-icon{color:#c6a75f}.status-completed .status-icon{color:#79a47a}.status-failed .status-icon{color:#bb7066}.status-suspended .status-icon{color:#999}.journal-list article>div>span{color:#9c895e;font:800 .54rem/1 monospace;text-transform:uppercase}h3{margin:6px 0 5px;font:700 1.05rem/1 Georgia,serif}.journal-list p{margin:0;color:rgba(238,228,207,.58);font-size:.67rem;line-height:1.45}.journal-list strong{display:block;margin-top:8px;color:#c4aa70;font-size:.62rem}.journal-list dl{display:grid;gap:4px;margin:9px 0 0}.journal-list dl div{display:flex;justify-content:space-between;gap:10px;padding:5px 7px;border-radius:5px;background:rgba(255,255,255,.025);font-size:.59rem}.journal-list dt{color:rgba(238,228,207,.48)}.journal-list dd{margin:0;color:#d2bd8b}.journal-list em{display:block;margin-top:9px;color:#d19689;font-size:.59rem;font-style:normal}.empty{padding:45px 20px;color:rgba(238,228,207,.4);text-align:center}.journal-enter-active,.journal-leave-active{transition:opacity .16s}.journal-enter-active .quest-journal,.journal-leave-active .quest-journal{transition:transform .16s}.journal-enter-from,.journal-leave-to{opacity:0}.journal-enter-from .quest-journal,.journal-leave-to .quest-journal{transform:translateX(100%)}
</style>

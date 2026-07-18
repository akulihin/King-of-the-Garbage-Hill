<script setup lang="ts">
import { nextTick, onBeforeUnmount, ref, watch } from 'vue'
import { CheckCircle2, Clock3, LockKeyhole, ScrollText, Sparkles, X } from 'lucide-vue-next'

export interface DialogueChoiceView {
  id: string
  label: string
  description?: string
  costs: string[]
  effects: string[]
  requirements?: string[]
  target?: string
  disabled?: boolean
  disabledReason?: string
}

const props = defineProps<{
  open: boolean
  questId: string
  title: string
  stageName: string
  speaker: string
  text: string
  imageUrl?: string
  status: 'active' | 'completed' | 'failed' | 'suspended'
  mandatory: boolean
  choices: DialogueChoiceView[]
}>()

const emit = defineEmits<{
  choose: [choiceId: string]
  close: []
}>()

const dialog = ref<HTMLElement | null>(null)
let previouslyFocused: HTMLElement | null = null

watch(() => props.open, async (open) => {
  if (!open) {
    previouslyFocused?.focus()
    previouslyFocused = null
    return
  }
  previouslyFocused = document.activeElement instanceof HTMLElement ? document.activeElement : null
  await nextTick()
  dialog.value?.focus()
}, { immediate: true })

onBeforeUnmount(() => previouslyFocused?.focus())

function canClose() {
  return !props.mandatory || props.status === 'completed' || props.status === 'failed'
}

function close() {
  if (canClose()) emit('close')
}

function choose(choice: DialogueChoiceView) {
  if (!choice.disabled && props.status === 'active') emit('choose', choice.id)
}

function trapFocus(event: KeyboardEvent) {
  if (!dialog.value) return
  const focusable = Array.from(dialog.value.querySelectorAll<HTMLElement>('button:not([disabled])'))
  if (!focusable.length) {
    event.preventDefault()
    dialog.value.focus()
    return
  }
  const first = focusable[0]
  const last = focusable[focusable.length - 1]
  if (event.shiftKey && document.activeElement === first) {
    event.preventDefault()
    last.focus()
  } else if (!event.shiftKey && document.activeElement === last) {
    event.preventDefault()
    first.focus()
  }
}

function keyboardChoice(event: KeyboardEvent) {
  if (props.status !== 'active') return
  const index = Number(event.key) - 1
  const choice = props.choices[index]
  if (Number.isInteger(index) && choice && !choice.disabled) {
    event.preventDefault()
    emit('choose', choice.id)
  }
}
</script>

<template>
  <Teleport to="body">
    <Transition name="dialogue">
      <div v-if="open" class="dialogue-backdrop">
        <section
          ref="dialog"
          class="dialogue-panel"
          role="dialog"
          aria-modal="true"
          aria-labelledby="dialogue-title"
          aria-describedby="dialogue-copy"
          tabindex="-1"
          :data-quest-id="questId"
          @keydown.tab="trapFocus"
          @keydown.esc="close"
          @keydown="keyboardChoice"
        >
          <header>
            <span class="quest-seal"><ScrollText :size="22" /></span>
            <div>
              <span>{{ stageName }}</span>
              <h2 id="dialogue-title">{{ title }}</h2>
            </div>
            <button v-if="canClose()" type="button" aria-label="Закрыть диалог" @click="close"><X :size="18" /></button>
            <span v-else class="mandatory-mark"><LockKeyhole :size="13" /> Обязательный диалог</span>
          </header>

          <article :class="`status-${status}`">
            <img v-if="imageUrl" :src="imageUrl" alt="" />
            <strong>{{ speaker }}</strong>
            <p id="dialogue-copy">{{ text }}</p>
          </article>

          <div v-if="status === 'active'" class="dialogue-choices" role="group" aria-label="Ответы в диалоге">
            <button
              v-for="(choice, index) in choices"
              :key="choice.id"
              type="button"
              :disabled="choice.disabled"
              :data-testid="`dialogue-choice-${choice.id}`"
              @click="choose(choice)"
            >
              <span class="choice-number">{{ index + 1 }}</span>
              <span class="choice-copy">
                <strong>{{ choice.label }}</strong>
                <small v-if="choice.description">{{ choice.description }}</small>
                <span v-if="choice.costs.length"><Clock3 :size="12" /> {{ choice.costs.join(' · ') }}</span>
                <span v-if="choice.requirements?.length"><LockKeyhole :size="12" /> {{ choice.requirements.join(' · ') }}</span>
                <span v-if="choice.target"><ScrollText :size="12" /> {{ choice.target }}</span>
                <span v-if="choice.effects.length"><Sparkles :size="12" /> {{ choice.effects.join(' · ') }}</span>
                <em v-if="choice.disabledReason">{{ choice.disabledReason }}</em>
              </span>
            </button>
          </div>

          <footer v-else>
            <CheckCircle2 :size="18" />
            <span>{{ status === 'completed' ? 'Задание выполнено.' : status === 'failed' ? 'Задание завершено неудачей.' : 'Задание приостановлено для совместимости.' }}</span>
            <button v-if="canClose()" type="button" data-testid="dialogue-dismiss" @click="close">Продолжить</button>
          </footer>
        </section>
      </div>
    </Transition>
  </Teleport>
</template>

<style scoped>
.dialogue-backdrop { position:fixed; z-index:1210; display:grid; inset:0; place-items:center; overflow:auto; padding:22px; background:rgba(4,6,5,.84); backdrop-filter:blur(10px); }
.dialogue-panel { width:min(850px,100%); max-height:calc(100vh - 44px); overflow:auto; border:1px solid rgba(220,190,121,.28); border-radius:16px; color:#eee4cf; background:linear-gradient(155deg,#202219,#11140f 72%); box-shadow:0 34px 110px rgba(0,0,0,.68); }
.dialogue-panel:focus { outline:none; }.dialogue-panel:focus-visible { outline:2px solid #e4c77f; outline-offset:3px; }
header { display:grid; grid-template-columns:auto minmax(0,1fr) auto; align-items:center; gap:12px; padding:18px 20px; border-bottom:1px solid rgba(220,190,121,.15); background:radial-gradient(circle at 80% 0,rgba(204,166,86,.13),transparent 44%); }
.quest-seal { display:grid; width:45px; height:45px; place-items:center; border:1px solid rgba(220,190,121,.3); border-radius:50%; color:#dfc070; }.dialogue-panel header div>span { color:#bda15f; font:800 .58rem/1 monospace; letter-spacing:.1em; text-transform:uppercase; }h2 { margin:5px 0 0; font:700 1.6rem/1 Georgia,serif; }header button { display:grid; width:34px; height:34px; place-items:center; border:1px solid rgba(220,190,121,.2); border-radius:7px; color:#daceb7; background:rgba(255,255,255,.03); cursor:pointer; }.mandatory-mark { display:flex; align-items:center; gap:5px; color:#c7a968; font-size:.59rem; }
article { padding:24px 26px; border-bottom:1px solid rgba(220,190,121,.12); }article>strong { color:#d8b865; font:800 .65rem/1 monospace; letter-spacing:.08em; text-transform:uppercase; }article p { margin:11px 0 0; color:#f0e6d3; font:500 .9rem/1.65 Georgia,serif; white-space:pre-line; }.status-failed { border-left:3px solid #a95e57; }.status-completed { border-left:3px solid #6f9b72; }
article img { float:right; width:min(260px,42%); max-height:180px; margin:0 0 12px 18px; border-radius:9px; object-fit:cover; }
.dialogue-choices { display:grid; gap:8px; padding:18px; }.dialogue-choices>button { display:grid; grid-template-columns:34px minmax(0,1fr); overflow:hidden; padding:0; border:1px solid rgba(220,190,121,.15); border-radius:9px; color:#ece1cb; text-align:left; background:rgba(255,255,255,.025); cursor:pointer; }.dialogue-choices>button:hover:not(:disabled),.dialogue-choices>button:focus-visible { border-color:#c7a85f; background:rgba(199,168,95,.07); }.dialogue-choices>button:disabled { opacity:.48; cursor:not-allowed; }.choice-number { display:grid; place-items:center; border-right:1px solid rgba(220,190,121,.12); color:#d1b46d; font:800 .7rem/1 monospace; }.choice-copy { display:grid; gap:5px; padding:12px; }.choice-copy>strong { font:700 .9rem/1.2 Georgia,serif; }.choice-copy small { color:rgba(238,228,207,.57); }.choice-copy span { display:flex; align-items:center; gap:5px; color:#b9a675; font-size:.6rem; }.choice-copy em { color:#d69a8e; font-size:.59rem; font-style:normal; }
footer { display:flex; align-items:center; gap:8px; padding:18px 20px; color:#bcd2b9; }footer span { margin-right:auto; }footer button { padding:9px 14px; border:1px solid #a98d4e; border-radius:7px; color:#251f14; background:#d1b264; font-weight:800; cursor:pointer; }
.dialogue-enter-active,.dialogue-leave-active { transition:opacity .16s ease; }.dialogue-enter-from,.dialogue-leave-to { opacity:0; }
@media (max-width:600px) { .dialogue-backdrop{padding:7px}.dialogue-panel{max-height:calc(100vh - 14px)}header{padding:14px}.mandatory-mark{font-size:0}.mandatory-mark svg{width:16px;height:16px}article{padding:19px 16px}.dialogue-choices{padding:10px} }
</style>

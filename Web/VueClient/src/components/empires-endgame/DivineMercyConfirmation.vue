<script setup lang="ts">
import { nextTick, ref, watch } from 'vue'
import { RotateCcw, Sparkles } from 'lucide-vue-next'

const props = defineProps<{
  open: boolean
  title: string
  confirmLabel: string
  cancelLabel: string
  cardName: string
}>()

const emit = defineEmits<{ confirm: [], cancel: [] }>()
const cancelButton = ref<HTMLButtonElement | null>(null)

watch(() => props.open, async (open) => {
  if (!open) return
  await nextTick()
  cancelButton.value?.focus()
}, { immediate: true })
</script>

<template>
  <div v-if="open" class="mercy-backdrop" data-testid="divine-mercy-confirmation" @click.self="emit('cancel')">
    <section
      class="mercy-dialog"
      role="alertdialog"
      aria-modal="true"
      aria-labelledby="mercy-title"
      aria-describedby="mercy-description"
      @keydown.esc="emit('cancel')"
    >
      <span class="mercy-sigil" aria-hidden="true"><Sparkles :size="28" /></span>
      <span>Божественная Милость</span>
      <h2 id="mercy-title">{{ title }}</h2>
      <p id="mercy-description">Карта «{{ cardName }}» станет прямой. Милость будет списана только после подтверждения.</p>
      <div class="mercy-actions">
        <button data-testid="confirm-divine-mercy" type="button" class="confirm" @click="emit('confirm')">
          <RotateCcw :size="16" /> {{ confirmLabel }}
        </button>
        <button ref="cancelButton" data-testid="cancel-divine-mercy" type="button" class="cancel" @click="emit('cancel')">
          {{ cancelLabel }}
        </button>
      </div>
    </section>
  </div>
</template>

<style scoped>
.mercy-backdrop { position:fixed; z-index:90; inset:0; display:grid; place-items:center; padding:18px; background:rgba(4,6,5,.82); backdrop-filter:blur(8px); }
.mercy-dialog { display:grid; width:min(570px,100%); justify-items:center; padding:30px; border:1px solid rgba(220,188,105,.42); border-radius:18px; color:#f0e4ca; background:radial-gradient(circle at 50% 0,rgba(210,178,91,.16),transparent 36%),#171a16; box-shadow:0 30px 100px rgba(0,0,0,.64); text-align:center; }
.mercy-sigil { display:grid; width:62px; height:62px; place-items:center; margin-bottom:12px; border:1px solid #d2b25f; border-radius:50%; color:#f1d983; background:rgba(210,178,95,.09); box-shadow:0 0 28px rgba(215,184,99,.15); }
.mercy-dialog > span:not(.mercy-sigil) { color:#ae9456; font:900 .56rem/1 monospace; letter-spacing:.13em; text-transform:uppercase; }
h2 { max-width:480px; margin:10px 0; font:700 1.55rem/1.25 Georgia,serif; }
p { margin:0; color:rgba(240,228,202,.58); font-size:.72rem; line-height:1.5; }
.mercy-actions { display:grid; width:100%; gap:8px; margin-top:22px; }
button { min-height:44px; padding:8px 14px; border-radius:8px; cursor:pointer; font-size:.67rem; font-weight:850; }
.confirm { display:inline-flex; align-items:center; justify-content:center; gap:7px; border:1px solid #d1b05c; color:#261f14; background:#d1b05c; }
.cancel { border:1px solid rgba(223,205,163,.25); color:#d9ccb0; background:#20231d; }
button:focus-visible { outline:2px solid #f0d47e; outline-offset:3px; }
</style>

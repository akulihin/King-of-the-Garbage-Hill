<script setup lang="ts">
import { useFocusTrapDialog } from 'src/composables/useFocusTrapDialog'

defineProps<{
  title: string
  message: string
  confirmLabel: string
  cancelLabel: string
}>()

const emit = defineEmits<{
  confirm: []
  cancel: []
}>()

const { overlayRef, dialogRef, trapTabKey } = useFocusTrapDialog()

function onDialogKeydown(event: KeyboardEvent): void {
  if (event.key === 'Escape') {
    event.preventDefault()
    emit('cancel')
    return
  }
  trapTabKey(event)
}
</script>

<template>
  <Teleport to="body">
    <Transition name="bs-confirm" appear>
      <div ref="overlayRef" class="confirm-overlay" @click.self="emit('cancel')">
        <section
          ref="dialogRef"
          class="confirm-dialog"
          role="alertdialog"
          aria-modal="true"
          aria-labelledby="bs-confirm-title"
          aria-describedby="bs-confirm-message"
          tabindex="-1"
          @keydown="onDialogKeydown"
        >
          <h3 id="bs-confirm-title">{{ title }}</h3>
          <p id="bs-confirm-message">{{ message }}</p>
          <div class="confirm-actions">
            <button class="bs-btn" type="button" @click="emit('cancel')">{{ cancelLabel }}</button>
            <button class="bs-btn bs-btn--danger" type="button" @click="emit('confirm')">{{ confirmLabel }}</button>
          </div>
        </section>
      </div>
    </Transition>
  </Teleport>
</template>

<style scoped>
.confirm-overlay {
  position: fixed;
  inset: 0;
  z-index: 3200;
  display: flex;
  align-items: center;
  justify-content: center;
  padding: 18px;
  background: radial-gradient(circle at 50% 42%, rgba(32, 30, 42, 0.6), rgba(4, 4, 7, 0.85) 70%);
  backdrop-filter: blur(8px);
}

.confirm-dialog {
  width: min(380px, 100%);
  padding: 22px 24px 18px;
  border: 1px solid color-mix(in srgb, var(--accent-red) 30%, var(--glass-border));
  border-radius: 16px;
  outline: none;
  background: linear-gradient(155deg, color-mix(in srgb, var(--accent-red) 7%, var(--bg-card)), var(--bg-secondary) 72%);
  box-shadow: 0 22px 70px rgba(0, 0, 0, 0.6), inset 0 1px 0 var(--glass-highlight);
  text-align: center;
}

.confirm-dialog h3 {
  margin: 0 0 6px;
  color: var(--text-primary);
  font-size: 1.05rem;
  font-weight: 900;
}

.confirm-dialog p {
  margin: 0 0 16px;
  color: var(--text-muted);
  font-size: 0.82rem;
  line-height: 1.5;
}

.confirm-actions {
  display: flex;
  justify-content: center;
  gap: 8px;
}

.confirm-actions .bs-btn {
  flex: 1;
  min-height: 42px;
}

.bs-confirm-enter-active { transition: opacity 0.2s ease; }
.bs-confirm-enter-active .confirm-dialog { animation: confirm-in 0.32s var(--ease-spring) both; }
.bs-confirm-leave-active { transition: opacity 0.15s ease; }
.bs-confirm-enter-from,
.bs-confirm-leave-to { opacity: 0; }

@keyframes confirm-in {
  from { opacity: 0; transform: scale(0.85) translateY(12px); }
  to { opacity: 1; transform: scale(1) translateY(0); }
}
</style>

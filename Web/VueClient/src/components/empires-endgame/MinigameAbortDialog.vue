<script setup lang="ts">
import { nextTick, onMounted, ref } from 'vue'
import { useFocusTrapDialog } from '../../composables/useFocusTrapDialog'

const props = defineProps<{
  idPrefix: string
  title: string
  description: string
  confirmLabel: string
  continueLabel: string
  confirmTestId: string
  continueTestId: string
}>()

const emit = defineEmits<{
  confirm: []
  cancel: []
}>()

const continueButton = ref<HTMLButtonElement | null>(null)
const { overlayRef, dialogRef, trapTabKey } = useFocusTrapDialog()

const titleId = `${props.idPrefix}-title`
const descriptionId = `${props.idPrefix}-description`

function onKeydown(event: KeyboardEvent): void {
  if (event.key === 'Escape') {
    event.preventDefault()
    event.stopPropagation()
    emit('cancel')
    return
  }
  trapTabKey(event)
}

onMounted(async () => {
  await nextTick()
  continueButton.value?.focus({ preventScroll: true })
})
</script>

<template>
  <Teleport to="body">
    <div ref="overlayRef" class="minigame-abort" @click.self="emit('cancel')">
      <section
        ref="dialogRef"
        class="minigame-abort__panel"
        role="alertdialog"
        aria-modal="true"
        :aria-labelledby="titleId"
        :aria-describedby="descriptionId"
        tabindex="-1"
        @keydown="onKeydown"
      >
        <h3 :id="titleId">{{ title }}</h3>
        <p :id="descriptionId">{{ description }}</p>
        <div class="minigame-abort__actions">
          <button
            ref="continueButton"
            type="button"
            :data-testid="continueTestId"
            @click="emit('cancel')"
          >
            {{ continueLabel }}
          </button>
          <button
            class="minigame-abort__danger"
            type="button"
            :data-testid="confirmTestId"
            @click="emit('confirm')"
          >
            {{ confirmLabel }}
          </button>
        </div>
      </section>
    </div>
  </Teleport>
</template>

<style scoped>
.minigame-abort {
  position: fixed;
  inset: 0;
  z-index: 3200;
  display: grid;
  place-items: center;
  padding: 20px;
  background: rgba(0, 0, 0, .74);
}

.minigame-abort__panel {
  display: grid;
  gap: 12px;
  width: min(450px, 100%);
  padding: 22px;
  border: 1px solid #d6725d;
  outline: none;
  background: #1c211b;
  color: #f5eadf;
  box-shadow: 0 22px 70px rgba(0, 0, 0, .62);
}

.minigame-abort__panel h3,
.minigame-abort__panel p {
  margin: 0;
}

.minigame-abort__panel p {
  color: rgba(245, 234, 223, .75);
  line-height: 1.5;
}

.minigame-abort__actions {
  display: flex;
  flex-wrap: wrap;
  gap: 8px;
}

.minigame-abort__actions button {
  flex: 1 1 170px;
  min-height: 42px;
  padding: 8px 12px;
  border: 1px solid rgba(224, 184, 99, .55);
  background: rgba(92, 79, 45, .82);
  color: inherit;
  cursor: pointer;
}

.minigame-abort__actions button:focus-visible {
  outline: 2px solid #fff0a8;
  outline-offset: 2px;
}

.minigame-abort__danger {
  border-color: rgba(238, 97, 73, .72) !important;
  background: rgba(105, 29, 21, .78) !important;
}
</style>

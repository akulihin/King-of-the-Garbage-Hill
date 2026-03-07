import { ref, onUnmounted } from 'vue'

export function useTip() {
  const tipText = ref('')
  const tipVisible = ref(false)
  const tipPos = ref({ x: 0, y: 0 })
  let tipTimer: ReturnType<typeof setTimeout> | null = null

  function showTip(e: MouseEvent, text: string) {
    if (!text) return
    if (tipTimer) clearTimeout(tipTimer)
    tipText.value = text
    tipPos.value = { x: e.clientX, y: e.clientY }
    tipTimer = setTimeout(() => { tipVisible.value = true }, 120)
  }

  function moveTip(e: MouseEvent) {
    tipPos.value = { x: e.clientX, y: e.clientY }
  }

  function hideTip() {
    if (tipTimer) clearTimeout(tipTimer)
    tipTimer = null
    tipVisible.value = false
  }

  onUnmounted(() => {
    if (tipTimer) clearTimeout(tipTimer)
  })

  return { tipText, tipVisible, tipPos, showTip, moveTip, hideTip }
}

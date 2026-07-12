import { nextTick, onMounted, onUnmounted, ref, type Ref } from 'vue'

/**
 * Shared modal focus-trap behavior (extracted from LootBox.vue / AchievementPopup.vue):
 * saves and restores the previously focused element, hides body scroll, marks every
 * other body child inert, keeps focus inside the dialog, and traps Tab / Shift+Tab.
 *
 * Usage: call at setup, bind `overlayRef` to the overlay element and `dialogRef` to the
 * dialog element, and call `trapTabKey(event)` from the dialog's keydown handler after
 * any component-specific keys (Escape, …) are handled.
 */
export function useFocusTrapDialog() {
  const overlayRef: Ref<HTMLElement | null> = ref(null)
  const dialogRef: Ref<HTMLElement | null> = ref(null)
  let previousBodyOverflow = ''
  let previouslyFocusedElement: HTMLElement | null = null
  const isolatedBodyChildren: Array<{ element: HTMLElement; wasInert: boolean }> = []

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

  function trapTabKey(event: KeyboardEvent): void {
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

  onMounted(async () => {
    previouslyFocusedElement = document.activeElement instanceof HTMLElement ? document.activeElement : null
    previousBodyOverflow = document.body.style.overflow
    document.body.style.overflow = 'hidden'
    isolateBackground()
    document.addEventListener('focusin', keepFocusInside)
    await nextTick()
    focusFirstControl()
  })

  onUnmounted(() => {
    document.removeEventListener('focusin', keepFocusInside)
    restoreBackground()
    document.body.style.overflow = previousBodyOverflow
    if (previouslyFocusedElement?.isConnected) {
      previouslyFocusedElement.focus({ preventScroll: true })
    }
  })

  return { overlayRef, dialogRef, focusableElements, focusFirstControl, trapTabKey }
}

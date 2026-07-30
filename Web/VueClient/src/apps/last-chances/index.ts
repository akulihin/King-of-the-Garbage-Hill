import StandaloneGameShell from '../../platform/StandaloneGameShell.vue'
import LastChances from '../../pages/LastChances.vue'
import { mountVueApplication } from '../../platform/bootstrap'

export function mount(root: Element): void {
  mountVueApplication(root, StandaloneGameShell, {
    rootProps: { game: LastChances },
    titleKey: 'last-chances.title',
  })
}

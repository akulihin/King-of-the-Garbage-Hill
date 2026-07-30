import StandaloneGameShell from '../../platform/StandaloneGameShell.vue'
import EmpiresEndgame from '../../pages/EmpiresEndgame.vue'
import { mountVueApplication } from '../../platform/bootstrap'

export function mount(root: Element): void {
  mountVueApplication(root, StandaloneGameShell, {
    rootProps: { game: EmpiresEndgame },
    titleKey: 'empires-endgame.title',
  })
}

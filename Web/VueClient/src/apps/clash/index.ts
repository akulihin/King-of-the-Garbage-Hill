import App from '../../App.vue'
import { mountVueApplication } from '../../platform/bootstrap'
import { clashRouter } from './router'

export function mount(root: Element): void {
  mountVueApplication(root, App, {
    router: clashRouter,
    rootProps: { productId: 'clash' },
    titleKey: 'clash.title',
  })
}

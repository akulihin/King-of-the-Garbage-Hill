import App from '../../App.vue'
import { router } from '../../router'
import { mountVueApplication } from '../../platform/bootstrap'

export function mount(root: Element): void {
  mountVueApplication(root, App, {
    router,
    rootProps: { productId: 'kotgh' },
    titleKey: 'kotgh.title',
  })
}

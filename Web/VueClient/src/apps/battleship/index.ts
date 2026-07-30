import App from '../../App.vue'
import { mountVueApplication } from '../../platform/bootstrap'
import { battleshipRouter } from './router'

export function mount(root: Element): void {
  mountVueApplication(root, App, {
    router: battleshipRouter,
    rootProps: { productId: 'battleship' },
    titleKey: 'battleship.title',
  })
}

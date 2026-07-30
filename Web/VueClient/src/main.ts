import './assets/main.css'
import { resolveProductApplication } from './apps/registry'

const appRoot = document.getElementById('app')
if (!appRoot)
  throw new Error('Application root #app is missing.')

void resolveProductApplication(window.location.pathname)
  .then(application => application.mount(appRoot))
  .catch((error) => {
    console.error('[app-host] Failed to mount product application:', error)
    appRoot.textContent = 'The application could not be loaded.'
  })

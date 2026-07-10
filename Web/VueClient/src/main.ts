import './assets/main.css'
import { createApp } from 'vue'
import { createPinia } from 'pinia'
import App from './App.vue'
import { router } from './router'
import { installDomLocalization } from './i18n'

const app = createApp(App)

app.use(createPinia())
app.use(router)

app.mount('#app')

const appRoot = document.getElementById('app')
if (appRoot) installDomLocalization(appRoot)

import type { Component } from 'vue'
import { createApp, watch } from 'vue'
import { createPinia } from 'pinia'
import type { Router } from 'vue-router'
import { installDomLocalization } from '../i18n'
import { currentLocale, message } from './localization'

type ApplicationMountOptions = Readonly<{
  router?: Router
  rootProps?: Record<string, unknown>
  titleKey: string
}>

export function mountVueApplication(
  root: Element,
  rootComponent: Component,
  options: ApplicationMountOptions,
): void {
  const updateTitle = () => {
    document.title = message(options.titleKey)
  }
  updateTitle()
  watch(currentLocale, updateTitle)
  const app = createApp(rootComponent, options.rootProps)
  app.use(createPinia())
  if (options.router) app.use(options.router)
  app.mount(root)
  installDomLocalization(root)
}

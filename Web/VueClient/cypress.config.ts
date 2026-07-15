import { defineConfig } from 'cypress'

const processEnv = (globalThis as typeof globalThis & {
  process?: { env?: Record<string, string | undefined> }
}).process?.env

export default defineConfig({
  e2e: {
    baseUrl: processEnv?.CYPRESS_BASE_URL ?? 'http://127.0.0.1:4174',
    supportFile: false,
    specPattern: 'cypress/e2e/**/*.cy.ts',
  },
  defaultCommandTimeout: 12_000,
  pageLoadTimeout: 30_000,
  requestTimeout: 12_000,
  responseTimeout: 12_000,
  viewportWidth: 1440,
  viewportHeight: 1000,
  video: false,
  screenshotOnRunFailure: true,
  retries: {
    runMode: 1,
    openMode: 0,
  },
})

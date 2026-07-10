import { mkdirSync, writeFileSync } from 'node:fs'
import { resolve } from 'node:path'

const outputDir = resolve(process.env.CYPRESS_outputDir || '/tmp/kotgh-replay-ui')

export default {
  video: false,
  screenshotsFolder: resolve(outputDir, 'screenshots'),
  trashAssetsBeforeRuns: false,
  viewportWidth: Number(process.env.CYPRESS_viewportWidth || 1440),
  viewportHeight: Number(process.env.CYPRESS_viewportHeight || 1000),
  defaultCommandTimeout: 10000,
  requestTimeout: 20000,
  responseTimeout: 60000,
  chromeWebSecurity: false,
  e2e: {
    supportFile: false,
    specPattern: 'scripts/**/*.cy.mjs',
    setupNodeEvents(on) {
      on('task', {
        writeEvidence({ filename, content }) {
          mkdirSync(outputDir, { recursive: true })
          writeFileSync(resolve(outputDir, filename), content, 'utf8')
          return null
        },
      })
    },
  },
}

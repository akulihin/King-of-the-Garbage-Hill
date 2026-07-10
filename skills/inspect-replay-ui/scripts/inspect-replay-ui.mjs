#!/usr/bin/env node

import { existsSync, mkdirSync, readFileSync, readdirSync, writeFileSync } from 'node:fs'
import { homedir } from 'node:os'
import { basename, dirname, resolve } from 'node:path'
import { fileURLToPath } from 'node:url'
import { spawnSync } from 'node:child_process'

const scriptDir = dirname(fileURLToPath(import.meta.url))
const skillDir = resolve(scriptDir, '..')
const repoRoot = resolve(skillDir, '..', '..')
const frontendRoot = process.env.KOTGH_FRONTEND_ROOT || resolve(repoRoot, 'Web', 'VueClient')

const usage = `
Usage:
  node inspect-replay-ui.mjs <replay-url> [options]

Options:
  --action <ACTION>       Repeatable rendered-UI action (see below)
  --output <PATH>         Evidence directory (default: /tmp/kotgh-replay-ui/...)
  --viewport <WxH>        Browser viewport (default: 1440x1000)
  --browser <NAME|PATH>   Cypress browser name/path (default: electron)
  --viewer-id <ID>        Numeric ID used only to pass the app login gate (default: 1)
  --include-json          Save the replay API response as replay.json
  --headed                Show the Electron browser window
  --help                  Show this help

Actions:
  round:N                 Reach round N with the visible previous/next buttons
  next-round | prev-round Click a round button once
  player:N                Click zero-based player N
  player-name:TEXT        Click the player whose rendered button contains TEXT
  tab:TEXT                Click a fight tab (for example: Все бои)
  fight:N                 Click zero-based personal fight thumbnail N
  all-fight:N             Click zero-based row N in Все бои
  play | skip             Start playback or skip the current fight to its end
  speed:1|2|4             Click a playback speed
  click:TEXT              Click a visible button/control by rendered text
  selector:CSS            Click a visible element by CSS selector
  wait:MS                 Wait for UI animation/network settling
  snapshot:NAME           Take an intermediate full-page screenshot
`

let failureOutputDir = ''

function fail(message, details = {}) {
  if (failureOutputDir) {
    mkdirSync(failureOutputDir, { recursive: true })
    writeFileSync(resolve(failureOutputDir, 'failure.json'), JSON.stringify({
      capturedAt: new Date().toISOString(),
      message,
      outputDir: failureOutputDir,
      ...details,
    }, null, 2), 'utf8')
  }
  console.error(`inspect-replay-ui: ${message}`)
  process.exit(2)
}

function parseArgs(argv) {
  const options = {
    actions: [],
    viewport: '1440x1000',
    viewerId: '1',
    includeJson: false,
    headed: false,
    browser: 'electron',
    output: '',
    url: '',
  }

  for (let i = 0; i < argv.length; i += 1) {
    const arg = argv[i]
    if (arg === '--help' || arg === '-h') {
      console.log(usage.trim())
      process.exit(0)
    }
    if (arg === '--action') options.actions.push(argv[++i] ?? fail('--action needs a value'))
    else if (arg === '--output') options.output = argv[++i] ?? fail('--output needs a path')
    else if (arg === '--viewport') options.viewport = argv[++i] ?? fail('--viewport needs WxH')
    else if (arg === '--browser') options.browser = argv[++i] ?? fail('--browser needs a name or executable path')
    else if (arg === '--viewer-id') options.viewerId = argv[++i] ?? fail('--viewer-id needs a value')
    else if (arg === '--include-json') options.includeJson = true
    else if (arg === '--headed') options.headed = true
    else if (arg.startsWith('-')) fail(`unknown option: ${arg}`)
    else if (!options.url) options.url = arg
    else fail(`unexpected argument: ${arg}`)
  }

  if (!options.url) fail('a replay URL is required (use --help for examples)')
  let parsedUrl
  try {
    parsedUrl = new URL(options.url)
  } catch {
    fail(`invalid URL: ${options.url}`)
  }
  if (!/^https?:$/.test(parsedUrl.protocol)) fail('the replay URL must use http or https')
  if (!/\/replay\/[^/]+/.test(parsedUrl.pathname)) fail('URL must point to /replay/<hash>')
  if (!/^\d+$/.test(options.viewerId)) fail('--viewer-id must contain digits only')

  const viewportMatch = options.viewport.match(/^(\d+)x(\d+)$/i)
  if (!viewportMatch) fail('--viewport must look like 1440x1000')
  options.viewportWidth = Number(viewportMatch[1])
  options.viewportHeight = Number(viewportMatch[2])
  if (options.viewportWidth < 320 || options.viewportHeight < 320) fail('viewport dimensions must be at least 320')

  const hash = basename(parsedUrl.pathname).replace(/[^a-zA-Z0-9_-]/g, '') || 'replay'
  const stamp = new Date().toISOString().replace(/[:.]/g, '-')
  options.output = resolve(options.output || `/tmp/kotgh-replay-ui/${hash}-${stamp}`)
  options.url = parsedUrl.toString()
  return options
}

function cypressPackageVersion() {
  try {
    return JSON.parse(readFileSync(resolve(frontendRoot, 'node_modules', 'cypress', 'package.json'), 'utf8')).version || ''
  } catch {
    return ''
  }
}

function findBundledCypress(expectedVersion) {
  if (process.env.CYPRESS_RUN_BINARY && existsSync(process.env.CYPRESS_RUN_BINARY)) {
    return process.env.CYPRESS_RUN_BINARY
  }
  const cacheRoot = resolve(homedir(), '.cache', 'Cypress')
  if (!existsSync(cacheRoot)) return ''
  const expected = resolve(cacheRoot, expectedVersion, 'Cypress', 'Cypress')
  return expectedVersion && existsSync(expected) ? expected : ''
}

function cachedCypressVersions() {
  const cacheRoot = resolve(homedir(), '.cache', 'Cypress')
  if (!existsSync(cacheRoot)) return []
  return readdirSync(cacheRoot)
    .filter(version => existsSync(resolve(cacheRoot, version, 'Cypress', 'Cypress')))
    .sort((a, b) => b.localeCompare(a, undefined, { numeric: true }))
}

function missingSharedLibraries(binary) {
  if (process.platform !== 'linux' || !binary) return []
  const result = spawnSync('ldd', [binary], { encoding: 'utf8' })
  return [...`${result.stdout || ''}\n${result.stderr || ''}`.matchAll(/^\s*(\S+)\s+=>\s+not found\s*$/gm)]
    .map(match => match[1])
}

const options = parseArgs(process.argv.slice(2))
if (!existsSync(resolve(frontendRoot, 'package.json'))) {
  fail(`Vue client not found at ${frontendRoot}; set KOTGH_FRONTEND_ROOT`)
}
mkdirSync(options.output, { recursive: true })
failureOutputDir = options.output
console.log(`Replay UI: ${options.url}`)
console.log(`Evidence:  ${options.output}`)
console.log(`Actions:   ${options.actions.length ? options.actions.join(' -> ') : '(capture initial URL state)'}`)

const expectedCypressVersion = cypressPackageVersion()
if (!expectedCypressVersion) {
  fail(`the Cypress package is not installed under ${frontendRoot}; run pnpm install for Web/VueClient`, { frontendRoot })
}
const cypressBinary = findBundledCypress(expectedCypressVersion)
if (!cypressBinary) {
  const cachedVersions = cachedCypressVersions()
  fail(`Cypress package ${expectedCypressVersion} has no matching browser binary. Install that binary from Web/VueClient with 'pnpm exec cypress install'.`, {
    expectedCypressVersion,
    cachedVersions,
  })
}
const missingLibraries = missingSharedLibraries(cypressBinary)
if (missingLibraries.length) {
  fail(`Cypress cannot start because shared libraries are missing: ${missingLibraries.join(', ')}. Install the Cypress OS prerequisites.`, {
    cypressBinary,
    missingLibraries,
  })
}
const env = {
  ...process.env,
  CYPRESS_replayUrl: options.url,
  CYPRESS_actionsBase64: Buffer.from(JSON.stringify(options.actions), 'utf8').toString('base64'),
  CYPRESS_outputDir: options.output,
  CYPRESS_viewerId: options.viewerId,
  CYPRESS_includeReplayJson: String(options.includeJson),
  CYPRESS_viewportWidth: String(options.viewportWidth),
  CYPRESS_viewportHeight: String(options.viewportHeight),
}
if (cypressBinary) env.CYPRESS_RUN_BINARY = cypressBinary

const args = [
  '--dir', frontendRoot,
  'exec', 'cypress', 'run',
  '--project', skillDir,
  '--config-file', resolve(scriptDir, 'cypress.config.mjs'),
  '--spec', resolve(scriptDir, 'replay-probe.cy.mjs'),
  '--browser', options.browser,
]
if (options.headed) args.push('--headed')

const run = spawnSync('pnpm', args, { env, encoding: 'utf8', maxBuffer: 50 * 1024 * 1024 })
const browserLog = `${run.stdout || ''}${run.stderr || ''}`
writeFileSync(resolve(options.output, 'browser-run.log'), browserLog, 'utf8')
if (run.stdout) process.stdout.write(run.stdout)
if (run.stderr) process.stderr.write(run.stderr)
if (run.error) fail(`could not launch pnpm/Cypress: ${run.error.message}`, { error: run.error.message })
if (run.status !== 0) {
  writeFileSync(resolve(options.output, 'failure.json'), JSON.stringify({
    capturedAt: new Date().toISOString(),
    message: `Replay UI probe failed with exit code ${run.status}`,
    exitCode: run.status,
    signal: run.signal,
    browserLog: 'browser-run.log',
  }, null, 2), 'utf8')
  console.error(`Replay UI probe failed with exit code ${run.status}. Failure evidence is in ${options.output}`)
  process.exit(run.status || 1)
}

console.log(`Replay UI evidence captured in ${options.output}`)

<script setup lang="ts">
import { onMounted, ref } from 'vue'

type Status = 'pending' | 'success' | 'error'

const status = ref<Status>('pending')
const message = ref('Syncing your Discord widget...')

function parseFragment(): Record<string, string> {
  const out: Record<string, string> = {}
  const hash = window.location.hash.startsWith('#') ? window.location.hash.slice(1) : window.location.hash
  for (const pair of hash.split('&')) {
    if (!pair) continue
    const [k, v] = pair.split('=')
    out[decodeURIComponent(k)] = decodeURIComponent(v ?? '')
  }
  return out
}

onMounted(async () => {
  const params = parseFragment()
  const accessToken = params['access_token']
  if (!accessToken) {
    status.value = 'error'
    message.value = 'No access token in URL. Did the OAuth redirect complete?'
    return
  }

  // Remove the access_token from the URL bar / browser history. Fragments stay in
  // shoulder-surf range and indexed history otherwise; we only need it once.
  if (window.history?.replaceState) {
    window.history.replaceState(null, '', window.location.pathname)
  }

  const ctrl = new AbortController()
  const timer = setTimeout(() => ctrl.abort(), 10_000)
  try {
    const res = await fetch('/api/widget/sync', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ accessToken }),
      signal: ctrl.signal,
    })
    if (res.ok) {
      status.value = 'success'
      message.value = 'Discord widget updated. You can close this tab.'
    } else {
      const text = await res.text()
      status.value = 'error'
      message.value = `Sync failed: ${text}`
    }
  } catch (err) {
    status.value = 'error'
    const e = err as Error
    message.value = e.name === 'AbortError'
      ? 'Sync timed out. Please retry.'
      : `Network error: ${e.message}`
  } finally {
    clearTimeout(timer)
  }
})
</script>

<template>
  <div class="widget-page">
    <h1>Discord Widget</h1>
    <p :class="status" role="status" aria-live="polite">{{ message }}</p>
  </div>
</template>

<style scoped>
.widget-page {
  max-width: 480px;
  margin: 4rem auto;
  text-align: center;
  font-family: system-ui, sans-serif;
}
.pending { color: #888; }
.success { color: #2ecc71; }
.error { color: #e74c3c; }
</style>

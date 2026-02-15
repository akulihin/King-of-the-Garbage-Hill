<script setup lang="ts">
import { ref, watch, onBeforeUnmount } from 'vue'
import type { MediaMessage } from 'src/services/signalr'

const props = defineProps<{
  messages: MediaMessage[]
}>()

// ── Audio playback state ──────────────────────────────────────────
// Audio objects are managed IMPERATIVELY (not via <audio> DOM elements)
// so they survive Vue's frequent re-renders from SignalR updates (~300ms).
const volume = ref(0.5)
const audioObjects = new Map<string, HTMLAudioElement>() // keyed by fileUrl
const playingUrl = ref<string | null>(null)

function getOrCreateAudio(url: string, loop: boolean): HTMLAudioElement {
  let audio = audioObjects.get(url)
  if (!audio) {
    audio = new Audio(url)
    audio.preload = 'auto'
    audio.volume = volume.value
    audio.loop = loop
    audio.addEventListener('ended', () => {
      // Only clear playing state if NOT looping (looping handles itself)
      if (!audio!.loop && playingUrl.value === url) {
        playingUrl.value = null
      }
    })
    audio.addEventListener('error', (e) => {
      console.warn('[MediaMessages] Audio error for', url, e)
      if (playingUrl.value === url) {
        playingUrl.value = null
      }
    })
    audioObjects.set(url, audio)
  }
  // Update loop setting in case it changed
  audio.loop = loop
  return audio
}

function togglePlay(url: string, roundsToPlay: number) {
  if (!url) return
  const loop = roundsToPlay > 1
  const audio = getOrCreateAudio(url, loop)

  if (playingUrl.value === url && !audio.paused) {
    audio.pause()
    playingUrl.value = null
  } else {
    // Pause any other playing audio
    audioObjects.forEach((a) => {
      if (!a.paused) a.pause()
    })
    audio.currentTime = 0
    audio.volume = volume.value
    audio.play().catch((err) => {
      console.warn('[MediaMessages] Play failed:', err.message)
    })
    playingUrl.value = url
  }
}

function isPlaying(url: string): boolean {
  return playingUrl.value === url
}

watch(volume, (v: number) => {
  audioObjects.forEach((a) => {
    a.volume = v
  })
})

// Track which audio URLs we've already auto-started so we don't restart them
// on every SignalR update or across rounds (multi-round audio should continue).
const autoStartedUrls = new Set<string>()

watch(
  () => props.messages,
  (msgs: MediaMessage[]) => {
    if (!msgs) return

    // Collect all active audio URLs from current messages
    const activeAudioUrls = new Set<string>()
    for (const msg of msgs) {
      if (msg.fileType === 'audio' && msg.fileUrl) {
        activeAudioUrls.add(msg.fileUrl)
        const loop = (msg.roundsToPlay ?? 1) > 1

        // Auto-start audio that we haven't started yet
        if (!autoStartedUrls.has(msg.fileUrl)) {
          autoStartedUrls.add(msg.fileUrl)
          const url = msg.fileUrl
          // Small delay to let the component mount on first render
          setTimeout(() => {
            const audio = getOrCreateAudio(url, loop)
            audio.volume = volume.value
            audio.play().catch((err) => {
              console.warn('[MediaMessages] Auto-play failed:', err.message)
            })
            playingUrl.value = url
          }, 100)
        }
      }
    }

    // Stop audio that is no longer in the messages list (media expired / round ended)
    for (const url of autoStartedUrls) {
      if (!activeAudioUrls.has(url)) {
        const audio = audioObjects.get(url)
        if (audio && !audio.paused) {
          audio.pause()
        }
        audioObjects.delete(url)
        autoStartedUrls.delete(url)
        if (playingUrl.value === url) {
          playingUrl.value = null
        }
      }
    }
  },
  { deep: true, immediate: true },
)

onBeforeUnmount(() => {
  audioObjects.forEach((a) => {
    if (!a.paused) a.pause()
  })
  audioObjects.clear()
  autoStartedUrls.clear()
})

function getMediaIcon(fileType: string): string {
  if (fileType === 'audio') return '🎵'
  if (fileType === 'image') return '🖼️'
  return '💬'
}
</script>

<template>
  <div v-if="messages.length" class="media-messages">
    <div class="media-header">
      <span class="media-title">Character Events</span>
      <div class="volume-control">
        <span class="vol-icon">🔊</span>
        <input
          v-model.number="volume"
          type="range"
          min="0"
          max="1"
          step="0.05"
          class="vol-slider"
          title="Volume"
        />
        <span class="vol-label">{{ Math.round(volume * 100) }}%</span>
      </div>
    </div>

    <div class="media-list">
      <div
        v-for="(msg, idx) in messages"
        :key="idx"
        class="media-card"
        :class="[`media-${msg.fileType}`]"
      >
        <div class="media-card-header">
          <span class="media-icon">{{ getMediaIcon(msg.fileType) }}</span>
          <span class="passive-name">{{ msg.passiveName }}</span>
        </div>

        <div class="media-card-body">
          <!-- Text content -->
          <p class="phrase-text">{{ msg.text }}</p>

          <!-- Audio player (imperative Audio objects, no <audio> in DOM) -->
          <div v-if="msg.fileType === 'audio' && msg.fileUrl" class="audio-player">
            <button
              class="play-btn"
              :class="{ playing: isPlaying(msg.fileUrl) }"
              @click="togglePlay(msg.fileUrl, msg.roundsToPlay ?? 1)"
            >
              {{ isPlaying(msg.fileUrl) ? '⏸' : '▶' }}
            </button>
            <span class="audio-file">{{ msg.fileUrl?.split('/').pop() }}</span>
            <span v-if="(msg.roundsToPlay ?? 1) > 1" class="loop-badge">LOOP</span>
          </div>

          <!-- Image/GIF display -->
          <div v-if="msg.fileType === 'image' && msg.fileUrl" class="image-container">
            <img :src="msg.fileUrl" :alt="msg.text" class="media-image" loading="lazy" />
          </div>
        </div>
      </div>
    </div>
  </div>
</template>

<style scoped>
.media-messages {
  margin-top: 8px;
}

.media-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 6px 10px;
  background: var(--bg-card);
  border: 1px solid var(--border-subtle);
  border-radius: var(--radius) var(--radius) 0 0;
}

.media-title {
  font-size: 11px;
  font-weight: 800;
  color: var(--accent-purple);
  text-transform: uppercase;
  letter-spacing: 0.5px;
}

.volume-control {
  display: flex;
  align-items: center;
  gap: 5px;
}

.vol-icon {
  font-size: 12px;
}

.vol-slider {
  width: 70px;
  height: 3px;
  -webkit-appearance: none;
  appearance: none;
  background: var(--bg-inset);
  border-radius: 2px;
  outline: none;
  cursor: pointer;
}

.vol-slider::-webkit-slider-thumb {
  -webkit-appearance: none;
  width: 12px;
  height: 12px;
  border-radius: 50%;
  background: var(--accent-purple);
  cursor: pointer;
}

.vol-slider::-moz-range-thumb {
  width: 12px;
  height: 12px;
  border-radius: 50%;
  background: var(--accent-purple);
  cursor: pointer;
  border: none;
}

.vol-label {
  font-size: 10px;
  color: var(--text-muted);
  min-width: 28px;
  text-align: right;
  font-family: var(--font-mono);
}

.media-list {
  display: flex;
  flex-direction: column;
  gap: 0;
  border: 1px solid var(--border-subtle);
  border-top: none;
  border-radius: 0 0 var(--radius) var(--radius);
  overflow: hidden;
}

.media-card {
  padding: 8px 12px;
  background: var(--bg-card);
  border-bottom: 1px solid var(--border-subtle);
  transition: background 0.15s;
}

.media-card:last-child {
  border-bottom: none;
}

.media-card:hover {
  background: var(--bg-card-hover);
}

.media-card-header {
  display: flex;
  align-items: center;
  gap: 5px;
  margin-bottom: 3px;
}

.media-icon {
  font-size: 12px;
}

.passive-name {
  font-size: 10px;
  font-weight: 800;
  color: var(--accent-gold);
  text-transform: uppercase;
  letter-spacing: 0.3px;
}

.media-card-body {
  padding-left: 20px;
}

.phrase-text {
  font-size: 13px;
  color: var(--text-primary);
  margin: 0 0 4px 0;
  line-height: 1.4;
  font-style: italic;
}

/* ── Audio ────────────────────────────────────────── */

.audio-player {
  display: flex;
  align-items: center;
  gap: 8px;
  padding: 5px 8px;
  background: rgba(180, 150, 255, 0.05);
  border: 1px solid rgba(180, 150, 255, 0.15);
  border-radius: var(--radius);
  margin-top: 4px;
}

.play-btn {
  width: 28px;
  height: 28px;
  border: none;
  border-radius: 50%;
  background: var(--kh-c-secondary-purple-500);
  color: #fff;
  font-size: 12px;
  cursor: pointer;
  display: flex;
  align-items: center;
  justify-content: center;
  transition: all 0.15s;
  flex-shrink: 0;
}

.play-btn:hover {
  background: var(--accent-purple);
  box-shadow: var(--glow-purple);
}

.play-btn.playing {
  background: var(--accent-orange);
}

.play-btn.playing:hover {
  background: #d4830f;
}

.audio-file {
  font-size: 10px;
  color: var(--text-muted);
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
  font-family: var(--font-mono);
}

.loop-badge {
  font-size: 8px;
  font-weight: 800;
  color: var(--accent-purple);
  background: rgba(180, 150, 255, 0.1);
  padding: 2px 5px;
  border-radius: 3px;
  letter-spacing: 0.5px;
  flex-shrink: 0;
}

/* ── Image ────────────────────────────────────────── */

.image-container {
  margin-top: 4px;
  border-radius: var(--radius);
  overflow: hidden;
  border: 1px solid var(--border-subtle);
  max-width: 400px;
}

.media-image {
  width: 100%;
  height: auto;
  display: block;
  max-height: 300px;
  object-fit: contain;
  background: var(--bg-inset);
}

/* ── Type-specific card accents ───────────────────── */

.media-audio {
  border-left: 2px solid var(--accent-purple);
}

.media-image {
  border-left: 2px solid var(--accent-blue);
}

.media-text {
  border-left: 2px solid var(--accent-gold);
}
</style>

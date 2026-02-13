<script setup lang="ts">
import { ref, watch, onBeforeUnmount } from 'vue'
import type { MediaMessage } from 'src/services/signalr'

const props = defineProps<{
  messages: MediaMessage[]
}>()

// ── Audio playback state ──────────────────────────────────────────
const volume = ref(0.5)
const audioElements = ref<Record<number, HTMLAudioElement>>({})
const playingIdx = ref<number | null>(null)

function onAudioRef(el: HTMLAudioElement | null, idx: number) {
  if (el) {
    audioElements.value[idx] = el
    el.volume = volume.value
  } else {
    delete audioElements.value[idx]
  }
}

function togglePlay(idx: number) {
  const audio = audioElements.value[idx]
  if (!audio) return

  if (playingIdx.value === idx && !audio.paused) {
    audio.pause()
    playingIdx.value = null
  } else {
    // Pause any other playing audio
    Object.values(audioElements.value).forEach((a) => {
      if (!a.paused) a.pause()
    })
    audio.currentTime = 0
    audio.volume = volume.value
    audio.play()
    playingIdx.value = idx
  }
}

function onAudioEnded(idx: number) {
  if (playingIdx.value === idx) {
    playingIdx.value = null
  }
}

watch(volume, (v) => {
  Object.values(audioElements.value).forEach((a) => {
    a.volume = v
  })
})

// Auto-play new audio messages when they appear
watch(
  () => props.messages,
  (newMsgs, oldMsgs) => {
    if (!newMsgs || !oldMsgs) return
    // Find new audio messages that weren't in the old list
    const oldLen = oldMsgs.length
    for (let i = oldLen; i < newMsgs.length; i++) {
      if (newMsgs[i].fileType === 'audio') {
        // Queue auto-play after a short delay for the DOM to update
        const idx = i
        setTimeout(() => {
          const audio = audioElements.value[idx]
          if (audio) {
            Object.values(audioElements.value).forEach((a) => {
              if (!a.paused) a.pause()
            })
            audio.volume = volume.value
            audio.play()
            playingIdx.value = idx
          }
        }, 100)
        break // only auto-play the first new audio
      }
    }
  },
  { deep: true },
)

onBeforeUnmount(() => {
  Object.values(audioElements.value).forEach((a) => {
    if (!a.paused) a.pause()
  })
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

          <!-- Audio player -->
          <div v-if="msg.fileType === 'audio' && msg.fileUrl" class="audio-player">
            <button
              class="play-btn"
              :class="{ playing: playingIdx === idx }"
              @click="togglePlay(idx)"
            >
              {{ playingIdx === idx ? '⏸' : '▶' }}
            </button>
            <audio
              :ref="(el) => onAudioRef(el as HTMLAudioElement, idx)"
              :src="msg.fileUrl"
              preload="auto"
              @ended="onAudioEnded(idx)"
            />
            <span class="audio-file">{{ msg.fileUrl?.split('/').pop() }}</span>
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
  margin-top: 10px;
}

.media-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 8px 12px;
  background: var(--bg-card);
  border: 1px solid var(--border-color);
  border-radius: var(--radius) var(--radius) 0 0;
}

.media-title {
  font-size: 13px;
  font-weight: 700;
  color: var(--accent-purple);
  text-transform: uppercase;
  letter-spacing: 0.5px;
}

.volume-control {
  display: flex;
  align-items: center;
  gap: 6px;
}

.vol-icon {
  font-size: 14px;
}

.vol-slider {
  width: 80px;
  height: 4px;
  -webkit-appearance: none;
  appearance: none;
  background: var(--bg-secondary);
  border-radius: 2px;
  outline: none;
  cursor: pointer;
}

.vol-slider::-webkit-slider-thumb {
  -webkit-appearance: none;
  width: 14px;
  height: 14px;
  border-radius: 50%;
  background: var(--accent-purple);
  cursor: pointer;
}

.vol-slider::-moz-range-thumb {
  width: 14px;
  height: 14px;
  border-radius: 50%;
  background: var(--accent-purple);
  cursor: pointer;
  border: none;
}

.vol-label {
  font-size: 11px;
  color: var(--text-muted);
  min-width: 32px;
  text-align: right;
}

.media-list {
  display: flex;
  flex-direction: column;
  gap: 0;
  border: 1px solid var(--border-color);
  border-top: none;
  border-radius: 0 0 var(--radius) var(--radius);
  overflow: hidden;
}

.media-card {
  padding: 10px 14px;
  background: var(--bg-card);
  border-bottom: 1px solid var(--border-color);
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
  gap: 6px;
  margin-bottom: 4px;
}

.media-icon {
  font-size: 14px;
}

.passive-name {
  font-size: 11px;
  font-weight: 700;
  color: var(--accent-gold);
  text-transform: uppercase;
  letter-spacing: 0.3px;
}

.media-card-body {
  padding-left: 22px;
}

.phrase-text {
  font-size: 14px;
  color: var(--text-primary);
  margin: 0 0 6px 0;
  line-height: 1.4;
  font-style: italic;
}

/* ── Audio ────────────────────────────────────────── */

.audio-player {
  display: flex;
  align-items: center;
  gap: 8px;
  padding: 6px 10px;
  background: rgba(167, 139, 250, 0.08);
  border: 1px solid rgba(167, 139, 250, 0.2);
  border-radius: 8px;
  margin-top: 4px;
}

.play-btn {
  width: 32px;
  height: 32px;
  border: none;
  border-radius: 50%;
  background: var(--accent-purple);
  color: #fff;
  font-size: 14px;
  cursor: pointer;
  display: flex;
  align-items: center;
  justify-content: center;
  transition: all 0.15s;
  flex-shrink: 0;
}

.play-btn:hover {
  transform: scale(1.1);
  background: #8b5cf6;
}

.play-btn.playing {
  background: var(--accent-orange);
}

.play-btn.playing:hover {
  background: #f59e0b;
}

.audio-file {
  font-size: 11px;
  color: var(--text-muted);
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

/* ── Image ────────────────────────────────────────── */

.image-container {
  margin-top: 6px;
  border-radius: 8px;
  overflow: hidden;
  border: 1px solid var(--border-color);
  max-width: 400px;
}

.media-image {
  width: 100%;
  height: auto;
  display: block;
  max-height: 300px;
  object-fit: contain;
  background: var(--bg-secondary);
}

/* ── Type-specific card accents ───────────────────── */

.media-audio {
  border-left: 3px solid var(--accent-purple);
}

.media-image {
  border-left: 3px solid var(--accent-blue);
}

.media-text {
  border-left: 3px solid var(--accent-gold);
}
</style>

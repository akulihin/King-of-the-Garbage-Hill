<script setup lang="ts">
import type { Component } from 'vue'
import {
  currentLocale,
  message,
  setLocale,
  type AppLocale,
} from './localization'

defineProps<{
  game: Component
}>()

function changeLocale(locale: AppLocale) {
  setLocale(locale)
}
</script>

<template>
  <div class="standalone-game-shell">
    <nav class="standalone-game-shell__controls" :aria-label="message('shell.language.label')">
      <a href="/games" :title="message('kotgh.title')">KOTGH</a>
      <button
        type="button"
        :class="{ active: currentLocale === 'ru' }"
        :aria-pressed="currentLocale === 'ru'"
        @click="changeLocale('ru')"
      >
        RU
      </button>
      <button
        type="button"
        :class="{ active: currentLocale === 'en' }"
        :aria-pressed="currentLocale === 'en'"
        @click="changeLocale('en')"
      >
        ENG
      </button>
    </nav>
    <component :is="game" />
  </div>
</template>

<style scoped>
.standalone-game-shell {
  min-height: 100vh;
}

.standalone-game-shell__controls {
  position: fixed;
  z-index: 70;
  top: 10px;
  right: 12px;
  display: inline-flex;
  overflow: hidden;
  border: 1px solid rgba(255, 255, 255, 0.2);
  border-radius: 6px;
  background: rgba(12, 15, 20, 0.9);
  box-shadow: 0 2px 8px rgba(0, 0, 0, 0.35);
}

.standalone-game-shell__controls a,
.standalone-game-shell__controls button {
  min-width: 34px;
  padding: 6px 8px;
  border: 0;
  color: #aeb7c2;
  background: transparent;
  cursor: pointer;
  font: 700 11px/1 'JetBrains Mono', monospace;
  text-decoration: none;
}

.standalone-game-shell__controls a {
  display: inline-flex;
  align-items: center;
  border-right: 1px solid rgba(255, 255, 255, 0.15);
}

.standalone-game-shell__controls button.active {
  color: #11151b;
  background: #f0c850;
}
</style>

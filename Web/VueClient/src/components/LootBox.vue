<script setup lang="ts">
import { computed } from 'vue'
import type { LootBoxResult } from 'src/services/signalr'

const props = defineProps<{
  result: LootBoxResult
}>()

const emit = defineEmits<{
  dismiss: []
}>()

const rarityClass = computed(() => props.result.rarity.toLowerCase())

const rarityLabel = computed(() => {
  const labels: Record<string, string> = {
    common: 'Common',
    uncommon: 'Uncommon',
    rare: 'Rare',
    epic: 'Epic',
    legendary: 'LEGENDARY',
  }
  return labels[rarityClass.value] ?? props.result.rarity
})
</script>

<template>
  <div class="lootbox-overlay" @click.self="emit('dismiss')">
    <div class="lootbox-container" :class="rarityClass">
      <div class="lootbox-glow" />
      <div class="lootbox-content">
        <div class="lootbox-icon">
          <div class="box-shape" />
        </div>
        <div class="lootbox-rarity">{{ rarityLabel }}</div>
        <div class="lootbox-amount">+{{ result.zbsAmount }} ZBS</div>
        <button class="btn btn-primary lootbox-claim" @click="emit('dismiss')">
          Claim
        </button>
      </div>
    </div>
  </div>
</template>

<style scoped>
.lootbox-overlay {
  position: fixed;
  inset: 0;
  z-index: 1000;
  display: flex;
  align-items: center;
  justify-content: center;
  background: rgba(0, 0, 0, 0.75);
  backdrop-filter: blur(4px);
  animation: fadeIn 0.3s ease;
}

@keyframes fadeIn {
  from { opacity: 0; }
  to { opacity: 1; }
}

.lootbox-container {
  position: relative;
  width: 280px;
  padding: 32px 24px;
  border-radius: 16px;
  text-align: center;
  background: var(--bg-card);
  border: 2px solid var(--border-subtle);
  animation: popIn 0.5s cubic-bezier(0.34, 1.56, 0.64, 1);
  overflow: hidden;
}

@keyframes popIn {
  from { transform: scale(0.6); opacity: 0; }
  to { transform: scale(1); opacity: 1; }
}

.lootbox-glow {
  position: absolute;
  inset: -40px;
  border-radius: 50%;
  opacity: 0.15;
  filter: blur(40px);
  animation: pulse 2s ease-in-out infinite;
  pointer-events: none;
}

@keyframes pulse {
  0%, 100% { opacity: 0.1; transform: scale(0.95); }
  50% { opacity: 0.25; transform: scale(1.05); }
}

/* Rarity colors */
.common { border-color: #9e9e9e; }
.common .lootbox-glow { background: #9e9e9e; }
.common .lootbox-rarity { color: #9e9e9e; }

.uncommon { border-color: #4caf50; }
.uncommon .lootbox-glow { background: #4caf50; }
.uncommon .lootbox-rarity { color: #4caf50; }

.rare { border-color: #2196f3; }
.rare .lootbox-glow { background: #2196f3; }
.rare .lootbox-rarity { color: #2196f3; }

.epic { border-color: #9c27b0; }
.epic .lootbox-glow { background: #9c27b0; }
.epic .lootbox-rarity { color: #9c27b0; }

.legendary { border-color: #ff9800; }
.legendary .lootbox-glow { background: #ff9800; opacity: 0.3; }
.legendary .lootbox-rarity { color: #ff9800; text-shadow: 0 0 12px rgba(255, 152, 0, 0.5); }
.legendary .lootbox-amount { text-shadow: 0 0 12px rgba(255, 152, 0, 0.3); }

.lootbox-content {
  position: relative;
  z-index: 1;
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 12px;
}

.lootbox-icon {
  width: 64px;
  height: 64px;
  display: flex;
  align-items: center;
  justify-content: center;
  animation: bounce 0.6s ease 0.3s both;
}

@keyframes bounce {
  0% { transform: translateY(-20px); opacity: 0; }
  60% { transform: translateY(4px); }
  100% { transform: translateY(0); opacity: 1; }
}

.box-shape {
  width: 48px;
  height: 48px;
  border-radius: 8px;
  border: 3px solid currentColor;
  position: relative;
  animation: shake 0.4s ease 0.6s;
}

.common .box-shape { color: #9e9e9e; background: rgba(158, 158, 158, 0.1); }
.uncommon .box-shape { color: #4caf50; background: rgba(76, 175, 80, 0.1); }
.rare .box-shape { color: #2196f3; background: rgba(33, 150, 243, 0.1); }
.epic .box-shape { color: #9c27b0; background: rgba(156, 39, 176, 0.1); }
.legendary .box-shape {
  color: #ff9800;
  background: rgba(255, 152, 0, 0.15);
  box-shadow: 0 0 20px rgba(255, 152, 0, 0.3);
}

@keyframes shake {
  0%, 100% { transform: rotate(0deg); }
  25% { transform: rotate(-5deg); }
  75% { transform: rotate(5deg); }
}

.lootbox-rarity {
  font-size: 14px;
  font-weight: 800;
  text-transform: uppercase;
  letter-spacing: 2px;
  animation: revealText 0.4s ease 0.5s both;
}

.lootbox-amount {
  font-size: 32px;
  font-weight: 800;
  color: var(--accent-gold);
  font-family: var(--font-mono);
  animation: revealText 0.4s ease 0.7s both;
}

@keyframes revealText {
  from { opacity: 0; transform: translateY(8px); }
  to { opacity: 1; transform: translateY(0); }
}

.lootbox-claim {
  margin-top: 8px;
  padding: 8px 32px;
  font-size: 14px;
  animation: revealText 0.4s ease 0.9s both;
}
</style>

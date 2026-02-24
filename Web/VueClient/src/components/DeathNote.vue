<script setup lang="ts">
import { ref, computed } from 'vue'
import type { Player, DeathNote, CharacterInfo } from 'src/services/signalr'

const props = defineProps<{
  deathNote: DeathNote
  players: Player[]
  myPlayerId: string
  characterNames: string[]
  characterCatalog: CharacterInfo[]
  isFinished: boolean
  moral: string
}>()

const emit = defineEmits<{
  write: [payload: { targetPlayerId: string; characterName: string }]
  shinigamiEyes: []
}>()

// Which player's write dropdown is open
const writeOpenFor = ref<string | null>(null)
const writeSearch = ref('')

const opponents = computed(() =>
  props.players.filter(
    (p: Player) => p.playerId !== props.myPlayerId && !p.isDead,
  ),
)

const hasWrittenThisRound = computed(
  () => props.deathNote.currentRoundTarget !== '00000000-0000-0000-0000-000000000000'
    && props.deathNote.currentRoundTarget !== '',
)

function isFailedTarget(playerId: string): boolean {
  return props.deathNote.failedTargets.includes(playerId)
}

function getRevealedName(playerId: string): string | null {
  const r = props.deathNote.revealedPlayers.find((rp) => rp.playerId === playerId)
  return r?.characterName || null
}

function getEntryForPlayer(playerId: string) {
  return props.deathNote.entries.filter((e) => e.targetPlayerId === playerId)
}

function isLPlayer(playerId: string): boolean {
  return playerId === props.deathNote.lPlayerId
}

const charCatalogMap = computed(() => {
  const map: Record<string, CharacterInfo> = {}
  for (const c of props.characterCatalog || []) {
    map[c.name] = c
  }
  return map
})

function filteredCharacters(): string[] {
  const q = writeSearch.value.toLowerCase()
  if (!q) return props.characterNames
  return props.characterNames.filter((n: string) => n.toLowerCase().includes(q))
}

function openWrite(playerId: string) {
  writeOpenFor.value = playerId
  writeSearch.value = ''
}

function closeWrite() {
  writeOpenFor.value = null
  writeSearch.value = ''
}

function selectWrite(playerId: string, charName: string) {
  emit('write', { targetPlayerId: playerId, characterName: charName })
  writeOpenFor.value = null
  writeSearch.value = ''
}
</script>

<template>
  <div class="death-note card">
    <div class="dn-header">
      <span class="dn-title">Death Note</span>
      <span v-if="deathNote.isArrested" class="dn-arrested">ARRESTED</span>
      <span v-if="deathNote.shinigamiEyesActive" class="dn-eyes-active">Eyes Active</span>
    </div>

    <div class="dn-list">
      <div
        v-for="opp in opponents"
        :key="opp.playerId"
        class="dn-row"
        :class="{
          'dn-failed': isFailedTarget(opp.playerId),
          'dn-is-l': isLPlayer(opp.playerId),
        }"
      >
        <div class="dn-player">
          <span class="dn-name">{{ opp.discordUsername }}</span>
          <span v-if="isLPlayer(opp.playerId)" class="dn-l-badge">L</span>
          <span v-if="getRevealedName(opp.playerId)" class="dn-revealed">
            {{ getRevealedName(opp.playerId) }}
          </span>
        </div>

        <!-- History entries -->
        <div v-if="getEntryForPlayer(opp.playerId).length" class="dn-history">
          <span
            v-for="(entry, idx) in getEntryForPlayer(opp.playerId)"
            :key="idx"
            class="dn-entry"
            :class="{ 'dn-correct': entry.wasCorrect, 'dn-wrong': !entry.wasCorrect }"
            :title="`R${entry.roundWritten}: ${entry.writtenName}`"
          >
            {{ entry.wasCorrect ? '&#10003;' : '&#10007;' }} {{ entry.writtenName }}
          </span>
        </div>

        <!-- Write button -->
        <button
          v-if="!isFinished && !hasWrittenThisRound && !isFailedTarget(opp.playerId)"
          class="dn-write-btn"
          @click="openWrite(opp.playerId)"
        >
          Write
        </button>
        <span v-else-if="isFailedTarget(opp.playerId)" class="dn-locked">Locked</span>
        <span v-else-if="hasWrittenThisRound && deathNote.currentRoundTarget === opp.playerId" class="dn-pending">
          {{ deathNote.currentRoundName }}
        </span>
      </div>
    </div>

    <!-- Write dropdown — Teleported to body -->
    <Teleport to="body">
      <div v-if="writeOpenFor" class="predict-overlay" @click="closeWrite">
        <div class="predict-dropdown" @click.stop>
          <input
            v-model="writeSearch"
            class="predict-search"
            placeholder="Write character name..."
            autofocus
          >
          <div class="predict-list">
            <button
              v-for="name in filteredCharacters()"
              :key="name"
              class="predict-option"
              @click="selectWrite(writeOpenFor!, name)"
            >
              <img
                v-if="charCatalogMap[name]?.avatar"
                :src="charCatalogMap[name].avatar"
                class="predict-option-avatar"
                :alt="name"
              >
              <span>{{ name }}</span>
            </button>
            <div v-if="filteredCharacters().length === 0" class="predict-empty">
              No characters found
            </div>
          </div>
        </div>
      </div>
    </Teleport>
  </div>
</template>

<style scoped>
.death-note.card {
  padding: 10px;
  background: rgba(20, 15, 15, 0.8);
  border: 1px solid rgba(180, 50, 50, 0.3);
}

.dn-header {
  display: flex;
  align-items: center;
  gap: 8px;
  margin-bottom: 8px;
}

.dn-title {
  font-weight: 900;
  font-size: 14px;
  color: #ef5050;
  letter-spacing: 1px;
  text-transform: uppercase;
}

.dn-arrested {
  font-size: 10px;
  font-weight: 800;
  color: #fff;
  background: #c03030;
  padding: 2px 6px;
  border-radius: 3px;
}

.dn-eyes-active {
  font-size: 10px;
  font-weight: 700;
  color: #ef5050;
  border: 1px solid rgba(200, 50, 50, 0.4);
  padding: 2px 6px;
  border-radius: 3px;
}

.dn-list {
  display: flex;
  flex-direction: column;
  gap: 3px;
}

.dn-row {
  display: flex;
  align-items: center;
  gap: 8px;
  padding: 5px 8px;
  border-radius: var(--radius);
  background: var(--bg-secondary);
  border: 1px solid transparent;
}

.dn-row.dn-failed {
  opacity: 0.5;
  border-color: rgba(200, 50, 50, 0.2);
}

.dn-row.dn-is-l {
  border-color: rgba(200, 50, 50, 0.3);
}

.dn-player {
  flex: 1;
  display: flex;
  align-items: center;
  gap: 6px;
  min-width: 0;
}

.dn-name {
  font-weight: 700;
  font-size: 12px;
  color: var(--text-primary);
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}

.dn-l-badge {
  padding: 1px 5px;
  border-radius: 3px;
  font-size: 9px;
  font-weight: 900;
  background: #c03030;
  color: #fff;
}

.dn-revealed {
  font-size: 10px;
  color: #ef5050;
  font-weight: 600;
}

.dn-history {
  display: flex;
  gap: 4px;
  flex-shrink: 0;
}

.dn-entry {
  font-size: 10px;
  font-family: var(--font-mono);
  padding: 1px 4px;
  border-radius: 3px;
}

.dn-correct {
  color: var(--accent-green);
  background: rgba(100, 200, 100, 0.1);
}

.dn-wrong {
  color: var(--accent-red);
  background: rgba(200, 100, 100, 0.1);
}

.dn-write-btn {
  padding: 3px 10px;
  border: 1px solid rgba(200, 50, 50, 0.4);
  border-radius: var(--radius);
  background: rgba(200, 50, 50, 0.08);
  color: #ef5050;
  font-size: 11px;
  font-weight: 700;
  cursor: pointer;
  flex-shrink: 0;
  transition: all 0.15s;
}

.dn-write-btn:hover {
  background: rgba(200, 50, 50, 0.18);
  border-color: rgba(200, 50, 50, 0.6);
}

.dn-locked {
  font-size: 10px;
  color: var(--text-dim);
  font-style: italic;
}

.dn-pending {
  font-size: 10px;
  color: #ef5050;
  font-weight: 600;
}
</style>

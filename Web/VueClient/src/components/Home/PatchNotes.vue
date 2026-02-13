<template>
  <div class="patchNotes">
    <header>
      <div class="version">kotgh-1.0</div>
      <h2>Patch notes</h2>
    </header>
    <div class="notesList">
      <div v-for="(note, index) in patchNotes" :key="index" class="patchNoteBox">
        <div>
          <h3 class="patchVersion">{{ note.version }}</h3>
          <div class="patchDate">{{ note.date }}</div>
        </div>
        <div class="patchContents">
          <h4>{{ note.title }}</h4>
          <p class="patchDescription">{{ note.description }}</p>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
const patchNotes = [
  {
    version: '1.2.4',
    date: '2024-01-01',
    title: 'Balance Update',
    description: `um has been the industry's standard dummy text ever since the 1500s, when an unknown printer took a galley of type and scrambled it to make a type specimen book. It has survived not only five centuries, but also the leap into electronic typesetting, remaining e`,
  },
  {
    version: '1.2.0',
    date: '2024-02-01',
    title: 'New Features',
    description: 'Added new feature',
  },
  {
    version: '1.1.5',
    date: '2024-01-01',
    title: 'Bug Fixes',
    description: `um has been the industry's standard dummy text ever since the 1500s, when an unknown printer took a galley of type and scrambled it to make a type specimen book. It has survived not only five centuries, but also the leap into electronic typesetting, remaining e`,
  },
]
</script>

<style scoped>
* { display: flex; }

.patchNotes {
  align-self: flex-start;
  background-color: var(--kh-c-neutrals-sat-700);
  flex-direction: column;
  padding: 0.5rem;
  gap: 0.25rem;
}

.version {
  color: var(--kh-c-text-primary-700);
  font-size: 0.625rem;
  justify-content: center;
}

.patchNotes > header {
  flex-direction: column;
  padding: 1rem 0px;
  gap: 0.125rem;
}

.patchNotes > header > h2 {
  color: var(--kh-c-text-primary-700);
  font-size: 1.125rem;
  justify-content: center;
}

.notesList {
  flex-direction: column;
  gap: 0.625rem;
}

.notesList > .patchNoteBox {
  width: 25rem;
  flex-direction: column;
  padding: 1.5rem;
  gap: 0.625rem;
  border: 1px solid var(--kh-c-neutrals-sat-500);
  background-color: var(--kh-c-neutrals-sat-600);
}

.patchNoteBox > div:first-child {
  justify-content: space-between;
}

.patchNoteBox > div:first-child > .patchVersion {
  font-size: 1.25rem;
  color: var(--kh-c-text-primary-600);
}

.patchNoteBox > div:first-child > .patchDate {
  font-size: 0.625rem;
  align-items: center;
  color: var(--kh-c-text-primary-700);
}

.patchNoteBox > .patchContents {
  flex-direction: column;
  gap: 0.625rem;
}

.patchNoteBox > .patchContents > h4 {
  font-size: 0.875rem;
  color: var(--kh-c-text-primary-500);
}

.patchNoteBox > .patchContents > p {
  color: var(--kh-c-text-primary-600);
}
</style>

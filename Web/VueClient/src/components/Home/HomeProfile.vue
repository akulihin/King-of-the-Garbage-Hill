<template>
  <div class="homeProfile">
    <div class="userInfo">
      <div class="userName">{{ username }}</div>
      <div class="currency">
        <div>{{ currency }}</div>
        <div>coinicon</div>
      </div>
    </div>
    <div class="matches">
      <div>
        <h4>Matches</h4>
      </div>
      <div class="matchList">
        <MatchBox />
        <MatchBox />
        <MatchBox />
      </div>
    </div>
    <div class="chances">
      <div>
        <h4>Chances</h4>
      </div>
      <div class="chanceList">
        <ChanceBox />
        <ChanceBox />
        <ChanceBox />
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref } from 'vue'
import MatchBox from 'src/components/Home/MatchBox.vue'
import ChanceBox from 'src/components/Home/ChanceBox.vue'

const username = ref('Sirinoks')
const currency = ref(124)
</script>

<style scoped>
* { display: flex; }
.homeProfile {
  background-color: var(--kh-c-neutrals-sat-700);
  border: 1px solid var(--kh-c-neutrals-pale-375);
  border-radius: 10px;
  flex-direction: column;
  gap: 0.75rem;
  padding: 1.25rem;
}

.homeProfile h4 {
  font-size: 0.8rem;
  color: var(--kh-c-text-primary-700);
  text-transform: uppercase;
  letter-spacing: 0.5px;
  font-weight: 800;
}

.userInfo {
  flex-direction: column;
  font-size: 1.25rem;
  color: var(--kh-c-text-primary-600);
  gap: 0.5rem;
}

.currency {
  gap: 0.25rem;
  color: var(--kh-c-text-highlight-primary);
  font-family: 'JetBrains Mono', monospace;
  font-weight: 800;
  font-size: 1.1rem;
}

.homeProfile > div {
  flex-direction: column;
}

.matches {
  flex-direction: column;
  gap: 0.4rem;
}

.matchList, .chanceList {
  flex-direction: column;
  gap: 2px;
}
</style>

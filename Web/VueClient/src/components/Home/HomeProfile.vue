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
  background-color: var(--kh-c-neutrals-sat-600);
  flex-direction: column;
  gap: 1rem;
  padding: 1.5rem;
}

.homeProfile h4 {
  font-size: 1rem;
  color: var(--kh-c-text-primary-700);
}

.userInfo {
  flex-direction: column;
  font-size: 1.5rem;
  color: var(--kh-c-text-primary-700);
  gap: 0.75rem;
}

.currency {
  gap: 0.25rem;
  color: var(--kh-c-text-highlight-primary);
}

.homeProfile > div {
  flex-direction: column;
}

.matches {
  flex-direction: column;
  gap: 0.5rem;
}

.matchList, .chanceList {
  flex-direction: column;
}
</style>

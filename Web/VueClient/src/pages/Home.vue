<template>
  <div class="homePage">
    <nav>
      <RouterLink to="/games">Games</RouterLink>
      <RouterLink to="/home">Home</RouterLink>
      <RouterLink to="/about">Encyclopedia</RouterLink>
    </nav>
    <div class="homeContent">
      <HomeProfile />
      <div class="homeMain">
        <RouterView />
      </div>
      <PatchNotes />
    </div>

    <button class="logoutBtn" @click="logout">Log out</button>
  </div>
</template>

<script setup lang="ts">
import { useRouter } from 'vue-router'
import { useGameStore } from 'src/store/game'
import PatchNotes from 'src/components/Home/PatchNotes.vue'
import HomeProfile from 'src/components/Home/HomeProfile.vue'

const store = useGameStore()
const router = useRouter()

function logout() {
  localStorage.removeItem('discordId')
  store.discordId = ''
  store.isAuthenticated = false
  router.push('/')
}
</script>

<style scoped>
* { display: flex; }

.homePage {
  flex-direction: column;
  width: 100%;
}

.homePage > nav {
  align-self: center;
  gap: 2px;
  padding: 0 0.25rem;
}

.homePage > nav > a {
  background-color: var(--kh-c-neutrals-sat-600);
  padding: 0.5rem 1rem;
  color: var(--kh-c-text-primary-700);
  text-decoration: none;
  font-size: 0.85rem;
  font-weight: 700;
  border: 1px solid var(--kh-c-neutrals-pale-375);
  border-radius: 6px;
  transition: all 0.15s;
}

.homePage > nav > a:hover {
  background-color: var(--kh-c-neutrals-pale-500);
  color: var(--kh-c-text-primary-600);
}

.homePage > nav > a.router-link-active {
  background-color: var(--kh-c-neutrals-pale-575);
  color: var(--kh-c-text-highlight-primary);
  border-color: var(--kh-c-neutrals-pale-300);
}

.homePage > .homeContent {
  justify-content: space-between;
  padding: 1rem;
  gap: 1rem;
}

.homeMain {
  flex: 1;
  flex-direction: column;
}

.logoutBtn {
  align-self: flex-start;
  margin: 1rem;
  padding: 0.4rem 0.75rem;
  background-color: transparent;
  color: var(--kh-c-text-primary-800);
  border: 1px solid var(--kh-c-neutrals-pale-375);
  border-radius: 6px;
  cursor: pointer;
  font-size: 0.75rem;
  font-weight: 700;
  transition: all 0.15s;
}

.logoutBtn:hover {
  background-color: var(--kh-c-neutrals-pale-500);
  color: var(--kh-c-text-primary-700);
}
</style>

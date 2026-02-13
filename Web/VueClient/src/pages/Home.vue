<template>
  <div class="homePage">
    <nav>
      <RouterLink to="/">Games</RouterLink>
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
  gap: 0.25rem;
  padding: 0 0.25rem;
}

.homePage > nav > a {
  background-color: var(--kh-c-neutrals-pale-500);
  padding: 0.625rem 1rem;
  color: var(--kh-c-text-primary-600);
  text-decoration: none;
  font-size: 1.125rem;
  border: 1px solid var(--kh-c-neutrals-pale-260);
  transition: background-color 0.15s;
}

.homePage > nav > a:hover {
  background-color: var(--kh-c-neutrals-pale-350);
  border: 1px solid var(--kh-c-neutrals-pale-240);
}

.homePage > nav > a:active {
  background-color: var(--kh-c-neutrals-pale-575);
  border: 1px solid var(--kh-c-neutrals-pale-375);
}

.homePage > nav > a.router-link-active {
  background-color: var(--kh-c-neutrals-pale-575);
  border-bottom: 2px solid var(--kh-c-text-highlight-primary);
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
  padding: 0.5rem 1rem;
  background-color: var(--kh-c-neutrals-pale-500);
  color: var(--kh-c-text-primary-700);
  border: 1px solid var(--kh-c-neutrals-pale-260);
  cursor: pointer;
  font-size: 0.875rem;
  transition: background-color 0.15s;
}

.logoutBtn:hover {
  background-color: var(--kh-c-neutrals-pale-350);
}
</style>

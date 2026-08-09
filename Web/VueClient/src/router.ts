import type { RouteRecordRaw } from 'vue-router'
import { createRouter, createWebHistory } from 'vue-router'

export type AppRouteNames =
  | 'lobby'
  | 'adminLobby'
  | 'alternativeLobby'
  | 'game'
  | 'spectate'
  | 'replay'
  | 'home'
  | 'fortressOfDoom'
  | 'store'
  | 'achievements'
  | 'fightCalculator'
  | 'widget'

export const routes: RouteRecordRaw[] = [
  {
    path: '/',
    redirect: '/games',
  },
  {
    path: '/game',
    redirect: '/games',
  },
  {
    name: 'lobby',
    path: '/games',
    component: () => import('./pages/Lobby.vue'),
  },
  {
    name: 'adminLobby',
    path: '/admin-lobby',
    component: () => import('./pages/AdminLobby.vue'),
  },
  {
    name: 'alternativeLobby',
    path: '/alternative-lobby/:lobbyId',
    component: () => import('./pages/AlternativeLobby.vue'),
    props: true,
  },
  {
    name: 'game',
    path: '/game/:gameId',
    component: () => import('./pages/Game.vue'),
    props: true,
  },
  {
    name: 'spectate',
    path: '/spectate/:gameId',
    component: () => import('./pages/Spectate.vue'),
    props: true,
  },
  {
    name: 'replay',
    path: '/replay/:gameId',
    component: () => import('./pages/Replay.vue'),
    props: true,
  },
  {
    name: 'home',
    path: '/home',
    component: () => import('./pages/Home.vue'),
  },
  {
    name: 'fortressOfDoom',
    path: '/fortress-of-doom',
    component: () => import('./pages/FortressOfDoom.vue'),
  },
  {
    name: 'store',
    path: '/store',
    component: () => import('./pages/Store.vue'),
  },
  {
    name: 'achievements',
    path: '/achievements',
    component: () => import('./pages/Achievements.vue'),
  },
  {
    name: 'fightCalculator',
    path: '/fight-calculator',
    component: () => import('./pages/FightCalculator.vue'),
  },
  {
    name: 'widget',
    path: '/widget',
    component: () => import('./pages/Widget.vue'),
  },
]

export const router = createRouter({
  history: createWebHistory(),
  routes,
})

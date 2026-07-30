import type { RouteRecordRaw } from 'vue-router'
import { createRouter, createWebHistory } from 'vue-router'

export type AppRouteNames =
  | 'lobby'
  | 'adminLobby'
  | 'game'
  | 'spectate'
  | 'replay'
  | 'home'
  | 'fortressOfDoom'
  | 'store'
  | 'achievements'
  | 'fightCalculator'
  | 'lastChances'
  | 'empiresEndgame'
  | 'clash'
  | 'clashLab'
  | 'clashGame'
  | 'battleship'
  | 'battleshipGame'
  | 'battleshipSpectate'
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
    name: 'lastChances',
    path: '/99lc',
    component: () => import('./pages/LastChances.vue'),
  },
  {
    name: 'empiresEndgame',
    path: '/empires-endgame',
    component: () => import('./pages/EmpiresEndgame.vue'),
  },
  {
    name: 'clash',
    path: '/clash',
    component: () => import('./pages/ClashLobby.vue'),
  },
  {
    name: 'clashLab',
    path: '/clash/lab',
    component: () => import('./pages/Clash.vue'),
  },
  {
    name: 'clashGame',
    path: '/clash/:gameId',
    component: () => import('./pages/ClashGame.vue'),
    props: true,
  },
  {
    name: 'widget',
    path: '/widget',
    component: () => import('./pages/Widget.vue'),
  },
  {
    name: 'battleship',
    path: '/battleship',
    component: () => import('./pages/BattleshipLobby.vue'),
  },
  {
    name: 'battleshipGame',
    path: '/battleship/:gameId',
    component: () => import('./pages/BattleshipGame.vue'),
    props: true,
  },
  {
    name: 'battleshipSpectate',
    path: '/battleship/spectate/:gameId',
    component: () => import('./pages/BattleshipSpectate.vue'),
    props: true,
  },
]

export const router = createRouter({
  history: createWebHistory(),
  routes,
})

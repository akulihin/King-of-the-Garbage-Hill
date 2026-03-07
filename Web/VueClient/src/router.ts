import type { RouteRecordRaw } from 'vue-router'
import { createRouter, createWebHistory } from 'vue-router'

export type AppRouteNames =
  | 'lobby'
  | 'game'
  | 'spectate'
  | 'replay'
  | 'home'
  | 'battleship'
  | 'battleshipGame'
  | 'battleshipSpectate'

export const routes: RouteRecordRaw[] = [
  {
    path: '/',
    redirect: '/games',
  },
  {
    name: 'lobby',
    path: '/games',
    component: () => import('./pages/Lobby.vue'),
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

import type { RouteRecordRaw } from 'vue-router'
import { createRouter, createWebHashHistory } from 'vue-router'

export type AppRouteNames =
  | 'lobby'
  | 'game'
  | 'spectate'

export const routes: RouteRecordRaw[] = [
  {
    name: 'lobby',
    path: '/',
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
]

export const router = createRouter({
  history: createWebHashHistory(),
  routes,
})

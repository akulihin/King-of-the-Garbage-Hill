import type { RouteRecordRaw } from 'vue-router'
import { createRouter, createWebHistory } from 'vue-router'

export type AppRouteNames =
  | 'lobby'
  | 'game'
  | 'spectate'
  | 'home'

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
  {
    name: 'home',
    path: '/home',
    component: () => import('./pages/Home.vue'),
  },
]

export const router = createRouter({
  history: createWebHistory(),
  routes,
})

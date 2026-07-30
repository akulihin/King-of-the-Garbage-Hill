import { createRouter, createWebHistory, type RouteRecordRaw } from 'vue-router'

const routes: RouteRecordRaw[] = [
  {
    path: '/',
    redirect: '/battleship',
  },
  {
    name: 'battleship',
    path: '/battleship',
    component: () => import('../../pages/BattleshipLobby.vue'),
  },
  {
    name: 'battleshipSpectate',
    path: '/battleship/spectate/:gameId',
    component: () => import('../../pages/BattleshipSpectate.vue'),
    props: true,
  },
  {
    name: 'battleshipGame',
    path: '/battleship/:gameId',
    component: () => import('../../pages/BattleshipGame.vue'),
    props: true,
  },
]

export const battleshipRouter = createRouter({
  history: createWebHistory(),
  routes,
})

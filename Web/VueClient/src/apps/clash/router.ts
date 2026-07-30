import { createRouter, createWebHistory, type RouteRecordRaw } from 'vue-router'

const routes: RouteRecordRaw[] = [
  {
    path: '/',
    redirect: '/clash',
  },
  {
    name: 'clash',
    path: '/clash',
    component: () => import('../../pages/ClashLobby.vue'),
  },
  {
    name: 'clashGame',
    path: '/clash/:gameId([0-9a-f]{8})',
    component: () => import('../../pages/ClashGame.vue'),
    props: true,
  },
  {
    path: '/clash/:pathMatch(.*)*',
    redirect: '/clash',
  },
]

export const clashRouter = createRouter({
  history: createWebHistory(),
  routes,
})

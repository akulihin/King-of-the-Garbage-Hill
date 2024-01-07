import type { RouteParams, RouteRecordRaw } from 'vue-router'
import { createRouter, createWebHashHistory } from 'vue-router'
import Home from './pages/Home.vue'
import { isAuthorized } from './store/user'

export type AppRouteNames =
  | 'global-feed'
  | 'login'
  | 'register'
  | 'profile'
  | 'settings'

export const routes: RouteRecordRaw[] = [
  {
    name: 'global-feed',
    path: '/',
    component: Home,
  },
  {
    name: 'login',
    path: '/login',
    component: () => import('./pages/Login.vue'),
    beforeEnter: () => !isAuthorized(),
  },
  {
    name: 'register',
    path: '/register',
    component: () => import('./pages/Register.vue'),
    beforeEnter: () => !isAuthorized(),
  },
  {
    name: 'profile',
    path: '/profile/:username',
    component: () => import('./pages/Profile.vue'),
  },
  {
    name: 'settings',
    path: '/settings',
    component: () => import('./pages/Settings.vue'),
  },
]
export const router = createRouter({
  history: createWebHashHistory(),
  routes,
})

export function routerPush(name: AppRouteNames, params?: RouteParams): ReturnType<typeof router.push> {
  return params === undefined
    ? router.push({ name })
    : router.push({ name, params })
}

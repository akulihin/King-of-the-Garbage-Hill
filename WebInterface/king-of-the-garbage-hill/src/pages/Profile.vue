<template>
  <div class="profile-page">
    <div class="user-info">
      <div class="container">
        <div class="row">
          <div class="col-xs-12 col-md-10 offset-md-1">
            <div
              v-if="!profile"
              class="align-left"
            >
              Profile is downloading...
            </div>
            <template v-else>
              <img
                :src="profile.image"
                class="user-img"
                :alt="profile.username"
              >

              <h4>{{ profile.username }}</h4>

              <p v-if="profile.bio">
                {{ profile.bio }}
              </p>

              <AppLink
                v-if="showEdit"
                class="btn btn-sm btn-outline-secondary action-btn"
                name="settings"
                aria-label="Edit profile settings"
              >
                <i class="ion-gear-a space" />
                Edit profile settings
              </AppLink>

            </template>
          </div>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { computed } from 'vue'
import { useRoute } from 'vue-router'
import { storeToRefs } from 'pinia'
import { useProfile } from 'src/composable/useProfile'
import { useUserStore } from 'src/store/user'

const route = useRoute()
const username = computed<string>(() => route.params.username as string)

const { profile, updateProfile } = useProfile({ username })


const { user, isAuthorized } = storeToRefs(useUserStore())

const showEdit = computed<boolean>(() => isAuthorized && user.value?.username === username.value)
</script>

<style scoped>
.space {
  margin-right: 4px;
}
.align-left {
  text-align: left
}
</style>

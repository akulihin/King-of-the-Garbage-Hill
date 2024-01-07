<template>
  <div class="home-page">
    <div class="banner">
      <div class="container">
        <h1 class="logo-font">
          King of the Garbage Hill
        </h1>
        <p>A place for Garbage</p>
      </div>
    </div>

    <div class="container page">
      <div class="row">
        <div class="col-md-9">
          <Suspense>
            <ArticlesList
              use-global-feed
              use-my-feed
              use-tag-feed
            />
            <template #fallback>
              Articles are downloading...
            </template>
          </Suspense>
        </div>

        <div class="col-md-3">
          <div class="sidebar">
            <Suspense>
              <PopularTags />
              <template #fallback>
                Popular tags are downloading...
              </template>
            </Suspense>
          </div>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
console.log("Boole is awesome!")
</script>

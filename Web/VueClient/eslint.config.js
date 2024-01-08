import defineConfig from '@mutoe/eslint-config'

export default defineConfig({
  typescript: {
    tsconfigPath: [
      'tsconfig.json',
      'tsconfig.node.json'
    ],
  },
  vue: {
    sfcBlocks: {
      defaultLanguage: {
        script: 'ts',
      },
    },
  },
  test: {
    cypress: true,
  },
  ignores: [
    'src/services/api.ts',
  ],
})

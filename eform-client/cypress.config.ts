import { defineConfig } from 'cypress';

export default defineConfig({
  e2e: {
    video: true, // Enable video recording for debugging
    viewportWidth: 2560,
    viewportHeight: 1440,
    setupNodeEvents(on, config) {
      // Custom task to output logs to terminal/CI
      on('task', {
        log(message) {
          console.log(message);
          return null;
        },
      });
      return config;
    },
  },
});

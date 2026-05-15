import { defineConfig } from '@playwright/test';

export default defineConfig({
  testDir: 'playwright/e2e',
  workers: 1,
  // Explicit fail-fast — do NOT raise this. Shard b's specs form a chain of
  // cumulative state mutations on a shared worker / week range with no
  // per-spec DB reset; retrying through a partial failure stacks writes and
  // produces confusing cascade failures downstream (e.g. expected 88.36, got
  // 156.38 from a doubled WorkingHours pass). The actual fix for the
  // underlying flake source belongs in test isolation (worker isolation OR
  // a test-only reset endpoint), not in masking via retries.
  retries: 0,
  use: {
    baseURL: 'http://localhost:4200',
    viewport: { width: 1920, height: 1080 },
    video: 'on',
    screenshot: 'only-on-failure',
  },
  reporter: [
    ['html'],
    ['json', { outputFile: 'playwright-results/results.json' }],
  ],
  timeout: 120000,
});

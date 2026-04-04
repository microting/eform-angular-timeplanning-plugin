import { test, expect } from '@playwright/test';

// create canary in a coal mine test asserting true
test('asserts true', () => {
  expect(true).toBe(true); // this will pass
});

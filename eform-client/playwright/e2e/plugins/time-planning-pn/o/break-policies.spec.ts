// Temporarily disabled tests — ported from Cypress as-is (all commented out in source)
import { test, expect } from '@playwright/test';
import { LoginPage } from '../../../Page objects/Login.page';

// test.describe('Break Policies Tests', () => {
//   test.beforeEach(async ({ page }) => {
//     await page.goto('http://localhost:4200');
//     await new LoginPage(page).login();
//   });

//   const navigateToBreakPolicies = async (page) => {
//     // Navigate using menu (Danish locale: Timeregistrering -> Pausepolitikker)
//     await expect(page.locator('#spinner-animation')).toHaveCount(0);
//     await page.locator('text=Timeregistrering').click();
//     await page.waitForTimeout(500);
//     await page.locator('text=Pausepolitikker').click();
//     await page.waitForTimeout(500);
//   };

//   test('should navigate to break policies page', async ({ page }) => {
//     await navigateToBreakPolicies(page);
//     await expect(page).toHaveURL(/break-policies/);
//   });

//   test('should display break policies list', async ({ page }) => {
//     await navigateToBreakPolicies(page);
//     await expect(page.locator('mtx-grid')).toBeVisible();
//   });

//   test('should open create modal', async ({ page }) => {
//     await navigateToBreakPolicies(page);
//     await page.locator('button', { hasText: 'Create Break Policy' }).click();
//     await page.waitForTimeout(500);
//     await expect(page.locator('text=Create Break Policy')).toBeVisible();
//     await expect(page.locator('input[formcontrolname="name"]')).toBeVisible();
//   });

//   test('should create new break policy', async ({ page }) => {
//     await navigateToBreakPolicies(page);
//     await page.locator('button', { hasText: 'Create Break Policy' }).click();
//     await page.waitForTimeout(500);
//     await page.locator('input[formcontrolname="name"]').fill('Test Break Policy');
//     await page.locator('button', { hasText: 'Create' }).click();
//     await page.waitForTimeout(1000);
//     await expect(page.locator('text=Test Break Policy')).toBeVisible();
//   });

//   test('should edit break policy', async ({ page }) => {
//     await navigateToBreakPolicies(page);
//     await page.locator('mtx-grid').locator('button[mattooltip="Edit"]').first().click();
//     await page.waitForTimeout(500);
//     await expect(page.locator('text=Edit Break Policy')).toBeVisible();
//     await page.locator('input[formcontrolname="name"]').fill('Updated Break Policy');
//     await page.locator('button', { hasText: 'Save' }).click();
//     await page.waitForTimeout(1000);
//     await expect(page.locator('text=Updated Break Policy')).toBeVisible();
//   });

//   test('should delete break policy', async ({ page }) => {
//     await navigateToBreakPolicies(page);
//     const initialCount = await page.locator('mtx-grid tbody tr').count();
//     await page.locator('mtx-grid').locator('button[mattooltip="Delete"]').first().click();
//     await page.waitForTimeout(500);
//     await expect(page.locator('text=Are you sure')).toBeVisible();
//     await page.locator('button', { hasText: 'Delete' }).click();
//     await page.waitForTimeout(1000);
//     await expect(page.locator('mtx-grid tbody tr')).toHaveCount(initialCount - 1);
//   });

//   test('should validate required fields', async ({ page }) => {
//     await navigateToBreakPolicies(page);
//     await page.locator('button', { hasText: 'Create Break Policy' }).click();
//     await page.waitForTimeout(500);
//     await expect(page.locator('button', { hasText: 'Create' })).toBeDisabled();
//     await page.locator('input[formcontrolname="name"]').fill('Test');
//     await expect(page.locator('button', { hasText: 'Create' })).not.toBeDisabled();
//   });
// });

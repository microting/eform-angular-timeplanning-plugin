import { test, expect, Page, Request } from '@playwright/test';
import { LoginPage } from '../../../Page objects/Login.page';

/**
 * Regression guard for #1744: the assigned-site dialog re-opened by itself after
 * logging out and in again.
 *
 * onFirstColumnClick() subscribed to the admin flag in the store, which never
 * completes, so every click left a live subscription behind. Logout flips the flag
 * to false, the next login flips it back to true, and that callback fetched the
 * worker's assigned site and opened the dialog on top of whatever page login landed
 * on. The leaked callback fires during login itself, so its GET is issued before the
 * landing page renders; counting those GETs after logout is the deterministic check.
 */

const BASE_URL = 'http://localhost:4200';
const UI_TIMEOUT = 15000;
const API_TIMEOUT = 30000;

const isAssignedSiteGet = (request: Request) =>
  request.url().includes('/api/time-planning-pn/settings/assigned-sites')
  && request.url().includes('siteId=')
  && request.method() === 'GET';

async function openDashboard(page: Page) {
  const index = page.waitForResponse(
    r => r.url().includes('/api/time-planning-pn/plannings/index') && r.request().method() === 'POST',
    { timeout: API_TIMEOUT });
  await page.goto(`${BASE_URL}/plugins/time-planning-pn/planning`);
  await index;
}

test.describe('Time Planning - assigned-site dialog stays closed after logout and login', () => {
  test('a dialog opened and closed before logout does not re-open on the next login', async ({ page }) => {
    // Two logins (each waits for the landing page) plus one dashboard load.
    test.setTimeout(240000);

    const loginPage = new LoginPage(page);
    await page.goto(BASE_URL);
    await loginPage.login();
    await openDashboard(page);

    const dialog = page.locator('mat-dialog-container');
    const assignedSite = page.waitForResponse(
      r => isAssignedSiteGet(r.request()), { timeout: API_TIMEOUT });
    await page.locator('#firstColumn0').click();
    await assignedSite;
    await expect(dialog).toBeVisible({ timeout: UI_TIMEOUT });
    await dialog.locator('#cancelButton').click();
    await expect(dialog).toHaveCount(0, { timeout: UI_TIMEOUT });

    let assignedSiteGetsAfterLogout = 0;
    page.on('request', r => {
      if (isAssignedSiteGet(r)) {
        assignedSiteGetsAfterLogout++;
      }
    });

    await page.locator('#sign-out-dropdown').click();
    const signOut = page.locator('#sign-out');
    await signOut.waitFor({ state: 'visible', timeout: UI_TIMEOUT });
    await signOut.click();
    await page.locator('#loginBtn').waitFor({ state: 'visible', timeout: UI_TIMEOUT });

    // Waits for the landing page (#newEFormBtn) before it returns.
    await loginPage.login();

    expect(assignedSiteGetsAfterLogout, 'no assigned-site GET fired by the login itself').toBe(0);
    await expect(page.locator('app-assigned-site-dialog')).toHaveCount(0);
    await expect(dialog).toHaveCount(0);
  });
});

import { test, expect, Page } from '@playwright/test';
import { LoginPage } from '../../../Page objects/Login.page';

const BASE_URL = 'http://localhost:4200';

// The Download Excel modal opens on whatever the dashboard is showing — same period,
// same tags — and says how much the export covers. Changing a filter inside the modal
// only changes that export, and the count line follows it without a reload.
// Seeded via API: one fresh tag on exactly one worker, so the filtered count is 1.
test.describe.serial('Time Planning - download excel modal inherits the page filters', () => {
  const tagName = `ExportTag-${Date.now()}-${Math.random().toString(36).substring(7)}`;
  let tagId = 0;
  let siteName = '';

  async function apiHeaders(page: Page): Promise<{ Authorization: string }> {
    const res = await page.request.post(`${BASE_URL}/api/auth/token`, {
      form: { username: 'admin@admin.com', password: 'secretpassword', grant_type: 'password' },
    });
    const json = await res.json();
    return { Authorization: `Bearer ${json.model.accessToken}` };
  }

  async function openDashboard(page: Page) {
    const indexResponse = page.waitForResponse(
      r => r.url().includes('/api/time-planning-pn/plannings/index') && r.request().method() === 'POST');
    await page.goto(`${BASE_URL}/plugins/time-planning-pn/planning`);
    await indexResponse;
    await page.waitForTimeout(1000);
  }

  async function openDownloadExcelDialog(page: Page) {
    await page.locator('#file-export-excel').click();
    await page.locator('mat-dialog-container').waitFor({ state: 'visible', timeout: 10000 });
  }

  /** "14 workers · 30 days" → 14, whatever the locale calls a worker. */
  async function workersInScope(page: Page): Promise<number> {
    const line = page.locator('mat-dialog-container #downloadExcelScope');
    await expect(line).toBeVisible({ timeout: 10000 });
    const text = (await line.textContent()) || '';
    const match = text.match(/\d+/);
    expect(match, `a worker count in "${text}"`).toBeTruthy();
    return Number(match![0]);
  }

  test.beforeEach(async ({ page }) => {
    await page.goto(BASE_URL);
    await new LoginPage(page).login();
    await page.waitForTimeout(2000);
  });

  test('seed: a fresh tag on exactly one worker', async ({ page }) => {
    test.setTimeout(120000);
    const headers = await apiHeaders(page);

    await page.request.post(`${BASE_URL}/api/tags`, { headers, data: { id: 0, name: tagName } });
    const tagsRes = await page.request.get(`${BASE_URL}/api/tags/index`, { headers });
    const tag = ((await tagsRes.json()).model || []).find((t: any) => t.name === tagName);
    expect(tag, `tag ${tagName} created`).toBeTruthy();
    tagId = tag.id;

    // The count is only meaningful against several workers, so the seed says so out loud.
    const sitesRes = await page.request.get(`${BASE_URL}/api/time-planning-pn/settings/sites`, { headers });
    const tpSites = (await sitesRes.json()).model || [];
    expect(tpSites.length, 'more than one assigned site').toBeGreaterThan(1);
    siteName = tpSites[0].siteName;

    // Core id differs from the time-planning siteId/uid; SiteModel.tags is a number[].
    const coreRes = await page.request.get(`${BASE_URL}/api/sites/pairing`, { headers });
    const coreSite = ((await coreRes.json()).model || []).find((s: any) => s.siteName === siteName);
    expect(coreSite, `core site named ${siteName}`).toBeTruthy();
    const putRes = await page.request.put(`${BASE_URL}/api/sites`, {
      headers,
      data: { id: coreSite.id, siteName: coreSite.siteName, tags: [...(coreSite.tags || []), tagId] },
    });
    expect(putRes.status()).toBe(200);
  });

  test('opens with the period and the tags the page already has', async ({ page }) => {
    test.setTimeout(120000);
    await openDashboard(page);

    // Filter the page by the seeded tag first — that is what the modal must inherit.
    const filteredIndex = page.waitForRequest(
      r => r.url().includes('/api/time-planning-pn/plannings/index') && r.method() === 'POST'
        && ((r.postDataJSON()?.tagIds || []) as number[]).includes(tagId));
    await page.locator('#planningTags').click();
    await page.locator('.ng-option').filter({ hasText: tagName }).first().click();
    await filteredIndex;
    await page.waitForTimeout(1000);

    const pageRange = page.locator('#workingHoursRange input.workingHoursRange');
    const pageFrom = await pageRange.nth(0).inputValue();
    const pageTo = await pageRange.nth(1).inputValue();

    await openDownloadExcelDialog(page);
    const dialog = page.locator('mat-dialog-container');

    await expect(dialog.locator('#downloadExcelTags')).toContainText(tagName);
    const dialogRange = dialog.locator('#workingHoursRange input.workingHoursRange');
    expect(await dialogRange.nth(0).inputValue()).toBe(pageFrom);
    expect(await dialogRange.nth(1).inputValue()).toBe(pageTo);

    // One worker carries the seeded tag.
    expect(await workersInScope(page)).toBe(1);
  });

  test('the count follows a tag picked inside the modal', async ({ page }) => {
    test.setTimeout(120000);
    await openDashboard(page);
    await openDownloadExcelDialog(page);

    const unfiltered = await workersInScope(page);
    expect(unfiltered).toBeGreaterThan(1);

    await page.locator('mat-dialog-container #downloadExcelTags').click();
    await page.locator('.ng-option').filter({ hasText: tagName }).first().click();

    // Local recount, no reload: the line is already right on the next paint.
    await expect(page.locator('mat-dialog-container #downloadExcelScope'))
      .not.toHaveText(new RegExp(`^\\s*${unfiltered}\\D`));
    expect(await workersInScope(page)).toBe(1);
  });
});

import { test, expect, Page } from '@playwright/test';
import { LoginPage } from '../../../Page objects/Login.page';

const BASE_URL = 'http://localhost:4200';

// Dashboard "Navn" column enrichment: every row shows the site's tag chips
// and a fixed-order settings icon strip (pay rules, mobile registration,
// over midnight, auto break, 1-minute intervals, extra shifts).
// Seeded entirely via API: a fresh tag is put on the first assigned site and
// its assigned-site settings are set to a known combination.
test.describe.serial('Time Planning - dashboard row tags & settings strip', () => {
  const tagName = `RowTag-${Date.now()}-${Math.random().toString(36).substring(7)}`;
  let tagId = 0;
  let siteName = '';

  async function apiHeaders(page: Page): Promise<{ Authorization: string }> {
    const res = await page.request.post(`${BASE_URL}/api/auth/token`, {
      form: { username: 'admin@admin.com', password: 'secretpassword', grant_type: 'password' },
    });
    const json = await res.json();
    return { Authorization: `Bearer ${json.model.accessToken}` };
  }

  test.beforeEach(async ({ page }) => {
    await page.goto(BASE_URL);
    await new LoginPage(page).login();
    await page.waitForTimeout(2000);
  });

  test('seed: tag on first assigned site + known settings combination', async ({ page }) => {
    test.setTimeout(120000);
    const headers = await apiHeaders(page);

    // 1. Create the tag and resolve its id.
    await page.request.post(`${BASE_URL}/api/tags`, { headers, data: { id: 0, name: tagName } });
    const tagsRes = await page.request.get(`${BASE_URL}/api/tags/index`, { headers });
    const tag = ((await tagsRes.json()).model || []).find((t: any) => t.name === tagName);
    expect(tag, `tag ${tagName} created`).toBeTruthy();
    tagId = tag.id;

    // 2. First time-registration site shown on the dashboard.
    const sitesRes = await page.request.get(`${BASE_URL}/api/time-planning-pn/settings/sites`, { headers });
    const tpSite = ((await sitesRes.json()).model || [])[0];
    expect(tpSite, 'at least one assigned site').toBeTruthy();
    siteName = tpSite.siteName;

    // 3. Attach the tag via the core sites API (core id differs from the
    //    time-planning siteId/uid; SiteModel.tags is a number[]).
    const coreRes = await page.request.get(`${BASE_URL}/api/sites/pairing`, { headers });
    const coreSite = ((await coreRes.json()).model || []).find((s: any) => s.siteName === siteName);
    expect(coreSite, `core site named ${siteName}`).toBeTruthy();
    const existingTagIds: number[] = coreSite.tags || [];
    await page.request.put(`${BASE_URL}/api/sites`, {
      headers,
      data: { id: coreSite.id, siteName: coreSite.siteName, tags: [...existingTagIds, tagId] },
    });

    // 4. Known settings combination (GET → mutate → PUT keeps other fields).
    const assignedRes = await page.request.get(
      `${BASE_URL}/api/time-planning-pn/settings/assigned-sites?siteId=${tpSite.siteId}`, { headers });
    const model = (await assignedRes.json()).model;
    model.allowPersonalTimeRegistration = true;
    model.usePunchClock = true;
    model.allowAcceptOfPlannedHours = false;
    model.overMidnight = true;
    model.autoBreakCalculationActive = false;
    const putRes = await page.request.put(`${BASE_URL}/api/time-planning-pn/settings/assigned-site`, {
      headers, data: model,
    });
    expect(putRes.status()).toBe(200);
  });

  test('shows the tag chip and correct icon states on the seeded row', async ({ page }) => {
    test.setTimeout(120000);
    const indexResponse = page.waitForResponse(
      r => r.url().includes('/api/time-planning-pn/plannings/index') && r.request().method() === 'POST');
    await page.goto(`${BASE_URL}/plugins/time-planning-pn/planning`);
    await indexResponse;
    await page.waitForTimeout(1000);

    const row = page.locator('.first-column').filter({ hasText: siteName }).first();
    await expect(row).toBeVisible({ timeout: 30000 });

    // Tag chip rendered from the row payload
    await expect(row.locator('.row-tags')).toContainText(tagName);

    // Fixed-order settings strip with the seeded states
    await expect(row.locator('.settings-strip')).toBeVisible();
    await expect(row.locator('[id^=settingMobileReg]')).toHaveClass(/active/);
    await expect(row.locator('[id^=settingOverMidnight]')).toHaveClass(/active/);
    await expect(row.locator('[id^=settingAutoBreak]')).toHaveClass(/off/);
  });

  test('clicking a tag chip applies the Etiketter filter without opening the edit dialog', async ({ page }) => {
    test.setTimeout(120000);
    const firstIndex = page.waitForResponse(
      r => r.url().includes('/api/time-planning-pn/plannings/index') && r.request().method() === 'POST');
    await page.goto(`${BASE_URL}/plugins/time-planning-pn/planning`);
    await firstIndex;
    await page.waitForTimeout(1000);

    const row = page.locator('.first-column').filter({ hasText: siteName }).first();
    await expect(row).toBeVisible({ timeout: 30000 });

    const filteredIndex = page.waitForRequest(
      r => r.url().includes('/api/time-planning-pn/plannings/index') && r.method() === 'POST'
        && ((r.postDataJSON()?.tagIds || []) as number[]).includes(tagId));
    await row.locator('.row-tags mat-chip').filter({ hasText: tagName }).first().click();

    // The reload must carry the clicked tag id…
    await filteredIndex;
    await page.waitForTimeout(1000);
    // …the chip click must not have opened the assigned-site edit dialog…
    await expect(page.locator('mat-dialog-container')).toHaveCount(0);
    // …and the filtered list still contains the seeded row.
    await expect(page.locator('.first-column').filter({ hasText: siteName }).first()).toBeVisible();
  });
});

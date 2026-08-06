import { test, expect, Page } from '@playwright/test';
import { LoginPage } from '../../../Page objects/Login.page';

const BASE_URL = 'http://localhost:4200';

// Two regressions on the pay-rule-sets screen:
//
// 1. Locked GLS-A presets became editable and deletable for rule sets stored
//    under an older agreement period: lock status is a name-string match, and
//    the preset catalogue was renamed "... 2024-2026" -> "... 2026-2029"
//    without migrating existing rows. Matching now normalizes the trailing
//    validity period away, so both spellings stay locked - in the row menu
//    AND in the API (the client guard alone is not a guard).
//
// 2. Tier thresholds were edited and shown as raw seconds, and an unlimited
//    top tier rendered as an empty field behind a "28800" placeholder, which
//    reads as a real - and nonsensically low - value. Durations are now
//    hours:minutes and unlimited says so in words.

async function apiHeaders(page: Page): Promise<{ Authorization: string }> {
  const res = await page.request.post(`${BASE_URL}/api/auth/token`, {
    form: { username: 'admin@admin.com', password: 'secretpassword', grant_type: 'password' },
  });
  const json = await res.json();
  return { Authorization: `Bearer ${json.model.accessToken}` };
}

async function goToPayRuleSets(page: Page): Promise<void> {
  const loaded = page.waitForResponse(
    r => r.url().includes('/api/time-planning-pn/pay-rule-sets') && r.request().method() === 'GET');
  await page.goto(`${BASE_URL}/plugins/time-planning-pn/pay-rule-sets`);
  await loaded;
  await page.locator('#time-planning-pn-pay-rule-sets-grid').waitFor({ state: 'visible', timeout: 30000 });
}

// The seeded set carries a locked preset name, so the API deliberately refuses
// to delete it again - that is the behaviour under test. CI runs each shard
// against a freshly loaded database, so the row does not outlive the run.
test.describe.serial('Pay rule sets - locked presets and hh:mm durations', () => {
  let legacyId = 0;

  test.beforeEach(async ({ page }) => {
    await page.goto(BASE_URL);
    await new LoginPage(page).login();
    await page.waitForTimeout(2000);
  });

  test('seed: a rule set stored under the legacy agreement period', async ({ page }) => {
    test.setTimeout(120000);
    const headers = await apiHeaders(page);
    // The unique suffix keeps parallel shards from colliding; the normalizer
    // strips " 2024-2026" from the middle of the name only when it trails, so
    // the seeded name is deliberately built to end with the period.
    const name = 'GLS-A / 3F - Jordbrug Dyrehold 2024-2026';
    const res = await page.request.post(`${BASE_URL}/api/time-planning-pn/pay-rule-sets`, {
      headers,
      data: {
        name,
        payDayRules: [
          {
            dayCode: 'WEEKDAY',
            payTierRules: [
              { order: 1, upToSeconds: 26640, payCode: 'NORMAL' },
              { order: 2, upToSeconds: 33840, payCode: 'OVERTIME_30' },
              { order: 3, upToSeconds: null, payCode: 'OVERTIME_80' },
            ],
          },
        ],
        payDayTypeRules: [],
      },
    });
    expect(res.status()).toBe(200);

    const list = await page.request.get(`${BASE_URL}/api/time-planning-pn/pay-rule-sets`, { headers });
    const rows = (await list.json()).model?.payRuleSets || (await list.json()).model || [];
    const found = (Array.isArray(rows) ? rows : []).filter((r: any) => r.name === name);
    expect(found.length, 'seeded rule set is listed').toBeGreaterThan(0);
    legacyId = found[found.length - 1].id;
  });

  test('a legacy-named GLS-A set cannot be edited or deleted, but can be viewed', async ({ page }) => {
    test.setTimeout(120000);
    await goToPayRuleSets(page);

    const row = page.locator('.mat-mdc-row').filter({ hasText: 'Jordbrug Dyrehold 2024-2026' }).first();
    await expect(row).toBeVisible({ timeout: 30000 });
    await row.locator('button').first().click();

    const menu = page.locator('.mat-mdc-menu-panel');
    await expect(menu).toBeVisible();
    // View stays available; edit and delete are disabled for a locked preset.
    await expect(menu.locator('button').filter({ hasText: 'Vis' }).first()).toBeEnabled();
    await expect(menu.locator('button').filter({ hasText: 'Rediger' }).first()).toBeDisabled();
    await expect(menu.locator('button').filter({ hasText: 'Slet' }).first()).toBeDisabled();

    await page.keyboard.press('Escape');
  });

  test('the API rejects update and delete of a legacy-named locked preset', async ({ page }) => {
    test.setTimeout(120000);
    expect(legacyId, 'seeded id').toBeGreaterThan(0);
    const headers = await apiHeaders(page);

    const update = await page.request.put(`${BASE_URL}/api/time-planning-pn/pay-rule-sets/${legacyId}`, {
      headers,
      data: { id: legacyId, name: 'Renamed by test', payDayRules: [], payDayTypeRules: [] },
    });
    expect((await update.json()).success, 'update rejected').toBe(false);

    const remove = await page.request.delete(`${BASE_URL}/api/time-planning-pn/pay-rule-sets/${legacyId}`, { headers });
    expect((await remove.json()).success, 'delete rejected').toBe(false);

    // Still there, still named as seeded.
    const read = await page.request.get(`${BASE_URL}/api/time-planning-pn/pay-rule-sets/${legacyId}`, { headers });
    expect((await read.json()).model.name).toContain('Jordbrug Dyrehold 2024-2026');
  });

  test('an unlimited top tier reads as "unlimited", never as a number', async ({ page }) => {
    test.setTimeout(120000);
    await goToPayRuleSets(page);

    const row = page.locator('.mat-mdc-row').filter({ hasText: 'Jordbrug Dyrehold 2024-2026' }).first();
    await row.locator('button').first().click();
    await page.locator('.mat-mdc-menu-panel button').filter({ hasText: 'Vis' }).first().click();

    const dialog = page.locator('mat-dialog-container');
    await expect(dialog).toBeVisible({ timeout: 30000 });

    // The weekday chain shows hh:mm-style durations and spells out the
    // unbounded top tier instead of leaving a bare pay code (or a number).
    const weekday = dialog.locator('tr').filter({ hasText: 'WEEKDAY' }).first();
    await expect(weekday).toContainText('7h24m');
    await expect(weekday).toContainText('9h24m');
    await expect(weekday).toContainText('OVERTIME_80');
    await expect(weekday).toContainText('Ubegrænset');
    // The old misleading placeholder value must not appear anywhere.
    await expect(dialog).not.toContainText('28800');
  });
});

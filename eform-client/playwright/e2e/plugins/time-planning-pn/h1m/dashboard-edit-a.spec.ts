import { test, expect, Page } from '@playwright/test';
import { LoginPage } from '../../../Page objects/Login.page';

/**
 * h1m variant of `h/dashboard-edit-a.spec.ts`: the paid-out-flex decimal
 * round-trip suite, exercised under the `UseOneMinuteIntervals = true` code
 * path. The shard's `post-migration.sql` flips the flag on every active
 * assigned site, so the legacy in-spec setup from `h/` runs against the
 * `minutesGap = 1` timepicker context.
 *
 * Why no shift-filling here (deviation from b1m / c1m / d1m / e1m):
 *
 *   The legacy `h/` shard saves successfully WITHOUT filling any shift
 *   inputs — `#paidOutFlex` is a numeric field on the workday-entity
 *   dialog and the shift validators only fire when start/stop values are
 *   PRESENT (empty = no error, saveButton stays enabled). An earlier
 *   iteration of h1m tried to apply the multishift-shape pattern and
 *   timed out on `[data-testid="plannedStartOfShift3"]` because shifts
 *   3-5 are gated behind `thirdShiftActive / fourthShiftActive /
 *   fifthShiftActive` per-site flags that the post-migration patch
 *   doesn't touch (only `UseOneMinuteIntervals` is flipped). The b1m
 *   spec activates those flags via a `#firstColumn3` settings-dialog
 *   dance — but h's flow goes through `#cell0_0` (a different dialog),
 *   so the b1m setup doesn't transplant cleanly. Rather than recreating
 *   the multi-shift activation dance for a spec that has nothing to do
 *   with shift inputs, h1m mirrors the legacy `h/` spec's empty-shift
 *   approach and gets its flag-on coverage incidentally (the flag is
 *   active on the assigned site for the whole session).
 *
 * Display assertion note: `#paidOutFlex` is a NUMERIC input — its
 * `toHaveValue` assertions stay as `'1.2'`, `'0'` etc. (no `HH:mm:ss`
 * formatting) since the field has nothing to do with time-of-day.
 *
 * Stateful test ordering: the legacy `h/` spec has the same cross-test
 * dependency — test N reads back the value test N-1 saved. h1m preserves
 * that order exactly. If a single test fails the chain breaks; that's
 * intentional and matches `h/`.
 *
 * Defensive timing additions over the legacy `h/` spec — informed by the
 * (reverted) g1m shard where the same `#workingHoursSite + 'ac ad'`
 * filter pattern caused a "element detached from DOM" hang on the cell
 * click. h1m waits for the spinner AND a 500ms settle after the site
 * filter applies, then gates the dialog open on `#paidOutFlex` becoming
 * visible. `#saveButton` is asserted `toBeEnabled` before the click —
 * never `force: true`.
 */

async function waitForSpinner(page: Page) {
  if (await page.locator('.overlay-spinner').count() > 0) {
    await page.locator('.overlay-spinner').waitFor({ state: 'hidden', timeout: 30000 });
  }
}

test.describe('Dashboard edit values (h1m, flag-on, paid-out-flex)', () => {
  test.beforeEach(async ({ page }) => {
    await page.goto('http://localhost:4200');
    await new LoginPage(page).login();
    await page.locator('mat-nested-tree-node').filter({ hasText: 'Timeregistrering' }).click();

    const indexUpdatePromise = page.waitForResponse(
      r => r.url().includes('/api/time-planning-pn/plannings/index') && r.request().method() === 'POST'
    );

    await page.locator('mat-tree-node').filter({ hasText: 'Dashboard' }).click();
    await indexUpdatePromise;
    await waitForSpinner(page);

    await page.locator('#workingHoursSite').click();
    await page.locator('.ng-option').filter({ hasText: 'ac ad' }).click();
    // Wait for site-filter re-index to finish before opening the cell — the
    // legacy h spec races a 500ms timeout here, but the (reverted) g1m
    // shard showed that the site-filter trigger detaches the cell row mid-
    // click on flag-on. Wait for the spinner before reaching for #cell0_0.
    await waitForSpinner(page);
    await page.waitForTimeout(500);

    await page.locator('#cell0_0').click();
    // Dialog open is async — gate on `#paidOutFlex` becoming visible.
    await expect(page.locator('#paidOutFlex')).toBeVisible({ timeout: 10000 });
  });

  test('should set paid out flex value', async ({ page }) => {
    await page.locator('#paidOutFlex').clear();
    await page.locator('#paidOutFlex').fill('1.2');
  });

  test('should accepts decimal values with dot', async ({ page }) => {
    await expect(page.locator('#paidOutFlex')).toHaveValue('1.2');
    await page.locator('#paidOutFlex').clear();
    await page.locator('#paidOutFlex').fill('1,2');
  });

  test('should accepts decimal values with comma', async ({ page }) => {
    await expect(page.locator('#paidOutFlex')).toHaveValue('1.2');
    await page.locator('#paidOutFlex').clear();
    await page.locator('#paidOutFlex').fill('1,2');
  });

  test('should accepts whole numbers', async ({ page }) => {
    await expect(page.locator('#paidOutFlex')).toHaveValue('1.2');
    await page.locator('#paidOutFlex').clear();
    await page.locator('#paidOutFlex').fill('0');
  });

  test.afterEach(async ({ page }) => {
    // Wait for saveButton to be enabled BEFORE clicking — never `force: true`.
    await expect(page.locator('#saveButton')).toBeEnabled({ timeout: 10000 });
    const savePromise = page.waitForResponse(
      r => r.url().includes('/api/time-planning-pn/plannings/') && r.request().method() === 'PUT'
    );
    await page.locator('#saveButton').click();
    await savePromise;
    await page.waitForTimeout(1000);
  });
});

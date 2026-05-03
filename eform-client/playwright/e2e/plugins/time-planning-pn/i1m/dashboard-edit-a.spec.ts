import { test, expect, Page } from '@playwright/test';
import { LoginPage } from '../../../Page objects/Login.page';
import { OFFGRID_TIMES_I1M } from '../../../../helpers/one-minute-times';

/**
 * i1m variant of `i/dashboard-edit-a.spec.ts` — the "edit time planned in
 * last week" single-shift round-trip test, exercised under the
 * `UseOneMinuteIntervals = true` code path. The shard's `post-migration.sql`
 * flips the flag on every active assigned site, so the legacy in-spec
 * setup from `i/` runs against the `minutesGap = 1` timepicker context.
 *
 * Why "shifts 1 only" shape (deviation from b1m / c1m / d1m / e1m):
 *
 *   The legacy `i/` shard fills ONLY shift 1 (`plannedStartOfShift1` +
 *   `plannedEndOfShift1`) — no actual times, no shifts 2-5. Earlier 1m
 *   shards (b1m / c1m / d1m / e1m / g1m) clone the multishift-shape
 *   pattern by filling all five shifts ascending, but those source specs
 *   already exercise shifts 3-5 because their seed has the per-site
 *   `thirdShiftActive / fourthShiftActive / fifthShiftActive` flags set.
 *   The `i/` source spec doesn't touch shifts 2-5 (the inputs aren't
 *   even rendered in the dialog by default), so cloning in the same
 *   shape — single shift 1 — is the correct call. Forcing a five-shift
 *   block here would time out on `[data-testid="plannedStartOfShift3"]`
 *   exactly the way an earlier (reverted) g1m attempt did.
 *
 * Off-grid times: `OFFGRID_TIMES_I1M` defines `07:14` → `15:14` (both
 * minute-14, non-multiple of 5) so the flag-on `minutesGap=1` rendering
 * is the only way the picker can land on these values. The resulting
 * `planHours` is a clean integer `8` (480 min) — matching the legacy
 * `i` shard's `planHours='8'` assertion exactly. See helper for math.
 *
 * Save gating: `await expect(page.locator('#saveButton'))
 * .toBeEnabled({ timeout: 10000 });` BEFORE `click()` — never
 * `force: true`. The shift validators only fire when start AND stop
 * are both non-empty AND form a valid range; with `07:14 → 15:14`
 * filled, the saveButton is enabled.
 *
 * Display assertions stay as `HH:mm` (not `HH:mm:ss`) — the
 * `[data-testid="plannedStartOfShift${n}"]` input control reports its
 * value as `HH:mm`. This matches the b1m / c1m / d1m / e1m precedent.
 *
 * `afterEach` cleanup: mirrors the legacy `i/` spec's row-level delete
 * dance — open the dialog again and click the delete-icon next to each
 * planned shift field that has a value, then save. This keeps the seed
 * row clean for the next test (and for adjacent shards that share the
 * same baseline data).
 */

async function waitForSpinner(page: Page) {
  if (await page.locator('.overlay-spinner').count() > 0) {
    await page.locator('.overlay-spinner').waitFor({ state: 'hidden', timeout: 30000 });
  }
}

/**
 * Position-based clock-face picker. Identical helper to the b1m / c1m /
 * d1m / e1m / f1m / g1m specs — works uniformly for h=0 (break and
 * midnight times) unlike rotateZ-selector strategies that fail on the
 * inner-ring `00` position.
 */
async function pickTime(page: Page, timeStr: string) {
  const [hourStr, minuteStr] = timeStr.split(':');
  const h = parseInt(hourStr, 10);
  const m = parseInt(minuteStr, 10);

  const cx = 145, cy = 145;

  const hourFace = page.locator('.clock-face');
  await hourFace.first().waitFor({ state: 'visible', timeout: 5000 });
  const hourAngle = (h % 12) * 30;
  const hourR = (h === 0 || h > 12) ? 60 : 100;
  const hourRad = hourAngle * Math.PI / 180;
  await hourFace.first().click({
    position: {
      x: Math.round(cx + hourR * Math.sin(hourRad)),
      y: Math.round(cy - hourR * Math.cos(hourRad)) + (Math.abs(Math.cos(hourRad)) < 0.01 ? 1 : 0),
    },
  });

  await page.waitForTimeout(500);
  const minuteFace = page.locator('.clock-face');
  await minuteFace.first().waitFor({ state: 'visible', timeout: 5000 });
  const minuteAngle = m * 6;
  const minuteR = 100;
  const minuteRad = minuteAngle * Math.PI / 180;
  await minuteFace.first().click({
    position: {
      x: Math.round(cx + minuteR * Math.sin(minuteRad)),
      y: Math.round(cy - minuteR * Math.cos(minuteRad)) + (Math.abs(Math.cos(minuteRad)) < 0.01 ? 1 : 0),
    },
  });

  await page.waitForTimeout(500);
  await page.locator('.timepicker-button span').filter({ hasText: 'Ok' }).click();
  // Wait for the timepicker overlay to fully close before the next pick.
  await page.locator('.cdk-overlay-backdrop').waitFor({ state: 'hidden', timeout: 10000 }).catch(() => {});
  await page.waitForTimeout(500);
}

/** Open the timepicker for `selector` and pick `timeStr`. */
async function setTimepickerValue(page: Page, selector: string, timeStr: string) {
  await page.locator(`[data-testid="${selector}"]`).click();
  await pickTime(page, timeStr);
}

test.describe('Dashboard edit values (i1m, flag-on, off-grid single-shift)', () => {
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
    // legacy i spec races a 500ms timeout, but the (reverted) g1m shard
    // showed that the site-filter trigger detaches the cell row mid-click
    // on flag-on. Wait for the spinner before reaching for #cell0_0.
    await waitForSpinner(page);
    await page.waitForTimeout(500);
  });

  test('should edit time planned in last week', async ({ page }) => {
    // Open the first day cell (opens a MatDialog).
    await page.locator('#cell0_0').click();
    // Wait for mtx-grid shift template to render inside the dialog.
    await page.locator('[data-testid="plannedStartOfShift1"]').waitFor({ state: 'visible', timeout: 15000 });

    // Set planned shift 1 times (off-grid 07:14 → 15:14 ⇒ 480 min ⇒ 8 h).
    await setTimepickerValue(page, 'plannedStartOfShift1', OFFGRID_TIMES_I1M.shift1Start);
    await expect(page.locator('[data-testid="plannedStartOfShift1"]')).toHaveValue(OFFGRID_TIMES_I1M.shift1Start, { timeout: 5000 });
    await setTimepickerValue(page, 'plannedEndOfShift1', OFFGRID_TIMES_I1M.shift1End);
    await expect(page.locator('[data-testid="plannedEndOfShift1"]')).toHaveValue(OFFGRID_TIMES_I1M.shift1End, { timeout: 5000 });

    // Verify plan hours calculated correctly (15:14 - 07:14 = 8).
    await expect(page.locator('#planHours')).toHaveValue(OFFGRID_TIMES_I1M.planHours, { timeout: 5000 });

    // Wait for saveButton to be enabled BEFORE clicking — never `force: true`.
    await expect(page.locator('#saveButton')).toBeEnabled({ timeout: 10000 });

    const savePromise = page.waitForResponse(
      r => r.url().includes('/api/time-planning-pn/plannings/') && r.request().method() === 'PUT'
    );
    await page.locator('#saveButton').click();
    await savePromise;
    // Wait for dialog to close and table to stabilize.
    await page.locator('.cdk-overlay-backdrop').waitFor({ state: 'hidden', timeout: 10000 }).catch(() => {});
    await waitForSpinner(page);
    await page.waitForTimeout(2000);

    // Reopen the dialog and verify values persisted.
    await page.locator('#cell0_0').click();
    // Wait for the shift template to fully render in the new dialog.
    await page.locator('[data-testid="plannedStartOfShift1"]').waitFor({ state: 'visible', timeout: 15000 });
    await page.waitForTimeout(1000);

    await expect(page.locator('[data-testid="plannedStartOfShift1"]')).toHaveValue(OFFGRID_TIMES_I1M.shift1Start, { timeout: 10000 });
    await expect(page.locator('[data-testid="plannedEndOfShift1"]')).toHaveValue(OFFGRID_TIMES_I1M.shift1End, { timeout: 5000 });
    await expect(page.locator('#planHours')).toHaveValue(OFFGRID_TIMES_I1M.planHours, { timeout: 5000 });
  });

  test.afterEach(async ({ page }) => {
    // Close any open dialog.
    await page.keyboard.press('Escape');
    await page.locator('.cdk-overlay-backdrop').waitFor({ state: 'hidden', timeout: 5000 }).catch(() => {});
    await page.waitForTimeout(1000);

    // Open dialog to clean up.
    await page.locator('#cell0_0').click();
    await page.locator('[data-testid="plannedStartOfShift1"]').waitFor({ state: 'visible', timeout: 15000 }).catch(() => {});
    await page.waitForTimeout(500);

    // Delete planned shift fields if they have values.
    for (const selector of ['plannedStartOfShift1', 'plannedEndOfShift1']) {
      const deleteBtn = page.locator(`[data-testid="${selector}"]`)
        .locator('xpath=ancestor::div[contains(@class,"flex-row")]')
        .locator('button mat-icon')
        .filter({ hasText: 'delete' });
      if (await deleteBtn.count() > 0) {
        await deleteBtn.click({ force: true });
        await page.waitForTimeout(500);
      }
    }

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

import { test, expect, Page } from '@playwright/test';
import { LoginPage } from '../../../Page objects/Login.page';
import { OFFGRID_TIMES_G1M } from '../../../../helpers/one-minute-times';

/**
 * g1m variant of the comment add/modify/remove suite cloned from
 * `g/dashboard-edit-a.spec.ts`. Where b1m/c1m/d1m/e1m sweep different
 * quadrants of the timepicker clock face with ascending 5-shift round-trip
 * data, and f1m exercises the negative-path validators, g1m's contract is
 * the COMMENT field's PUT round-trip across save (add → modify → remove).
 *
 * Why this matters for the flag-on matrix entry:
 *   The legacy `g/` shard fills only shift 1 (`01:00-10:00`) before saving
 *   each comment change. Following the multishift-shape pattern from
 *   b1m/c1m/d1m/e1m, this variant fills ALL FIVE planned shifts ascending
 *   with off-grid times in every `beforeEach`, so the form's
 *   `plannedShiftDurationValidator` and `shiftWiseValidator` re-evaluate
 *   against `minutesGap=1` picker output across all five slots — not just
 *   shift 1. Comment field is then filled and saved exactly as in `g/`.
 *
 * State carries across tests:
 *   • test 1 saves comment `'test comment'`
 *   • test 2 asserts that value is read back, then saves `'test comment updated'`
 *   • test 3 asserts the updated value, clears, saves empty
 *   The `afterEach` clears shifts 1-5 (the variant adds shifts 2-5 vs. the
 *   legacy `g/` afterEach which only cleared shift-1) and saves an empty
 *   form so the next test starts from a clean planning row. Comment value
 *   is preserved across the clear+save (`#CommentOffice` is not touched
 *   in afterEach), so test 2's read-back assertion still sees the value
 *   test 1 wrote.
 *
 * Shift layout used by every `beforeEach` (every value non-multiple-of-5):
 *   Shift 1: 10:02 - 11:14
 *   Shift 2: 11:26 - 13:38
 *   Shift 3: 13:49 - 15:53
 *   Shift 4: 16:04 - 18:16
 *   Shift 5: 18:28 - 19:39
 *
 * Clock-quadrant: late-morning to early-evening (10-19), distinct from
 * b1m (08-16), c1m (08-19), d1m (13-23) and e1m (01-11).
 *
 * Shifts 3-5 are only rendered in the workday-entity dialog when the
 * assigned site has thirdShiftActive/fourthShiftActive/fifthShiftActive
 * flipped on. The post-migration patch only sets `UseOneMinuteIntervals`,
 * so the activation dance lives in `beforeEach` (idempotent: skipped on
 * subsequent tests because the DB state survives).
 */

async function waitForSpinner(page: Page) {
  if (await page.locator('.overlay-spinner').count() > 0) {
    await page.locator('.overlay-spinner').waitFor({ state: 'hidden', timeout: 30000 });
  }
}

/**
 * Position-based clock-face picker. Identical helper to the b1m/c1m/d1m/
 * e1m/f1m specs — works uniformly for h=0 (break and midnight times)
 * unlike rotateZ-selector strategies that fail on the inner-ring `00`
 * position.
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
  await page.locator('.cdk-overlay-backdrop').waitFor({ state: 'hidden', timeout: 10000 }).catch(() => {});
  await page.waitForTimeout(500);
}

async function setShiftPlanned(page: Page, shiftId: 1|2|3|4|5, start: string, end: string) {
  await page.locator(`[data-testid="plannedStartOfShift${shiftId}"]`).click();
  await pickTime(page, start);
  await expect(page.locator(`[data-testid="plannedStartOfShift${shiftId}"]`)).toHaveValue(start);

  await page.locator(`[data-testid="plannedEndOfShift${shiftId}"]`).click();
  await pickTime(page, end);
  await expect(page.locator(`[data-testid="plannedEndOfShift${shiftId}"]`)).toHaveValue(end);
}

const allFiveShifts = [
  { id: 1 as const, start: OFFGRID_TIMES_G1M.shift1Start, end: OFFGRID_TIMES_G1M.shift1End },
  { id: 2 as const, start: OFFGRID_TIMES_G1M.shift2Start, end: OFFGRID_TIMES_G1M.shift2End },
  { id: 3 as const, start: OFFGRID_TIMES_G1M.shift3Start, end: OFFGRID_TIMES_G1M.shift3End },
  { id: 4 as const, start: OFFGRID_TIMES_G1M.shift4Start, end: OFFGRID_TIMES_G1M.shift4End },
  { id: 5 as const, start: OFFGRID_TIMES_G1M.shift5Start, end: OFFGRID_TIMES_G1M.shift5End },
];

test.describe.configure({ mode: 'serial' });

test.describe('Dashboard edit values (g1m, flag-on, comment add/modify/remove with multishift fill)', () => {
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

    // After the site filter selection the dashboard re-renders. Without a
    // settle wait the next `#firstColumn3` click can race with the row
    // re-rendering ("element was detached from the DOM, retrying" → 120s
    // timeout, observed in PR #1552 round 1 g1m run).
    await waitForSpinner(page);
    await page.waitForTimeout(500);

    // Activate shifts 3-5 if not already on (b1m pattern, idempotent variant).
    // The DB state survives across tests in this `describe.serial` block,
    // so the first test does the toggling+saving, and tests 2/3 enter the
    // dialog, see all three checkboxes already checked, and cancel out
    // without hitting the PUT — avoiding a `waitForResponse` hang on a
    // no-op save (the form's saveButton is disabled when nothing changed).
    let needsActivation = false;
    {
      // Re-resolve via a fresh `.first()` snapshot inside the `.click()` call
      // so Playwright's auto-retry on a stale match still finds the post-
      // re-render element rather than the detached pre-render one.
      await page.locator('#firstColumn3').first().scrollIntoViewIfNeeded();
      await page.locator('#firstColumn3').first().click({ timeout: 30000 });
      await expect(page.locator('mat-dialog-container')).toBeVisible({ timeout: 10000 });
      for (const id of ['thirdShiftActive', 'fourthShiftActive', 'fifthShiftActive']) {
        const cb = page.locator(`#${id} input[type="checkbox"]`);
        await cb.waitFor({ state: 'attached', timeout: 10000 });
        if (!(await cb.isChecked())) {
          needsActivation = true;
          await page.locator(`#${id}`).click({ force: true });
          await expect(cb).toBeChecked();
        }
      }
      if (needsActivation) {
        const assignSitePromise = page.waitForResponse(
          r => r.url().includes('/api/time-planning-pn/settings/assigned-site') && r.request().method() === 'PUT');
        await page.locator('#saveButton').click({ force: true });
        await assignSitePromise;
      } else {
        await page.locator('#cancelButton').click({ force: true });
      }
      await waitForSpinner(page);
      await expect(page.locator('mat-dialog-container')).toHaveCount(0, { timeout: 10000 });
    }

    await page.locator('#cell0_0').click();
    await expect(page.locator('#planHours')).toBeVisible();

    // Wipe shift-1 (planned + actual-1 start/stop) the same way the legacy
    // `g/` shard does. The afterEach below clears shifts 1-5 planned, so by
    // the time we re-enter beforeEach the only seed-prefilled fields left
    // are the shift-1 set this loop covers (plus the seed's actual-1 row).
    for (const selector of ['plannedStartOfShift1', 'plannedEndOfShift1', 'start1StartedAt', 'stop1StoppedAt']) {
      const newSelector = `[data-testid="${selector}"]`;
      await page.locator(newSelector)
        .locator('xpath=ancestor::div[contains(@class,"flex-row")]')
        .locator('button mat-icon')
        .filter({ hasText: 'delete' })
        .click({ force: true });
      await page.waitForTimeout(500);
    }

    // Multishift-shape: fill all 5 planned shifts ascending with off-grid
    // values so every `beforeEach` exercises the flag-on `minutesGap=1`
    // picker on every shift slot, not just shift 1.
    for (const s of allFiveShifts) {
      await setShiftPlanned(page, s.id, s.start, s.end);
    }
    await page.waitForTimeout(500);
  });

  test('should add a comment', async ({ page }) => {
    await page.locator('#CommentOffice').clear();
    await page.locator('#CommentOffice').fill('test comment');

    await expect(page.locator('#saveButton')).toBeEnabled({ timeout: 10000 });

    const savePromise = page.waitForResponse(
      r => r.url().includes('/api/time-planning-pn/plannings/') && r.request().method() === 'PUT'
    );
    await page.locator('#saveButton').click();
    await savePromise;
  });

  test('should modify a comment', async ({ page }) => {
    await expect(page.locator('#CommentOffice')).toHaveValue('test comment');
    await page.locator('#CommentOffice').clear();
    await page.locator('#CommentOffice').fill('test comment updated');

    await expect(page.locator('#saveButton')).toBeEnabled({ timeout: 10000 });

    const savePromise = page.waitForResponse(
      r => r.url().includes('/api/time-planning-pn/plannings/') && r.request().method() === 'PUT'
    );
    await page.locator('#saveButton').click();
    await savePromise;
  });

  test('should remove a comment', async ({ page }) => {
    await expect(page.locator('#CommentOffice')).toHaveValue('test comment updated');
    await page.locator('#CommentOffice').clear();

    await expect(page.locator('#saveButton')).toBeEnabled({ timeout: 10000 });

    const savePromise = page.waitForResponse(
      r => r.url().includes('/api/time-planning-pn/plannings/') && r.request().method() === 'PUT'
    );
    await page.locator('#saveButton').click();
    await savePromise;
  });

  test.afterEach(async ({ page }) => {
    await page.locator('#cell0_0').click();

    // Clear shifts 1-5 (mirrors the legacy `g/` afterEach for shift-1, plus
    // the higher shifts this variant fills in `beforeEach`). Each delete-
    // icon click is best-effort — if a previous test already cleared the
    // field (or the form's validity changed mid-test), the icon may not
    // render; continue silently.
    const allShiftSelectors = [
      'plannedStartOfShift1', 'plannedEndOfShift1', 'start1StartedAt', 'stop1StoppedAt',
      'plannedStartOfShift2', 'plannedEndOfShift2',
      'plannedStartOfShift3', 'plannedEndOfShift3',
      'plannedStartOfShift4', 'plannedEndOfShift4',
      'plannedStartOfShift5', 'plannedEndOfShift5',
    ];
    for (const selector of allShiftSelectors) {
      const newSelector = `[data-testid="${selector}"]`;
      const input = page.locator(newSelector);
      if (await input.count() === 0) continue;
      const value = await input.inputValue().catch(() => '');
      if (!value) continue;
      const deleteIcon = page.locator(newSelector)
        .locator('xpath=ancestor::div[contains(@class,"flex-row")]')
        .locator('button mat-icon')
        .filter({ hasText: 'delete' });
      if (await deleteIcon.count() === 0) continue;
      await deleteIcon.first().click({ force: true });
      await page.waitForTimeout(500);
    }

    await expect(page.locator('#saveButton')).toBeEnabled({ timeout: 10000 });

    const savePromise = page.waitForResponse(
      r => r.url().includes('/api/time-planning-pn/plannings/') && r.request().method() === 'PUT'
    );
    await page.locator('#saveButton').click();
    await savePromise;
    await page.waitForTimeout(1000);
  });
});

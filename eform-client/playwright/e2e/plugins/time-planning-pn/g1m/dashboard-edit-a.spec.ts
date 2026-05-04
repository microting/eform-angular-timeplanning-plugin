import { test, expect, Page } from '@playwright/test';
import { LoginPage } from '../../../Page objects/Login.page';
import { OFFGRID_TIMES_G1M } from '../../../../helpers/one-minute-times';

/**
 * g1m variant — comment add round-trip cloned from `g/dashboard-edit-a.spec.ts`.
 *
 * Where b1m/c1m/d1m/e1m sweep different quadrants of the timepicker clock face
 * with ascending 5-shift round-trip data, and f1m exercises the negative-path
 * validators, g1m's contract is the COMMENT field's PUT round-trip across save
 * (add — single test, scope kept tight per FU-C plan).
 *
 * Why this matters for the flag-on matrix entry:
 *   The legacy `g/` shard fills only shift 1 (`01:00-10:00`) before saving
 *   each comment change. Following the multishift-shape pattern from
 *   b1m/c1m/d1m/e1m, this variant fills ALL FIVE planned shifts ascending
 *   with off-grid times BEFORE the comment-save click, so the form's
 *   `plannedShiftDurationValidator` and `shiftWiseValidator` re-evaluate
 *   against `minutesGap=1` picker output across all five slots — not just
 *   shift 1. Comment field is then filled and saved.
 *
 * History note: PR #1552 round 1+2 hit `locator.click: Test timeout 120000ms`
 * on the `#firstColumn3` click in `beforeEach` (the assigned-site config
 * dialog open, used to flip ThirdShiftActive / FourthShiftActive /
 * FifthShiftActive at runtime). PR #1556 (FU-A) moved that activation into
 * `post-migration.sql` for every 1m shard, so this re-add can skip the
 * runtime activation dance entirely — `#cell0_0` opens the workday-entity-
 * dialog directly with shifts 3-5 already rendered. This eliminates the
 * single-source hang the trim commit (5309702d) attributed to the g1m flow.
 *
 * Shift layout (every value non-multiple-of-5):
 *   Shift 1: 10:02 - 11:14
 *   Shift 2: 11:26 - 13:38
 *   Shift 3: 13:49 - 15:53
 *   Shift 4: 16:04 - 18:16
 *   Shift 5: 18:28 - 19:39
 *
 * Clock-quadrant: late-morning to early-evening (10-19), distinct from
 * b1m (08-16), c1m (08-19), d1m (13-23) and e1m (01-11).
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

test.describe('Dashboard edit values (g1m, flag-on, comment add with multishift fill)', () => {
  test('should add a comment', async ({ page }) => {
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

    // After the site filter selection the dashboard re-renders. Settle
    // before the cell click so the locator binds against the post-render
    // tree (avoids 'element detached from DOM' retries observed in
    // PR #1552 round 1).
    await waitForSpinner(page);
    await page.waitForTimeout(500);

    await page.locator('#cell0_0').click();
    await expect(page.locator('#planHours')).toBeVisible();

    // Wipe shift-1 (planned + actual-1 start/stop) the same way the legacy
    // `g/` shard does — the seed prefilled shift-1 only, so we only need
    // to clear that row before refilling with off-grid values across all
    // five shifts.
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
    // values so the flag-on `minutesGap=1` picker is exercised on every
    // shift slot. Shifts 3-5 are rendered because FU-A's post-migration
    // patch flips ThirdShiftActive / FourthShiftActive / FifthShiftActive
    // on every active assigned site — no runtime activation dance needed.
    for (const s of allFiveShifts) {
      await setShiftPlanned(page, s.id, s.start, s.end);
    }
    await page.waitForTimeout(500);

    // Now fill the COMMENT field — the contract under test.
    await page.locator('#CommentOffice').clear();
    await page.locator('#CommentOffice').fill('test comment');

    // Form must be valid (all 5 shifts filled, comment populated) before
    // the save button enables. Wait explicitly so we don't race the
    // button-state updater.
    await expect(page.locator('#saveButton')).toBeEnabled({ timeout: 10000 });

    const savePromise = page.waitForResponse(
      r => r.url().includes('/api/time-planning-pn/plannings/') && r.request().method() === 'PUT'
    );
    await page.locator('#saveButton').click();
    await savePromise;
  });
});

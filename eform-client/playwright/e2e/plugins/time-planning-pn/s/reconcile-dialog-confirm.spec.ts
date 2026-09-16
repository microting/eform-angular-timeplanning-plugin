import { test, expect } from '@playwright/test';
import { LoginPage } from '../../../Page objects/Login.page';
import {
  closeDayAfterLockChange, closeDayWithoutChange, countPuts, openDashboardLastWeek, openDay,
  PLANNING_PUT_PATH, PROVENANCE, RECONCILE_PATH, reconcileOpenDay, tdOf, unlockAll, unlockDay,
  workerAtRow,
} from './reconcile-helpers';

/**
 * Spec §8.2: reconcile takes two clicks in the dialog's own footer, and the dialog
 * that was open stays open, read-only. The worker is grid row 6 at the start and is
 * found by name after that.
 */
test.describe('Reconciled day lock: dialog confirm', () => {
  /**
   * The worker whose row a test locked. afterEach unlocks it even when an assertion
   * failed halfway through: the shard shares one database and runs its files in
   * order, so a row left locked would break every spec after this one.
   */
  let worker = '';

  test.beforeEach(async ({ page }) => {
    worker = '';
    await page.goto('http://localhost:4200');
    await new LoginPage(page).login();
    await openDashboardLastWeek(page);
  });

  test.afterEach(async ({ page }) => {
    await unlockAll(page, worker);
  });

  test('reconcile takes a second click in the same footer, then the dialog stays open read-only', async ({ page }) => {
    worker = await workerAtRow(page, 6);
    const reconciles = countPuts(page, RECONCILE_PATH);

    const date = await openDay(page, worker, 3);
    await expect(page.locator('#saveButton')).toBeVisible();
    await expect(page.locator('#CommentOffice')).toBeEnabled();

    // The first click morphs the footer. It commits nothing and opens no second modal.
    await page.locator('#reconcileButton').click();
    await expect(page.locator('#reconcileConfirmText')).toContainText(worker);
    await expect(page.locator('#reconcileConfirmText')).toContainText(date);
    await expect(page.locator('#saveButton')).toHaveCount(0);
    await expect(page.locator('mat-dialog-container')).toHaveCount(1);
    // Polled, not read once: a PUT already on the wire would slip through a
    // synchronous read taken the instant after the click.
    await expect.poll(() => reconciles.count).toBe(0);

    // Backing out restores the normal footer, and still nothing is sent.
    await page.locator('#reconcileCancelButton').click();
    await expect(page.locator('#saveButton')).toBeVisible();
    await expect.poll(() => reconciles.count).toBe(0);

    // The second click commits. The SAME dialog turns read-only, with the provenance
    // line where the actions were.
    await reconcileOpenDay(page);
    await expect.poll(() => reconciles.count).toBe(1);
    await expect(page.locator('mat-dialog-container')).toHaveCount(1);
    await expect(page.locator('#saveButton')).toHaveCount(0);
    await expect(page.locator('#reconcileButton')).toHaveCount(0);
    await expect(page.locator('#reconciledProvenanceText')).toHaveText(PROVENANCE);
    await expect(page.locator('#CommentOffice')).toBeDisabled();

    // Closing reloads the grid and does not save.
    await closeDayAfterLockChange(page);
    await expect(tdOf(page, worker, 3)).toHaveClass(/reconciled-background/);
    await expect(tdOf(page, worker, 2)).toHaveClass(/locked-background/);
    await expect(tdOf(page, worker, 4)).not.toHaveClass(/locked-background|reconciled-background/);

    // A later open shows the server's stored timestamp.
    await openDay(page, worker, 3);
    await expect(page.locator('#reconciledProvenanceText')).toHaveText(PROVENANCE);
    await closeDayWithoutChange(page);

    // Cleanup.
    await unlockDay(page, worker, 3);
    await expect(tdOf(page, worker, 3)).not.toHaveClass(/reconciled-background/);
  });

  test('a day with unsaved edits offers no reconcile, and Cancel writes nothing', async ({ page }) => {
    worker = await workerAtRow(page, 6);
    await openDay(page, worker, 1);
    await expect(page.locator('#reconcileButton')).toBeVisible();

    await page.locator('#CommentOffice').fill('reconcile-guard');
    await expect(page.locator('#reconcileButton')).toHaveCount(0);
    await expect(page.locator('#reconcileNeedsSave')).toBeVisible();

    // Cancel closes with '': no save, no reconcile, no write of any kind.
    const writes = countPuts(page, PLANNING_PUT_PATH);
    await closeDayWithoutChange(page);
    await page.waitForTimeout(1000);
    await expect
      .poll(() => writes.count, { message: 'Cancel must not send a save or a reconcile' })
      .toBe(0);
  });
});

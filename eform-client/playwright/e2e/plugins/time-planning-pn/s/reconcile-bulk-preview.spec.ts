import { test, expect } from '@playwright/test';
import {
  cellOf, closeDayWithoutChange, countPuts, expectSuccess, lastWeekMonday,
  openDay, RECONCILE_THROUGH_PATH, reconcileDay, rowCheckbox, rowOf, tdOf, unlockDay,
  useLastWeekDashboard, waitForIndex, waitForPut, waitForSpinner,
} from './reconcile-helpers';

/**
 * Spec §8.3. The workers are picked from grid rows 7 (A), 8 (B) and 10 (outsider) at the
 * start and are found by name after that. B is reconciled further forward (day 5) than
 * the bulk target (day 3), so it must be shown as skipped and left where it is.
 *
 * The outsider is picked through the session as well, although the test's point is that
 * it is never touched: if the bulk reconcile reaches outside the selection anyway, the
 * session cleanup frees that row instead of leaving it to break the next spec.
 */
test.describe('Reconciled day lock: bulk reconcile', () => {
  const session = useLastWeekDashboard();

  test('a header click previews per worker, skips rows already further, and toasts counts', async ({ page }) => {
    // Three workers, a preview, a commit and three rows to clean up afterwards, and the
    // frame's login, navigation and cleanup count against the same per-test budget.
    test.slow();
    const a = await session.pickWorker(page, 7);
    const b = await session.pickWorker(page, 8);
    const outsider = await session.pickWorker(page, 10);
    const throughs = countPuts(page, RECONCILE_THROUGH_PATH);

    await reconcileDay(page, b, 5);

    await rowCheckbox(page, a).check();
    await rowCheckbox(page, b).check();

    // Opening a day must not clear the batch selection: mtx-grid selects the row on any
    // click inside it unless disableRowClickSelection is on.
    await openDay(page, a, 0);
    await closeDayWithoutChange(page);
    await expect(rowCheckbox(page, a)).toBeChecked();
    await expect(rowCheckbox(page, b)).toBeChecked();

    // Preview: the region is drawn in place, and nothing is written.
    await page.locator('#dayHeader3').click();
    for (const day of [0, 1, 2, 3]) {
      await expect(cellOf(page, a, day)).toHaveClass(/tp-preview-lock/);
    }
    await expect(cellOf(page, a, 3)).toHaveClass(/tp-preview-boundary/);
    await expect(cellOf(page, a, 4)).not.toHaveClass(/tp-preview-lock/);
    await expect(rowOf(page, b).locator('.tp-preview-skip-label')).toBeVisible();
    await expect(cellOf(page, b, 3)).toHaveClass(/tp-preview-skip/);
    await expect(cellOf(page, b, 3)).not.toHaveClass(/tp-preview-lock/);
    // The selection is the scope: an unticked worker is not previewed.
    await expect(cellOf(page, outsider, 0)).not.toHaveClass(/tp-preview-lock/);
    const summary = page.locator('#reconcileScopeSummary');
    await expect(summary).toHaveText(/^Afstem til og med \d{2}\.\d{2}\.\d{4} · Medarbejdere: 1$/);
    await expect(page.locator('#reconcileScopeSkipped')).toContainText('Springes over: 1');
    await expect.poll(() => throughs.count, { message: 'the preview must write nothing' }).toBe(0);

    // Cancel drops the preview and still writes nothing.
    await page.locator('#reconcileScopeCancel').click();
    await expect(page.locator('#reconcileScopeBar')).toHaveCount(0);
    await expect(page.locator('.tp-preview-lock')).toHaveCount(0);
    await expect.poll(() => throughs.count, { message: 'cancelling must write nothing' }).toBe(0);

    // Commit.
    await page.locator('#dayHeader3').click();
    const [, dd, mm, yyyy] = /(\d{2})\.(\d{2})\.(\d{4})/.exec(await summary.innerText())!;
    const put = waitForPut(page, RECONCILE_THROUGH_PATH);
    const reload = waitForIndex(page, lastWeekMonday());
    await page.locator('#reconcileScopeConfirm').click();
    const response = await put;
    const body = await expectSuccess(response);
    const sent = response.request().postDataJSON();
    expect(sent.date, 'the date sent is the date the scope bar named').toBe(`${yyyy}-${mm}-${dd}`);
    expect(sent.siteIds, 'the skipped row is sent too, so the server reports it').toHaveLength(2);
    expect(body.model.applied).toBe(1);
    expect(body.model.skippedAlreadyFurtherForward).toHaveLength(1);
    await expect(page.locator('#toast-container')).toContainText('Afstemt: 1 · Sprunget over: 1');
    await reload;
    await waitForSpinner(page);

    // The staircase: A at day 3, B untouched at day 5; the selection and preview are gone.
    await expect(tdOf(page, a, 3)).toHaveClass(/reconciled-background/);
    await expect(tdOf(page, a, 2)).toHaveClass(/locked-background/);
    await expect(tdOf(page, a, 4)).not.toHaveClass(/locked-background|reconciled-background/);
    await expect(tdOf(page, b, 5)).toHaveClass(/reconciled-background/);
    await expect(page.locator('#reconcileScopeBar')).toHaveCount(0);
    await expect(rowCheckbox(page, a)).not.toBeChecked();

    // Cleanup. The session cleanup is only the safety net for a failure above.
    await unlockDay(page, a, 3);
    await unlockDay(page, b, 5);
  });

  test('a reload while ticked rows are previewed cancels the preview instead of widening it', async ({ page }) => {
    const a = await session.pickWorker(page, 7);
    const outsider = await session.pickWorker(page, 10);

    await rowCheckbox(page, a).check();
    await page.locator('#dayHeader3').click();
    await expect(page.locator('#reconcileScopeSummary')).toContainText('Medarbejdere: 1');

    const reload = waitForIndex(page, lastWeekMonday());
    await page.locator('#workingHoursReload').click();
    await reload;
    await waitForSpinner(page);

    // Silently becoming "every visible worker" would put the outsider in scope. Instead
    // the preview is gone.
    await expect(page.locator('#reconcileScopeBar')).toHaveCount(0);
    await expect(cellOf(page, outsider, 0)).not.toHaveClass(/tp-preview-lock/);
    await expect(page.locator('.tp-preview-lock')).toHaveCount(0);
  });
});

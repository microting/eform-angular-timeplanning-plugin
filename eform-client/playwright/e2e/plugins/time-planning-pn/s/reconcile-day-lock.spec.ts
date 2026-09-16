import { test, expect } from '@playwright/test';
import {
  assertReadOnlyDialog, closeDayWithoutChange, openDay, reconcileDay, tdOf, unlockDay,
  useLastWeekDashboard,
} from './reconcile-helpers';

/**
 * The whole lock in one pass: reconcile a day, watch the cascade reach every earlier
 * day, open one of them read-only, be refused the unlock below the boundary, and
 * unlock at the boundary. The worker is grid row 3 at the start and is found by name
 * after that; the grid is on last week, so every day in view is in the past (I2)
 * whatever weekday CI runs on.
 */
test.describe('Reconciled day lock: end to end', () => {
  const session = useLastWeekDashboard();

  // Day 2 (last week's Wednesday) becomes the boundary: days 0 and 1 cascade shut
  // behind it, and day 3 stays open.
  test('reconciling a day locks it and every earlier day for that worker', async ({ page }) => {
    // The whole flow in one test, and the frame's login, navigation and cleanup count
    // against the same per-test budget.
    test.slow();
    const worker = await session.pickWorker(page, 3);

    const boundaryDate = await reconcileDay(page, worker, 2);

    // The boundary treatment sits on the reconciled day, and on it alone.
    await expect(tdOf(page, worker, 2)).toHaveClass(/reconciled-background/);

    // The cascade: earlier days are locked, but they were not themselves reconciled.
    for (const day of [0, 1]) {
      await expect(tdOf(page, worker, day)).toHaveClass(/locked-background/);
      await expect(tdOf(page, worker, day)).not.toHaveClass(/reconciled-background/);
    }

    // Nothing above the boundary moved.
    await expect(tdOf(page, worker, 3))
      .not.toHaveClass(/locked-background|reconciled-background/);

    // A cascade-locked day still opens — people read closed days constantly — but it
    // opens read-only, for the same worker. The fields are closed too, not just the
    // footer: this is the cascade branch, where nothing was reconciled and the day is
    // shut only by the boundary above it. The unlock is not offered here; the day that
    // has to be freed first is named instead.
    await openDay(page, worker, 0);
    await assertReadOnlyDialog(page);
    await expect(page.locator('#CommentOffice')).toBeDisabled();
    await expect(page.locator('#lockedFreeFirstText')).toContainText(boundaryDate);
    await closeDayWithoutChange(page);

    // The boundary is where the unlock lives, and using it frees the whole cascade —
    // the boundary day included, which must not merely lose its seal and stay shut.
    await unlockDay(page, worker, 2);
    await expect(tdOf(page, worker, 2)).not.toHaveClass(/reconciled-background/);
    for (const day of [0, 1, 2]) {
      await expect(tdOf(page, worker, day)).not.toHaveClass(/locked-background/);
    }
  });
});

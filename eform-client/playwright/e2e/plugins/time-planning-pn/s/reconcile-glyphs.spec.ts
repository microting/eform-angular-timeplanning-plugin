import { test, expect } from '@playwright/test';
import {
  cellOf, expectTooltipOnHover, LOCKED_TOOLTIP, PROVENANCE, reconcileDay,
  rowOf, tdOf, unlockDay, useLastWeekDashboard,
} from './reconcile-helpers';

/**
 * Spec §8.1: the lock and seal glyphs, their tooltips, and the legend under the
 * grid. The worker is grid row 5 at the start and is found by name after that.
 */
test.describe('Reconciled day lock: glyphs and legend', () => {
  const session = useLastWeekDashboard();

  // Day 4 (last week's Friday) becomes the boundary, so day 2 is cascade-locked and
  // day 5 stays open.
  test('the boundary shows the seal, earlier days the lock, and the legend explains both', async ({ page }) => {
    // Precondition, and a leak detector for the specs before this one: nothing
    // locked is in view, so there is no legend.
    await expect(page.locator('#lockLegend')).toHaveCount(0);

    const worker = await session.pickWorker(page, 5);
    await reconcileDay(page, worker, 4);

    // Boundary: the seal and no lock. Its tooltip is the provenance line.
    const seal = cellOf(page, worker, 4).locator('.tp-day-glyph--seal');
    await expect(seal).toBeVisible();
    await expect(seal.locator('mat-icon')).toHaveClass(/tp-seal/);
    await expect(cellOf(page, worker, 4).locator('.tp-day-glyph--lock')).toHaveCount(0);
    await expectTooltipOnHover(page, seal, PROVENANCE);

    // Cascade: the lock and no seal. Its tooltip says what the day is.
    const lock = cellOf(page, worker, 2).locator('.tp-day-glyph--lock');
    await expect(lock).toBeVisible();
    await expect(cellOf(page, worker, 2).locator('.tp-day-glyph--seal')).toHaveCount(0);
    await expectTooltipOnHover(page, lock, LOCKED_TOOLTIP);

    // Above the boundary: no glyph at all.
    await expect(cellOf(page, worker, 5).locator('.tp-day-glyph')).toHaveCount(0);

    // The legend appears with the first locked day and names both states.
    await expect(page.locator('#lockLegend')).toBeVisible();
    await expect(page.locator('#lockLegendLocked')).toContainText(LOCKED_TOOLTIP);
    await expect(page.locator('#lockLegendReconciled')).toContainText('Afstemt');

    // Unlocking is part of the behaviour under test: with nothing locked the glyphs
    // and the legend go away again. The session cleanup only catches what a failure
    // leaves behind.
    await unlockDay(page, worker, 4);
    await expect(cellOf(page, worker, 2).locator('.tp-day-glyph')).toHaveCount(0);
    await expect(page.locator('#lockLegend')).toHaveCount(0);
  });

  // Ruling F20: every reconcile leaves its own mark, so a row can carry several
  // sealed days. Only the day at lockedThrough is the boundary.
  test('an older reconciled day stays sealed under a later boundary, without the boundary border', async ({ page }) => {
    await expect(page.locator('#lockLegend')).toHaveCount(0);

    const worker = await session.pickWorker(page, 5);
    // Day 1 first, then day 3, which becomes the boundary.
    await reconcileDay(page, worker, 1);
    await reconcileDay(page, worker, 3);

    // Day 1 keeps its mark: the locked texture and the seal, with no boundary
    // border and no lock glyph. Its tooltip still says when it was reconciled.
    const olderSeal = cellOf(page, worker, 1).locator('.tp-day-glyph--seal');
    await expect(tdOf(page, worker, 1)).toHaveClass(/locked-background/);
    await expect(tdOf(page, worker, 1)).not.toHaveClass(/reconciled-background/);
    await expect(olderSeal).toBeVisible();
    await expect(cellOf(page, worker, 1).locator('.tp-day-glyph--lock')).toHaveCount(0);
    await expectTooltipOnHover(page, olderSeal, PROVENANCE);

    // Day 2, between the two marks, is plainly locked.
    await expect(tdOf(page, worker, 2)).toHaveClass(/locked-background/);
    await expect(cellOf(page, worker, 2).locator('.tp-day-glyph--lock')).toBeVisible();

    // Day 3 is the one boundary on the row.
    await expect(tdOf(page, worker, 3)).toHaveClass(/reconciled-background/);
    await expect(rowOf(page, worker).locator('td.reconciled-background')).toHaveCount(1);

    // Unlocking the boundary moves the line back to the older mark, which becomes
    // the boundary in turn.
    await unlockDay(page, worker, 3);
    await expect(tdOf(page, worker, 1)).toHaveClass(/reconciled-background/);
    await expect(cellOf(page, worker, 2).locator('.tp-day-glyph')).toHaveCount(0);
    await expect(tdOf(page, worker, 3)).not.toHaveClass(/locked-background|reconciled-background/);

    await unlockDay(page, worker, 1);
    await expect(page.locator('#lockLegend')).toHaveCount(0);
  });
});

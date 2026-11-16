import { expect, test } from '@playwright/test';
import {
  addDaysKey, Api, cellOf, clickCell, danishHours, expectSuccess, manualDotOf, mondayKey, openPlannerAsAdmin,
  shiftClickCell, waitForGrid, waitForPlanner, weekSumOf,
} from './planner-helpers';

/**
 * Screen 2: select several cells across two workers (click and shift-click) and
 * fill them with one short form. Next week, so no day is locked or in the past.
 */
test.describe('Planner - grid edit across several cells', () => {
  const nextMonday = mondayKey(1);
  const days = [0, 1, 2].map(d => addDaysKey(nextMonday, d));
  let first: number;
  let second: number;

  test.beforeAll(async () => {
    const api = await Api.signIn();
    [first, second] = await api.assignedSiteIds('c d', 'e f');
    await api.dispose();
  });

  test('fills Monday to Wednesday for two workers with one short form', async ({ page }) => {
    await openPlannerAsAdmin(page);

    // c d: Monday, shift-click Wednesday. e f: Monday (a new anchor), shift-click Wednesday.
    await clickCell(page, first, days[0]);
    await shiftClickCell(page, first, days[2]);
    await clickCell(page, second, days[0]);
    await shiftClickCell(page, second, days[2]);

    await expect(page.locator('#planner-editor')).toBeVisible();
    await expect(page.locator('[data-testid="planner-editor-count"]')).toHaveText('6 dage valgt (2 medarbejdere)');
    for (const id of [first, second]) {
      for (const key of days) {
        await expect(cellOf(page, id, key)).toHaveAttribute('aria-pressed', 'true');
      }
    }

    await page.locator('#planner-editor-shortform').fill('7-15/0,5');
    await expect(page.locator('[data-testid="planner-editor-invalid"]')).toHaveCount(0);
    const save = waitForPlanner(page, 'POST', /\/planner\/cells$/);
    const reload = waitForGrid(page);
    await page.locator('#planner-editor-fill').click();
    const body = await expectSuccess(await save);
    expect(body.model.updatedCount).toBe(6);
    expect(body.model.errors).toEqual([]);
    await expectSuccess(await reload);

    // The selection is gone, every day shows the hours and the manual dot.
    await expect(page.locator('#planner-editor')).toHaveCount(0);
    for (const id of [first, second]) {
      for (const key of days) {
        await expect(cellOf(page, id, key).locator('strong')).toHaveText(danishHours(7.5));
        await expect(manualDotOf(page, id, key)).toBeVisible();
        await expect(cellOf(page, id, key)).toHaveAttribute('aria-pressed', 'false');
      }
      // Thursday was not selected and is left alone.
      await expect(manualDotOf(page, id, addDaysKey(nextMonday, 3))).toHaveCount(0);
      await expect(weekSumOf(page, id, nextMonday)).toHaveText(danishHours(22.5));
    }
  });
});

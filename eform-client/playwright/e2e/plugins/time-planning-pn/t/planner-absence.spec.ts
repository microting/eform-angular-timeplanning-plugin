import { expect, test } from '@playwright/test';
import {
  absenceOf, addDaysKey, Api, cellOf, clickCell, expectSuccess, mondayKey, openPlannerAsAdmin, selectOption, shiftClickCell,
  waitForGrid, waitForPlanner,
} from './planner-helpers';

/**
 * Screen 4: absence over a period. The worker works Monday to Friday (a pattern
 * saved through the API); the whole of next week, Monday to Sunday, is
 * registered as vacation. The five working days become vacation, the weekend
 * has nothing planned and is skipped. Registering absence is a manager's
 * decision; t/post-migration.sql makes the admin this worker's manager.
 */
const WORK_WEEK = [['7-15/0,5', '7-15/0,5', '7-15/0,5', '7-15/0,5', '7-15/0,5', '', '']];

test.describe('Planner - absence over a period', () => {
  const nextMonday = mondayKey(1);
  const week = [0, 1, 2, 3, 4, 5, 6].map(d => addDaysKey(nextMonday, d));
  let worker: number;

  test.beforeAll(async () => {
    const api = await Api.signIn();
    [worker] = await api.assignedSiteIds('k l');
    await api.savePattern(worker, mondayKey(), mondayKey(), WORK_WEEK);
    await api.dispose();
  });

  test('registers vacation for next week and skips the weekend', async ({ page }) => {
    await openPlannerAsAdmin(page);

    // Select the week in the grid; the dialog takes the worker and the period from it.
    await clickCell(page, worker, week[0]);
    await shiftClickCell(page, worker, week[6]);
    await expect(page.locator('[data-testid="planner-editor-count"]')).toHaveText('7 dage valgt (1 medarbejdere)');

    await page.locator('#planner-absence-open').click();
    const dialog = page.locator('#planner-absence-dialog');
    await expect(dialog).toBeVisible();
    await expect(page.locator('#planner-absence-worker')).toContainText('k l');
    await expect(page.locator('#planner-absence-save')).toBeDisabled();

    await selectOption(page, page.locator('#planner-absence-type'), /^\s*Ferie\s*$/);
    await page.locator('#planner-absence-comment').fill('Efterårsferie');
    await expect(page.locator('#planner-absence-save')).toBeEnabled();

    const save = waitForPlanner(page, 'POST', /\/planner\/absence$/);
    const reload = waitForGrid(page);
    await page.locator('#planner-absence-save').click();
    const result = (await expectSuccess(await save)).model;
    expect(result.appliedDates.map((d: string) => d.substring(0, 10))).toEqual(week.slice(0, 5));
    expect(result.skippedDates.map((d: string) => d.substring(0, 10))).toEqual(week.slice(5));
    await expectSuccess(await reload);
    await expect(dialog).toHaveCount(0);

    for (const key of week.slice(0, 5)) {
      await expect(absenceOf(page, worker, key)).toHaveText('Ferie');
    }
    for (const key of week.slice(5)) {
      await expect(absenceOf(page, worker, key)).toHaveCount(0);
      await expect(cellOf(page, worker, key).locator('strong')).toHaveCount(0);
    }
    // The week before is untouched.
    await expect(absenceOf(page, worker, mondayKey())).toHaveCount(0);
  });
});

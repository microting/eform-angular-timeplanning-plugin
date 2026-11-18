import { expect, test } from '@playwright/test';
import {
  addDaysKey, Api, cellOf, danishHours, expectSuccess, mondayKey, openPlannerAsAdmin, rowOf, selectOption,
  waitForGrid, waitForPlanner, weekSumOf,
} from './planner-helpers';

/**
 * Screen 3, "Kopiér fra en kollega": a worker takes over a colleague's rotation.
 * The colleague's 2-week rotation is saved through the API first; the copy is
 * made in the pattern editor and saved, and the grid must then show the same
 * plan for both.
 */
const SOURCE_WEEKS = [
  ['6-14/0,5', '6-14/0,5', '6-14/0,5', '6-14/0,5', '6-14/0,5', '', ''],
  ['14-22/0,5', '14-22/0,5', '14-22/0,5', '14-22/0,5', '', '', ''],
];
const SECOND_WEEK_HOURS = 30;

test.describe('Planner - copy a pattern from a colleague', () => {
  let source: number;
  let target: number;

  test.beforeAll(async () => {
    const api = await Api.signIn();
    [source, target] = await api.assignedSiteIds('i j', 'm n');
    await api.savePattern(source, mondayKey(), mondayKey(), SOURCE_WEEKS);
    await api.dispose();
  });

  test('copies the colleague\'s rotation and the grid shows the same plan', async ({ page }) => {
    await openPlannerAsAdmin(page);
    await expect(rowOf(page, source)).toContainText('Rotation 2 uger');
    await expect(rowOf(page, target)).toContainText('Ingen fast plan');

    await page.locator(`#planner-pattern-open-${target}`).click();
    await expect(page.locator('#planner-pattern-dialog')).toBeVisible();
    await expect(page.locator('#planner-pattern-save')).toBeEnabled();

    await selectOption(page, page.locator('#planner-pattern-copy-source'), /^\s*i j\b/);
    const copy = waitForPlanner(page, 'POST', new RegExp(`/planner/patterns/${target}/copy-from/${source}$`));
    await page.locator('#planner-pattern-copy-btn').click();
    await expectSuccess(await copy);

    // The editor now holds the colleague's rotation.
    await expect(page.locator('#planner-pattern-rotation-2')).toHaveAttribute('aria-checked', 'true');
    await expect(page.locator('#planner-pattern-day-0-0')).toHaveValue(/^6(:00)?-14(:00)?/);
    await expect(page.locator('#planner-pattern-day-1-0')).toHaveValue(/^14(:00)?-22(:00)?/);

    const save = waitForPlanner(page, 'PUT', new RegExp(`/planner/patterns/${target}$`));
    const reload = waitForGrid(page);
    await page.locator('#planner-pattern-save').click();
    await expectSuccess(await save);
    await expectSuccess(await reload);
    await expect(page.locator('#planner-pattern-dialog')).toHaveCount(0);
    await expect(rowOf(page, target)).toContainText('Rotation 2 uger');

    // Next week is rotation week 2 for both (same anchor), day by day.
    const nextMonday = mondayKey(1);
    for (let d = 0; d < 7; d++) {
      const key = addDaysKey(nextMonday, d);
      const sourceText = await cellOf(page, source, key).innerText();
      await expect.poll(() => cellOf(page, target, key).innerText(), `${key} as the colleague's`).toBe(sourceText);
    }
    await expect(weekSumOf(page, target, nextMonday)).toHaveText(danishHours(SECOND_WEEK_HOURS));
    await expect(weekSumOf(page, source, nextMonday)).toHaveText(danishHours(SECOND_WEEK_HOURS));
  });
});

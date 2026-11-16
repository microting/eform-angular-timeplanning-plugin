import { expect, test } from '@playwright/test';
import {
  addDaysKey, Api, cellOf, danishHours, expectSuccess, mondayKey, nextPeriod, openPlannerAsAdmin, rowOf, waitForGrid,
  waitForPlanner, weekSumOf,
} from './planner-helpers';

/**
 * Screen 3: a 6-week rotation typed into the pattern editor, saved, and then
 * read back from the grid. Each rotation week has its own shift length, so the
 * week sums tell the rotation weeks apart: Monday to Friday of week w is
 * SHIFTS[w], the weekend is off.
 */
const SHIFTS = ['7-15/0,5', '7-14/0,5', '7-13/0,5', '7-12/0,5', '7-11', '7-10'];
const WEEK_HOURS = [37.5, 32.5, 27.5, 22.5, 20, 15];

test.describe('Planner - 6-week rotation pattern', () => {
  let worker: number;

  test.beforeAll(async () => {
    const api = await Api.signIn();
    [worker] = await api.assignedSiteIds('g h');
    await api.dispose();
  });

  test('saves a 6-week rotation and the grid follows it', async ({ page }) => {
    await openPlannerAsAdmin(page);
    await expect(rowOf(page, worker)).toContainText('Ingen fast plan');

    const load = waitForPlanner(page, 'GET', new RegExp(`/planner/patterns/${worker}$`));
    await page.locator(`#planner-pattern-open-${worker}`).click();
    const prefill = (await expectSuccess(await load)).model;
    const dialog = page.locator('#planner-pattern-dialog');
    await expect(dialog).toBeVisible();

    await page.locator('#planner-pattern-form-rotation').click();
    await page.locator('#planner-pattern-rotation-6').click();
    // Typing starts at once, on purpose: text typed while the grid re-renders for
    // the new length must be saved too (the saved days are checked below).
    for (let w = 0; w < 6; w++) {
      for (let d = 0; d < 5; d++) {
        await page.locator(`#planner-pattern-day-${w}-${d}`).fill(SHIFTS[w]);
      }
    }
    await expect(page.locator('[data-testid="planner-pattern-invalid"]')).toHaveCount(0);

    const save = waitForPlanner(page, 'PUT', new RegExp(`/planner/patterns/${worker}$`));
    const reload = waitForGrid(page);
    await page.locator('#planner-pattern-save').click();
    const response = await save;
    const saved = await expectSuccess(response);
    expect(saved.model.rowsWritten).toBeGreaterThan(0);
    // What is saved is what was typed: every rotation week, Monday to Friday, weekend off.
    const sent = response.request().postDataJSON();
    expect(sent.rotationWeeks).toBe(6);
    for (let w = 0; w < 6; w++) {
      for (let d = 0; d < 7; d++) {
        const day = sent.days.find((x: any) => x.weekIndex === w && x.dayOfWeek === d);
        expect(day?.planText ?? '', `rotation week ${w + 1}, weekday ${d}`).toBe(d < 5 ? SHIFTS[w] : '');
      }
    }
    await expectSuccess(await reload);
    await expect(dialog).toHaveCount(0);
    await expect(rowOf(page, worker)).toContainText('Rotation 6 uger');

    // The anchor (rotation week 1) is the prefilled one, the current Monday; the
    // pattern is valid from today, so next week on is wholly the pattern's.
    const anchor = prefill.anchorDate.substring(0, 10);
    expect(anchor).toBe(mondayKey());
    const expectedWeek = (weeksFromAnchor: number) => WEEK_HOURS[weeksFromAnchor % 6];

    await expect(weekSumOf(page, worker, mondayKey(1))).toHaveText(danishHours(expectedWeek(1)));
    await expect(cellOf(page, worker, mondayKey(1))).toContainText('7:00-14:00');
    await expect(cellOf(page, worker, addDaysKey(mondayKey(1), 5)).locator('strong')).toHaveCount(0);

    // Weeks 3 and 4 of the rotation, two periods on.
    await nextPeriod(page, mondayKey(2));
    await expect(weekSumOf(page, worker, mondayKey(2))).toHaveText(danishHours(expectedWeek(2)));
    await expect(weekSumOf(page, worker, mondayKey(3))).toHaveText(danishHours(expectedWeek(3)));
  });
});

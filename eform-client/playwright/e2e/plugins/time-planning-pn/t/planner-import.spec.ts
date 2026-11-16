import { expect, test } from '@playwright/test';
import {
  addDaysKey, Api, expectSuccess, mondayKey, openPlannerAsAdmin, rowOf, waitForGrid, waitForPlanner,
} from './planner-helpers';

/**
 * Screen 6: the one-off import that replaces the Google Sheet. It reads the plan
 * rows the sheet has written and proposes a rotation per worker. The rows are
 * seeded through the planner API:
 *
 *  - "o p" alternates two weeks, A and B, from two weeks back to three weeks on
 *    (every week the other specs show is covered, so a row the grid creates on
 *    its own cannot break the alternation): a 2-week rotation, recognised.
 *  - "q r" has a Monday 7-15 two weeks back and a Monday 8-16 last week and
 *    nothing to repeat: not recognised.
 *
 * Only "o p" is imported. Importing retires the sheet; the spec then takes the
 * sheet back ("Brug Google Sheet igen") so the shard ends as it began.
 */
const WEEK_A = ['7-15/0,5', '7-15/0,5', '7-15/0,5', '7-15/0,5', '7-15/0,5', 'Fri', 'Fri'];
const WEEK_B = ['9-17/0,5', '9-17/0,5', '9-17/0,5', '9-17/0,5', 'Fri', 'Fri', 'Fri'];

test.describe('Planner - import from the Google Sheet', () => {
  let recognised: number;
  let unrecognised: number;

  test.beforeAll(async () => {
    const api = await Api.signIn();
    [recognised, unrecognised] = await api.assignedSiteIds('o p', 'q r');

    // Days grouped by short form, one write per short form.
    const byText = new Map<string, string[]>();
    for (let week = -2; week <= 3; week++) {
      const days = (week % 2 === 0) ? WEEK_A : WEEK_B;
      days.forEach((text, d) => {
        byText.set(text, [...(byText.get(text) ?? []), addDaysKey(mondayKey(week), d)]);
      });
    }
    for (const [text, keys] of byText) {
      await api.writeDays(recognised, keys, text);
    }
    await api.writeDays(unrecognised, [mondayKey(-2)], '7-15/0,5');
    await api.writeDays(unrecognised, [mondayKey(-1)], '8-16/0,5');
    await api.dispose();
  });

  test('previews, reviews and applies the proposed rotation, then takes the sheet back', async ({ page }) => {
    await openPlannerAsAdmin(page);
    await expect(page.locator('#planner-use-sheet-again')).toHaveCount(0);

    // Step 1: preview.
    const preview = waitForPlanner(page, 'GET', /\/planner\/import\/preview$/);
    await page.locator('#planner-import-open').click();
    const model = (await expectSuccess(await preview)).model;
    const dialog = page.locator('#planner-import-dialog');
    await expect(dialog).toBeVisible();
    await expect(page.locator('[data-testid="planner-import-step"]')).toHaveText('Trin 1 af 3');
    await expect(page.locator('[data-testid="planner-import-preview"]')).toBeVisible();

    const proposed = model.workers.find((w: any) => w.assignedSiteId === recognised);
    expect(proposed.recognised, `o p must be recognised (${proposed.reason})`).toBe(true);
    expect(proposed.rotationWeeks).toBe(2);
    const refused = model.workers.find((w: any) => w.assignedSiteId === unrecognised);
    expect(refused.recognised).toBe(false);
    const recognisedCount = model.workers.filter((w: any) => w.recognised).length;
    await expect(page.locator('[data-testid="planner-import-recognised-count"]'))
      .toHaveText(`${recognisedCount} medarbejdere med et genkendt mønster`);

    // Step 2: review. Keep only "o p".
    await page.locator('#planner-import-next').click();
    await expect(page.locator('[data-testid="planner-import-step"]')).toHaveText('Trin 2 af 3');
    await expect(page.locator(`#planner-import-rotation-${recognised}`)).toContainText('Rotation 2 uger');
    await expect(page.locator(`[data-testid="planner-import-unrecognised-${unrecognised}"]`)).toBeVisible();
    await expect(page.locator(`#planner-import-include-${unrecognised}`)).toHaveCount(0);

    // A recognised history with no shift at all (the grid's own empty rows) is
    // "Ingen plan (timelønnet)" and is not ticked, so it is never imported by accident.
    const allOff = model.workers.filter((w: any) => w.recognised
      && w.days.every((d: any) => !d.planText || /^fri$/i.test(d.planText.trim())));
    expect(allOff.length, 'the grid has made empty rows for the untouched workers').toBeGreaterThan(0);
    for (const worker of allOff) {
      await expect(page.locator(`[data-testid="planner-import-no-plan-${worker.assignedSiteId}"]`))
        .toHaveText('Ingen plan (timelønnet)');
      await expect(page.locator(`#planner-import-include-${worker.assignedSiteId} input[type="checkbox"]`)).not.toBeChecked();
    }

    const includes = page.locator('[data-testid^="planner-import-include-"]');
    for (const include of await includes.all()) {
      const testId = await include.getAttribute('data-testid');
      const checkbox = include.locator('input[type="checkbox"]');
      if (testId === `planner-import-include-${recognised}`) {
        await expect(checkbox).toBeChecked();
      } else if (await checkbox.isChecked()) {
        await checkbox.uncheck();
      }
    }

    // The proposed days, opened for editing: week 1 Monday is A or B, week 2 the other,
    // in the server's canonical form (H:mm-H:mm/pause with a decimal point).
    await page.locator(`#planner-import-edit-${recognised}`).click();
    const firstMonday = await page.locator(`#planner-import-day-${recognised}-0-0`).inputValue();
    const secondMonday = await page.locator(`#planner-import-day-${recognised}-1-0`).inputValue();
    expect([firstMonday, secondMonday].sort()).toEqual(['7:00-15:00/0.5', '9:00-17:00/0.5']);

    // Step 3: apply.
    await page.locator('#planner-import-next').click();
    await expect(page.locator('[data-testid="planner-import-step"]')).toHaveText('Trin 3 af 3');
    await expect(page.locator('[data-testid="planner-import-apply"]')).toContainText('1');
    const apply = waitForPlanner(page, 'POST', /\/planner\/import\/apply$/);
    await page.locator('#planner-import-apply-btn').click();
    const applied = (await expectSuccess(await apply)).model;
    expect(applied.workers.map((r: any) => r.assignedSiteId)).toEqual([recognised]);
    await expect(page.locator('[data-testid="planner-import-done"]')).toContainText('o p');

    const reload = waitForGrid(page);
    await page.locator('#planner-import-cancel').click();
    await expectSuccess(await reload);
    await expect(dialog).toHaveCount(0);
    await expect(rowOf(page, recognised)).toContainText('Rotation 2 uger');

    // The sheet is retired: the import is gone and the way back is offered.
    await expect(page.locator('#planner-import-open')).toHaveCount(0);
    await expect(page.locator('#planner-use-sheet-again')).toBeVisible();
    const restore = waitForPlanner(page, 'PUT', /\/planner\/settings$/);
    const reloadAgain = waitForGrid(page);
    await page.locator('#planner-use-sheet-again').click();
    await expectSuccess(await restore);
    await expectSuccess(await reloadAgain);
    await expect(page.locator('#planner-import-open')).toBeVisible();
    await expect(page.locator('#planner-use-sheet-again')).toHaveCount(0);
  });
});

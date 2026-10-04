import { test, expect } from '@playwright/test';
import {
  closeDayWithoutChange, lastWeekMonday, openDay, useLastWeekDashboard, waitForSpinner,
} from './reconcile-helpers';

/**
 * #1741: the day dialog lists each shift's recorded pauses as clock ranges.
 *
 * The stamps come from the app, which this suite cannot drive, so the grid's index
 * response is given two shift-1 pauses (the primary slot and a sub-slot, out of
 * order) on one day, and every other pause slot of that day is emptied. Nothing is
 * written to the database. That the backend serves the stamps is covered by the C#
 * tests of the index; this covers what the web does with them.
 */

const INDEX_GLOB = '**/api/time-planning-pn/plannings/index';
const DAY = 2;
const API_TIMEOUT = 30000;
const UI_TIMEOUT = 15000;

test.describe('Day dialog: recorded pause times', () => {
  const session = useLastWeekDashboard();

  test('lists both pauses of shift 1 as clock ranges, in clock order', async ({ page }) => {
    const worker = await session.pickWorker(page, 0);

    // Set by the route, so a reload that patched nothing fails here and not as a
    // missing list in the dialog.
    let patchedRows = 0;
    await page.route(INDEX_GLOB, async route => {
      const response = await route.fetch();
      const body = await response.json();
      for (const row of body?.model ?? []) {
        const day = row.planningPrDayModels?.[DAY];
        if (!day?.id) {
          continue;
        }
        const date = `${day.date}`.slice(0, 10);
        for (const key of Object.keys(day)) {
          if (/^pause\d+(StartedAt|StoppedAt)$/.test(key)) {
            day[key] = null;
          }
        }
        day.pause1StartedAt = `${date}T12:30:00Z`;
        day.pause1StoppedAt = `${date}T12:45:00Z`;
        day.pause10StartedAt = `${date}T10:02:00Z`;
        day.pause10StoppedAt = `${date}T10:17:00Z`;
        day.pause1OverrideMinutes = null;
        patchedRows++;
      }
      await route.fulfill({ response, json: body });
    });

    const monday = lastWeekMonday();
    const reload = page.waitForResponse(r =>
      r.url().includes('/api/time-planning-pn/plannings/index')
      && r.request().method() === 'POST'
      && `${r.request().postDataJSON()?.dateFrom ?? ''}`.startsWith(monday),
    { timeout: API_TIMEOUT });
    await page.locator('#workingHoursReload').click();
    expect((await reload).status(), 'the grid reload (POST plannings/index)').toBeLessThan(400);
    await waitForSpinner(page);
    expect(patchedRows, `the reload must carry a registration on day ${DAY} to patch`).toBeGreaterThan(0);

    await openDay(page, worker, DAY);

    const segments = page.locator('mat-dialog-container [data-testid="pauseSegments1"] [data-testid="pauseSegment1"]');
    await expect(segments).toHaveCount(2, { timeout: UI_TIMEOUT });
    await expect(segments.nth(0)).toContainText('10:02–10:17');
    await expect(segments.nth(1)).toContainText('12:30–12:45');
    // Shift 2 recorded no pause, so it lists none.
    await expect(page.locator('mat-dialog-container [data-testid="pauseSegments2"]')).toHaveCount(0);

    await closeDayWithoutChange(page);
  });
});

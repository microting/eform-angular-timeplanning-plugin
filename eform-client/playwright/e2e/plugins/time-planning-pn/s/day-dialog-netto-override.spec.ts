import { test, expect, Page } from '@playwright/test';
import {
  cellOf, closeDayWithoutChange, lastWeekMonday, openDay, useLastWeekDashboard, waitForSpinner,
} from './reconcile-helpers';

/**
 * Regressions for an active netto-hours override. When nettoHoursOverrideActive is
 * set, nettoHoursOverride IS the day's effective netto, and the backend books
 * flex = effective netto - planHours.
 *
 * The grid's index response is patched for one past day, so the state under test is
 * exact and nothing is written to the database (the shard shares one DB and runs its
 * files in order). The patched shape is the one the server serves after a save with
 * the override on: actualHours stays the registrations' netto (PlanRegistrationHelper
 * maps ActualHours = NettoHours), and the override travels in its own two fields.
 */

const INDEX_GLOB = '**/api/time-planning-pn/plannings/index';
const DAY = 3;
const API_TIMEOUT = 30000;
const UI_TIMEOUT = 15000;

type DayPatch = (day: Record<string, any>) => void;

/** Empties every recorded registration of the day, so only the override carries netto. */
function clearRegistrations(day: Record<string, any>): void {
  for (const key of Object.keys(day)) {
    if (/^(start|stop|pause)\d+(Id|StartedAt|StoppedAt|OverrideMinutes)$/.test(key)) {
      day[key] = null;
    }
  }
  day.actualHours = 0;
}

/** Patches column DAY of every row in the index response, reloads the grid, asserts it took. */
async function reloadWithPatchedDay(page: Page, patch: DayPatch): Promise<void> {
  let patchedRows = 0;
  await page.route(INDEX_GLOB, async route => {
    const response = await route.fetch();
    const body = await response.json();
    for (const row of body?.model ?? []) {
      const day = row.planningPrDayModels?.[DAY];
      // Every row, saved or not: the picked worker's day must be patched whatever
      // the seed holds for it.
      if (!day) {
        continue;
      }
      patch(day);
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
  expect(patchedRows, `the reload must carry a day at column ${DAY} to patch`).toBeGreaterThan(0);
}

test.describe('Netto-hours override: dialog flex and grid hours', () => {
  const session = useLastWeekDashboard();

  test('day dialog Flex uses the active override on open (7.4 plan, 7.4 override => 0.00)', async ({ page }) => {
    const worker = await session.pickWorker(page, 0);

    await reloadWithPatchedDay(page, day => {
      clearRegistrations(day);
      day.planHours = 7.4;
      day.nettoHoursOverrideActive = true;
      day.nettoHoursOverride = 7.4;
    });

    // Open, close without touching anything, and reopen: the value on open is what
    // the admin reads, and it must not need an edit to become right.
    for (const pass of ['first open', 'reopen']) {
      await openDay(page, worker, DAY);

      // Preconditions, so a failure below is about Flex and not about the setup.
      await expect(page.locator('#planHours'), `${pass}: plan hours`).toHaveValue('7.4', { timeout: UI_TIMEOUT });
      await expect(page.locator('#nettoHours'), `${pass}: registrations netto`).toHaveValue('0.00');
      await expect(page.locator('#nettoHoursOverride'), `${pass}: the override field must be shown with its value`)
        .toHaveValue('7.4');

      await expect(
        page.locator('#todaysFlex'),
        `${pass}: Flex must be override - plan = 7.4 - 7.4 = 0.00, not registrations - plan = -7.40`,
      ).toHaveValue('0.00');

      await closeDayWithoutChange(page);
    }
  });

  test('grid cell shows the hours line for an active override of 0', async ({ page }) => {
    const worker = await session.pickWorker(page, 0);

    await reloadWithPatchedDay(page, day => {
      clearRegistrations(day);
      day.planHours = 7.4;
      day.nettoHoursOverrideActive = true;
      day.nettoHoursOverride = 0;
    });

    const hours = cellOf(page, worker, DAY).locator('[id^="totalWorkingHours"]');
    await expect(
      hours,
      'an active override of 0 is a real effective netto and must be shown, like any other override value',
    ).toHaveCount(1, { timeout: UI_TIMEOUT });
    // formatDuration(0) in Danish: "0 t 0 min (0.00 timer)".
    await expect(hours, 'the override line must read zero hours').toContainText('0 t 0 min');
    await expect(hours).toContainText('0.00 timer');
  });
});

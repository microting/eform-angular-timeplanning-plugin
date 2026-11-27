import { Page, test, expect } from '@playwright/test';
import {
  closeDayWithoutChange, lastWeekMonday, openDay, useLastWeekDashboard, waitForSpinner,
} from './reconcile-helpers';

/**
 * The day dialog on a 5-minute site must decode a legacy pause tick the canonical way.
 *
 * A pause tick id is minutes / 5 + 1 and decodes as (id - 1) * 5 (base
 * FlexChain.ComputeShiftPauseSeconds; id 0 = no pause). When a shift has no pause
 * stamps and no override, the dialog falls back to the tick, and must therefore show
 * Pause1Id 13 as 01:00 — the 60 min every netto and flex calculation deducts — and
 * Pause3Id 7 as 00:30. Saving that dialog unchanged must send the same tick back.
 *
 * As in day-dialog-pause-times.spec.ts the backend is not asked to hold such a row:
 * the grid's index response and the assigned-site response are patched, and the save
 * is answered by the route, so nothing is written to the database.
 */

const INDEX_GLOB = '**/api/time-planning-pn/plannings/index';
const DAY = 2;
const API_TIMEOUT = 30000;
const UI_TIMEOUT = 15000;
const PLANNING_SAVE_PATH = /\/api\/time-planning-pn\/plannings\/\d+$/;

/** A 5-minute site with a third shift, whatever the seeded site says. */
async function patchAssignedSite(page: Page): Promise<void> {
  await page.route(
    url => url.pathname.endsWith('/api/time-planning-pn/settings/assigned-sites'),
    async route => {
      if (route.request().method() !== 'GET') {
        await route.continue();
        return;
      }
      const response = await route.fetch();
      const body = await response.json();
      if (body?.model) {
        body.model.useOneMinuteIntervals = false;
        body.model.thirdShiftActive = true;
      }
      await route.fulfill({ response, json: body });
    });
}

/**
 * Gives the day shift 1 07:00-15:00 with Pause1Id 13 and, with `withShift3`, shift 3
 * 16:00-20:00 with Pause3Id 7; no pause stamps and no overrides anywhere. Returns a
 * counter of patched rows, so a reload that patched nothing fails before the dialog
 * is read.
 */
async function patchIndexWithTickOnlyPauses(page: Page, withShift3: boolean): Promise<{ rows: number }> {
  const patched = { rows: 0 };
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
        if (/^pause\d+(StartedAt|StoppedAt)$/.test(key) || /^pause\dOverrideMinutes$/.test(key)) {
          day[key] = null;
        }
      }
      for (const shift of withShift3 ? [2, 4, 5] : [2, 3, 4, 5]) {
        day[`start${shift}Id`] = null;
        day[`stop${shift}Id`] = null;
        day[`pause${shift}Id`] = null;
        day[`start${shift}StartedAt`] = null;
        day[`stop${shift}StoppedAt`] = null;
      }
      day.start1Id = 85; // 07:00
      day.stop1Id = 181; // 15:00
      day.start1StartedAt = `${date}T07:00:00Z`;
      day.stop1StoppedAt = `${date}T15:00:00Z`;
      day.pause1Id = 13; // (13 - 1) * 5 = 60 min
      day.break1Shift = 13;
      if (withShift3) {
        day.start3Id = 193; // 16:00
        day.stop3Id = 241; // 20:00
        day.start3StartedAt = `${date}T16:00:00Z`;
        day.stop3StoppedAt = `${date}T20:00:00Z`;
        day.pause3Id = 7; // (7 - 1) * 5 = 30 min
        day.break3Shift = 7;
      }
      day.nettoHoursOverrideActive = false;
      patched.rows++;
    }
    await route.fulfill({ response, json: body });
  });
  return patched;
}

async function reloadGrid(page: Page, patched: { rows: number }): Promise<void> {
  const monday = lastWeekMonday();
  const reload = page.waitForResponse(r =>
    r.url().includes('/api/time-planning-pn/plannings/index')
    && r.request().method() === 'POST'
    && `${r.request().postDataJSON()?.dateFrom ?? ''}`.startsWith(monday),
  { timeout: API_TIMEOUT });
  await page.locator('#workingHoursReload').click();
  expect((await reload).status(), 'the grid reload (POST plannings/index)').toBeLessThan(400);
  await waitForSpinner(page);
  expect(patched.rows, `the reload must carry a registration on day ${DAY} to patch`).toBeGreaterThan(0);
}

test.describe('Day dialog: legacy pause tick decoding (5-minute site)', () => {
  const session = useLastWeekDashboard();

  test('shows Pause1Id 13 as 01:00 and Pause3Id 7 as 00:30', async ({ page }) => {
    const worker = await session.pickWorker(page, 0);
    await patchAssignedSite(page);
    const patched = await patchIndexWithTickOnlyPauses(page, true);
    await reloadGrid(page, patched);

    await openDay(page, worker, DAY);

    await expect(
      page.locator('mat-dialog-container [data-testid="pause1Id"]'),
      'Pause1Id 13 with no stamps or override is a 60 min pause ((13 - 1) * 5), so the '
      + 'dialog must show 01:00. 01:05 means it decoded the tick as id * 5.',
    ).toHaveValue('01:00', { timeout: UI_TIMEOUT });
    await expect(
      page.locator('mat-dialog-container [data-testid="pause3Id"]'),
      'Pause3Id 7 is a 30 min pause ((7 - 1) * 5), so the dialog must show 00:30. '
      + '00:35 means it decoded the tick as id * 5.',
    ).toHaveValue('00:30', { timeout: UI_TIMEOUT });

    await closeDayWithoutChange(page);
  });

  test('saving the dialog unchanged sends Pause1Id 13 back, not 14', async ({ page }) => {
    const worker = await session.pickWorker(page, 0);
    await patchAssignedSite(page);
    // Shift 1 only, so nothing else on the day can hold the save back.
    const patched = await patchIndexWithTickOnlyPauses(page, false);
    await reloadGrid(page, patched);

    // Answer the save here, so the database is never touched; the payload is what
    // this test is about.
    const saved: { body: any } = { body: null };
    await page.route(
      url => PLANNING_SAVE_PATH.test(url.pathname),
      async route => {
        if (route.request().method() !== 'PUT') {
          await route.continue();
          return;
        }
        saved.body = route.request().postDataJSON();
        await route.fulfill({ json: { success: true, message: '' } });
      });

    await openDay(page, worker, DAY);
    await expect(page.locator('mat-dialog-container [data-testid="pause1Id"]')).not.toHaveValue('', { timeout: UI_TIMEOUT });
    await expect(page.locator('#saveButton')).toBeEnabled({ timeout: UI_TIMEOUT });
    await page.locator('#saveButton').click();
    await expect(page.locator('mat-dialog-container')).toHaveCount(0, { timeout: UI_TIMEOUT });
    await expect.poll(() => saved.body !== null, { message: 'the dialog save must PUT plannings/{id}', timeout: API_TIMEOUT })
      .toBe(true);

    const body = saved.body;
    expect(body.pause1Id,
      'An unchanged save of a row with Pause1Id 13 (60 min) must send Pause1Id 13 back. '
      + `It sent ${body.pause1Id} = ${(body.pause1Id - 1) * 5} min: the dialog showed 13 * 5 = 65 min `
      + 'and re-encoded that as 65 / 5 + 1 = 14, adding 5 min of pause on every save.',
    ).toBe(13);
    expect(body.pause1OverrideMinutesSpecified ?? false,
      'An unchanged save must not set a pause override.').toBe(false);
  });
});

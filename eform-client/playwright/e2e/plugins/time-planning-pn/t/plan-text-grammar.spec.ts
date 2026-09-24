import { expect, Page, Response, test } from '@playwright/test';
import { LoginPage } from '../../../Page objects/Login.page';
import { TimePlanningWorkingHoursPage } from '../TimePlanningWorkingHours.page';
import { selectDateRangeOnNewDatePicker } from '../../../helper-functions';

/**
 * PlanText grammar, end to end.
 *
 * PlanText is typed into the working-hours grid and parsed SERVER-side; the Angular
 * side treats it as an opaque string. Nothing in the client parses it, so the only
 * way to prove the parser is wired up is to type a string, let the server read it,
 * and read back the shift columns and planned hours it derived.
 *
 * Where the parse actually happens is worth knowing, because it is not the save. The
 * seed holds no planning for a week two ahead, so the working-hours PUT takes the
 * CreatePlanning branch, which stores PlanText verbatim and never parses it. The
 * derivation happens on the next dashboard load, in UpdatePlanRegistrationsInPeriod,
 * and is persisted there. So the flow below — fill, save, open the dashboard, read —
 * is not three conveniences in a row; the dashboard load is the step under test.
 *
 * Two server-side gates decide whether the text is parsed at all, and the fixture
 * respects both: the day must be after the day of payment (hence a future week), and
 * PlanChangedByAdmin must be false (hence the day dialog is only ever read and
 * cancelled, never saved — saving is the one thing that sets that flag).
 *
 * The grammar itself is exhaustively covered by unit tests in
 * Microting.TimePlanningBase. This fixture covers the wiring, and every case is one
 * that produced a different answer through this exact UI path before the shared
 * parser landed.
 *
 * The shard ships no seed of its own, so CI falls back to shard `a`'s — that is where
 * WORKER comes from. A seed added here later must still contain that worker.
 *
 * waitForSpinner and waitForIndex are duplicated here rather than imported from
 * `../s/reconcile-helpers`: no spec in this suite imports across shards, and that
 * module is shard s's contract — it mandates last-week and name-addressed rows, both
 * of which this fixture deliberately inverts. If the duplication is ever worth paying
 * off, the helpers move up beside TimePlanningWorkingHours.page.ts, not sideways.
 */

const WORKER = 'c d';

/**
 * Seeded into every day's hours column before any plan text is typed. It only has to
 * be a value no case derives, so "the hours column still reads 7.4" can only mean the
 * parser left it alone.
 */
const SEED_PLAN_HOURS = '7.4';

/** Shared by mondayAhead() and the dashboard's forward clicks — they must step alike. */
const WEEKS_AHEAD = 2;

const WORKING_HOURS_INDEX = '/api/time-planning-pn/working-hours/index';
const PLANNINGS_INDEX = '/api/time-planning-pn/plannings/index';

/** Monday of the week `weeksAhead` weeks from today, in local time. */
function mondayAhead(weeksAhead: number): Date {
  const base = new Date();
  base.setHours(0, 0, 0, 0);
  base.setDate(base.getDate() + weeksAhead * 7);
  const sinceMonday = (base.getDay() + 6) % 7;
  base.setDate(base.getDate() - sinceMonday);
  return base;
}

function isoDate(date: Date): string {
  const pad = (n: number) => String(n).padStart(2, '0');
  return `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())}`;
}

const weekMonday = mondayAhead(WEEKS_AHEAD);
const weekSunday = new Date(weekMonday);
weekSunday.setDate(weekMonday.getDate() + 6);

/**
 * One typed cell and everything the server should derive from it.
 *
 * `planHours` is the load-bearing assertion throughout: it is computed from all five
 * parsed shifts less their breaks, so a wrong start, a dropped break or a lost segment
 * all show up in it. `''` means the field must be empty. `why` is a field rather than
 * a comment because CI shows the assertion message, not the file.
 */
const cases = [
  {
    text: '8-16/1.5',
    start: '08:00', end: '16:00', break: '01:30', planHours: '6.5',
    why: 'a break longer than an hour. The old table capped at "1" and mapped 1.5 to zero, so the break was paid as worked time and this read 8.',
  },
  {
    text: '6.3-16/1',
    start: '06:30', end: '16:00', break: '01:00', planHours: '8.5',
    why: 'a single-digit fraction meaning half past. The old parser read ".3" as three minutes, so this started at 06:03.',
  },
  {
    text: '7,5-16',
    start: '07:30', end: '16:00', break: '', planHours: '8.5',
    why: 'the same shorthand the other way round, with a Danish comma. The old parser read ",5" as five minutes and started at 07:05.',
  },
  {
    text: '8-16/2 helligdag',
    start: '08:00', end: '16:00', break: '02:00', planHours: '6',
    why: 'a break with a word after it. The old table matched the whole token and fell through to zero.',
  },
  {
    text: '8-16 hjemme',
    start: '08:00', end: '16:00', break: '', planHours: '8',
    why: 'a note after the shift. The old parser threw on this, and nothing caught it until the import had aborted for every later worker and date.',
  },
  {
    text: '6-8/0.5;9-11/0.5;12-14/1;15-17/1.5;18-20/0.25',
    start: '06:00', end: '08:00', break: '00:30', planHours: '6.25',
    why: 'five segments each carrying a break. The old parser read all five but zeroed the 1.5 on shift 4, giving 7.75; the whole-day total is what catches that.',
  },
  {
    text: 'Ferie',
    start: '', end: '', break: '', planHours: SEED_PLAN_HOURS,
    why: 'text that is not a shift. The shift columns clear but the hours column must survive, because the sheet owns it for such a row. This held before the migration too — it guards the regression introduced while fixing the others, where non-shift text briefly zeroed PlanHours and seeded the flex chain from it.',
  },
];

// Rows and cells are addressed positionally off this array — grid day rows are 1..7
// (row 0 is the carry-over row) and dashboard day cells are 0..6. One case per day is
// therefore not a formatting choice: an eighth case would address an element that does
// not exist and fail as an opaque locator timeout instead of this.
if (cases.length !== 7) {
  throw new Error(`the fixture fills one day of the week per case, so it needs exactly 7, not ${cases.length}`);
}

async function waitForSpinner(page: Page): Promise<void> {
  if (await page.locator('.overlay-spinner').count() > 0) {
    await page.locator('.overlay-spinner').waitFor({ state: 'hidden', timeout: 30000 });
  }
}

/** With `dateFrom` it only accepts a load of that period, never a late earlier one. */
function waitForIndex(page: Page, path: string, dateFrom?: string): Promise<Response> {
  return page.waitForResponse(r =>
    r.url().includes(path)
    && r.request().method() === 'POST'
    && (dateFrom === undefined || `${r.request().postDataJSON()?.dateFrom ?? ''}`.startsWith(dateFrom)));
}

/** Shared setup. Both tests need the same grid, on the same week, for the same worker. */
async function openWorkingHoursOnTestWeek(page: Page): Promise<void> {
  const whPage = new TimePlanningWorkingHoursPage(page);

  await page.locator('mat-nested-tree-node').filter({ hasText: 'Timeregistrering' }).click();
  await waitForSpinner(page);
  await page.locator('mat-tree-node').filter({ hasText: 'Timeregistrering' }).click();
  await waitForSpinner(page);

  // The filters live behind the toolbar's expand button; the click target is the
  // button, not the ripple child the selector finds.
  await page.locator('mat-toolbar > div > button .mat-mdc-button-persistent-ripple').first().locator('..').click();
  // Picking a worker fires no load here: filtersChangedEmmit bails while dateFrom is
  // still null, and it starts null. The range below is what loads the grid.
  await page.locator('#workingHoursSite').locator('input').fill(WORKER);
  await page.locator('.ng-option.ng-option-marked').click();

  const loaded = waitForIndex(page, WORKING_HOURS_INDEX);
  await whPage.dateFormInput().click();
  await selectDateRangeOnNewDatePicker(
    page,
    weekMonday.getFullYear(), weekMonday.getMonth() + 1, weekMonday.getDate(),
    weekSunday.getFullYear(), weekSunday.getMonth() + 1, weekSunday.getDate(),
  );
  await loaded;
  await waitForSpinner(page);
}

// Serial: the second test reads what the first one's dashboard load persisted. Without
// this it would fail with "expected 6.5, received 7.4" whenever the first test broke,
// which reads exactly like a parser bug rather than a cascade.
test.describe.serial('PlanText grammar through the working-hours grid', () => {
  test.beforeEach(async ({ page }) => {
    test.setTimeout(180000);
    await page.goto('http://localhost:4200');
    await new LoginPage(page).login();
  });

  test('derives shifts, breaks and planned hours from the plan text', async ({ page }) => {
    await openWorkingHoursOnTestWeek(page);

    // Row 0 is the synthetic carry-over row built from the planning before the period,
    // so the week's seven days are rows 1..7.
    //
    // Seed every day's hours first. For the shift rows this proves shift text OVERRIDES
    // the hours column; for the "Ferie" row it proves the opposite.
    for (let i = 0; i < cases.length; i++) {
      await page.locator(`#planHours${i + 1}`).locator('input').fill(SEED_PLAN_HOURS);
    }

    for (let i = 0; i < cases.length; i++) {
      await page.locator(`#planText${i + 1}`).locator('input').fill(cases[i].text);
    }

    const saved = page.waitForResponse(r =>
      r.url().includes('/api/time-planning-pn/working-hours') && r.request().method() === 'PUT');
    await page.locator('#workingHoursSave').click();
    await saved;
    await waitForSpinner(page);

    // The dashboard load is the step that parses; the save above only stored the text.
    await page.locator('mat-nested-tree-node').filter({ hasText: 'Timeregistrering' }).click();
    const dashboardLoaded = waitForIndex(page, PLANNINGS_INDEX);
    await page.locator('mat-tree-node').filter({ hasText: 'Dashboard' }).click();
    await dashboardLoaded;
    await waitForSpinner(page);

    // The dashboard lists every worker, so it has to be filtered to this one before the
    // cell ids mean anything: `#cell0_{day}` is positional, and without the filter row 0
    // is whichever worker happens to sort first. Registered before the click that fires
    // it — onSiteChanged reloads synchronously, and a listener armed afterwards would
    // wait for a response that has already landed.
    const filtered = waitForIndex(page, PLANNINGS_INDEX);
    await page.locator('#workingHoursSite').locator('input').fill(WORKER);
    await page.locator('.ng-option.ng-option-marked').click();
    await filtered;
    await waitForSpinner(page);
    await expect(page.locator('[id^="firstColumn"]'), 'the filter must leave one row').toHaveCount(1);
    await expect(page.locator('#firstColumn0 .hours-info strong'), 'and it must be this worker')
      .toHaveText(new RegExp(`^\\s*${WORKER}\\s*$`));

    // The dashboard opens on the current week; the test week is WEEKS_AHEAD ahead. The
    // last hop is pinned to the expected dateFrom so a late earlier response cannot
    // satisfy it and leave the cells on the wrong week.
    for (let week = 1; week <= WEEKS_AHEAD; week++) {
      const target = week === WEEKS_AHEAD ? isoDate(weekMonday) : undefined;
      const forward = waitForIndex(page, PLANNINGS_INDEX, target);
      await page.locator('#forwards').click();
      await forward;
      await waitForSpinner(page);
    }

    for (const testCase of cases) {
      const day = cases.indexOf(testCase);
      const cell = page.locator(`#cell0_${day}`);
      await cell.scrollIntoViewIfNeeded();
      await cell.click();

      // Scoped to the dialog: the grid behind it carries #planHours1..7 too.
      const dialog = page.locator('mat-dialog-container');
      const planHours = dialog.locator('#planHours');
      await planHours.waitFor({ state: 'visible', timeout: 15000 });
      await planHours.scrollIntoViewIfNeeded();

      await expect(planHours, `${testCase.text} — ${testCase.why}`).toHaveValue(testCase.planHours);
      await expect(dialog.locator('[data-testid="plannedStartOfShift1"]'), `${testCase.text} start`)
        .toHaveValue(testCase.start);
      await expect(dialog.locator('[data-testid="plannedEndOfShift1"]'), `${testCase.text} end`)
        .toHaveValue(testCase.end);
      await expect(dialog.locator('[data-testid="plannedBreakOfShift1"]'), `${testCase.text} break`)
        .toHaveValue(testCase.break);

      // Cancel, never save: saving sets PlanChangedByAdmin, after which the server stops
      // deriving the columns from PlanText and every later case would read whatever the
      // dialog last held.
      await page.locator('#cancelButton').click();
      await expect(dialog).toHaveCount(0);
    }
  });

  test('keeps the derived values on reload, so what was parsed is what was stored', async ({ page }) => {
    await openWorkingHoursOnTestWeek(page);

    // Playwright gives every test a fresh browser context, so this is a cold read:
    // nothing the previous test typed is still around to satisfy these, and the grid has
    // to re-fetch from the database. That is why it lives in its own test — folding it
    // into the first would only prove the page still holds what it just typed.
    for (let i = 0; i < cases.length; i++) {
      await expect(page.locator(`#planText${i + 1}`).locator('input')).toHaveValue(cases[i].text);
    }

    // The hours column is the one value a non-shift row must keep, and a shift row must
    // have had overwritten.
    await expect(
      page.locator(`#planHours${cases.length}`).locator('input'),
      'Ferie must leave the hours column alone',
    ).toHaveValue(SEED_PLAN_HOURS);

    await expect(
      page.locator('#planHours1').locator('input'),
      'a shift text must own the hours column',
    ).toHaveValue('6.5');
  });
});

import { expect, Locator, Page, Response, test } from '@playwright/test';
import { LoginPage } from '../../../Page objects/Login.page';

/**
 * Shared by every spec in shard s. Two rules are enforced here so no spec can forget
 * them:
 *
 *  1. Rows are found by WORKER NAME, never by grid position. The `#cell{row}_{day}`
 *     ids are positional, and a row shift silently addresses another worker. That is
 *     exactly how e1m/dashboard-edit-multishift.spec.ts once failed. A position is
 *     read once per spec, to PICK a worker, and never again.
 *  2. The grid is always opened on LAST week, so every visible day is in the past.
 *     Reconcile is refused for today and the future (I2), and the current week has
 *     no past day at all on a Monday.
 *
 * Every spec uses its own worker and unlocks what it locked, because the shard shares
 * one database and runs its specs in file order (workers: 1). `useLastWeekDashboard`
 * holds both rules: it is the only door to a worker name, and it installs the login,
 * the last-week navigation and the cleanup as hooks, so no spec can forget them.
 */

const INDEX_PATH = '/api/time-planning-pn/plannings/index';
export const RECONCILE_PATH = /\/api\/time-planning-pn\/plannings\/\d+\/reconcile$/;
export const UNRECONCILE_PATH = /\/api\/time-planning-pn\/plannings\/\d+\/unreconcile$/;
export const RECONCILE_THROUGH_PATH = /\/api\/time-planning-pn\/plannings\/reconcile-through$/;
/** Any write to a day: a save (PUT plannings/{id}) as well as reconcile and unlock. */
export const PLANNING_PUT_PATH = /\/api\/time-planning-pn\/plannings\//;
/** Danish, like the rest of the suite: CI runs the UI in Danish. */
export const UNLOCK_WORD = 'LÅS OP';
export const LOCKED_TOOLTIP = 'Låst · ligger før en afstemt dag';
export const PROVENANCE = /^Afstemt \d{2}\.\d{2}\.\d{4} kl\. \d{2}:\d{2}$/;

export async function waitForSpinner(page: Page): Promise<void> {
  if (await page.locator('.overlay-spinner').count() > 0) {
    await page.locator('.overlay-spinner').waitFor({ state: 'hidden', timeout: 30000 });
  }
}

/** The grid's index POST. With `dateFrom` it only accepts a load of that period. */
export function waitForIndex(page: Page, dateFrom?: string): Promise<Response> {
  return page.waitForResponse(r =>
    r.url().includes(INDEX_PATH)
    && r.request().method() === 'POST'
    && (dateFrom === undefined || `${r.request().postDataJSON()?.dateFrom ?? ''}`.startsWith(dateFrom)));
}

/** Matches on the pathname: a bare includes('/reconcile') also matches /unreconcile. */
export function waitForPut(page: Page, path: RegExp): Promise<Response> {
  return page.waitForResponse(r =>
    r.request().method() === 'PUT' && path.test(new URL(r.url()).pathname));
}

/** Counts the PUTs matching `path` from now on. Read `.count` when needed. */
export function countPuts(page: Page, path: RegExp): { count: number } {
  const counter = { count: 0 };
  page.on('request', req => {
    if (req.method() === 'PUT' && path.test(new URL(req.url()).pathname)) {
      counter.count++;
    }
  });
  return counter;
}

/** OperationResult failures come back as HTTP 200 with success:false, so check both. */
export async function expectSuccess(response: Response): Promise<any> {
  expect(response.status(), `${response.url()} HTTP status`).toBeLessThan(400);
  const body = await response.json();
  expect(body.success, `${response.url()} failed: ${body.message}`).toBe(true);
  return body;
}

/** Last week's Monday as yyyy-MM-dd, in the same local calendar the grid's dateFrom uses. */
export function lastWeekMonday(): string {
  const d = new Date();
  d.setHours(0, 0, 0, 0);
  const sinceMonday = (d.getDay() + 6) % 7;
  d.setDate(d.getDate() - sinceMonday - 7);
  const pad = (n: number) => String(n).padStart(2, '0');
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}`;
}

/**
 * Deliberately not exported: `useLastWeekDashboard` is the only caller, so no spec can
 * open the grid on a week whose days are not all in the past.
 */
async function openDashboardLastWeek(page: Page): Promise<void> {
  await page.locator('mat-nested-tree-node').filter({ hasText: 'Timeregistrering' }).click();
  const initial = waitForIndex(page);
  await page.locator('mat-tree-node').filter({ hasText: 'Dashboard' }).click();
  await initial;
  await waitForSpinner(page);
  // Filtered on dateFrom, so a late current-week response cannot satisfy the wait.
  const monday = lastWeekMonday();
  const lastWeek = waitForIndex(page, monday);
  await page.locator('#backwards').click();
  const response = await lastWeek;
  expect(response.request().postDataJSON().dateFrom, 'the grid must be on last week').toMatch(new RegExp(`^${monday}`));
  await waitForSpinner(page);
}

/**
 * The worker rendered at a grid position. Deliberately not exported: the only way in
 * is `DashboardSession.pickWorker`, which also registers the row for cleanup.
 */
async function workerAtRow(page: Page, rowIndex: number): Promise<string> {
  const name = (await page.locator(`#firstColumn${rowIndex} .hours-info strong`).innerText()).trim();
  // A blank name would make every later lookup match vacuously.
  expect(name, `row ${rowIndex} must name a worker`).toMatch(/^[^\s-]/);
  return name;
}

function escapeRegExp(value: string): string {
  return value.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
}

/** The grid row for a worker, found by name, never by position. */
export function rowOf(page: Page, worker: string): Locator {
  return page.locator('#main-header-text tr.mat-mdc-row').filter({
    has: page.locator('.hours-info strong', {
      hasText: new RegExp(`^\\s*${escapeRegExp(worker)}\\s*$`),
    }),
  });
}

export function rowCheckbox(page: Page, worker: string): Locator {
  return rowOf(page, worker).locator('td.mtx-grid-checkbox-cell input[type="checkbox"]');
}

/** A worker's day cell (.plan-container). day = column index, 0 = first visible day. */
export function cellOf(page: Page, worker: string, day: number): Locator {
  return rowOf(page, worker).locator(`.plan-container[id^="cell"][id$="_${day}"]`);
}

/** The <td> mtx-grid stamps getCellClass onto. */
export function tdOf(page: Page, worker: string, day: number): Locator {
  return cellOf(page, worker, day).locator('xpath=..');
}

export async function dialogTitle(page: Page): Promise<{ worker: string; date: string }> {
  const title = page.locator('mat-dialog-container [mat-dialog-title]');
  await expect(title).toBeVisible({ timeout: 10000 });
  // "<name> - <dd.MM.yyyy> (<id>)", with the date on its own line.
  const raw = (await title.innerText()).replace(/\s+/g, ' ').trim();
  return {
    worker: raw.split(/\s+-\s+/)[0].trim(),
    date: /(\d{2}\.\d{2}\.\d{4})/.exec(raw)?.[1] ?? '',
  };
}

/** Opens a day, asserts the dialog is that worker's, and returns the date as dd.MM.yyyy. */
export async function openDay(page: Page, worker: string, day: number): Promise<string> {
  const cell = cellOf(page, worker, day);
  await expect(cell).toHaveCount(1);
  await cell.scrollIntoViewIfNeeded();
  await cell.click();
  const title = await dialogTitle(page);
  expect(title.worker, 'the dialog must belong to the worker the row was found by').toBe(worker);
  expect(title.date).toMatch(/^\d{2}\.\d{2}\.\d{4}$/);
  return title.date;
}

/** A locked day's dialog: it opens and reads, but offers no way to change anything. */
export async function assertReadOnlyDialog(page: Page): Promise<void> {
  await expect(page.locator('#saveButton')).toHaveCount(0);
  await expect(page.locator('#unlockButton')).toHaveCount(0);
}

/** Cancel with nothing persisted: no save and no reload. */
export async function closeDayWithoutChange(page: Page): Promise<void> {
  await page.locator('#cancelButton').click();
  await expect(page.locator('mat-dialog-container')).toHaveCount(0);
}

/** Close after a reconcile made in the dialog: the table reloads the grid instead of saving. */
export async function closeDayAfterLockChange(page: Page): Promise<void> {
  const reload = waitForIndex(page, lastWeekMonday());
  await page.locator('#cancelButton').click();
  await reload;
  await waitForSpinner(page);
}

/** Two-step reconcile from the open dialog (§8.2). The dialog stays open, read-only. */
export async function reconcileOpenDay(page: Page): Promise<void> {
  await page.locator('#reconcileButton').click();
  await expect(page.locator('#reconcileConfirmButton')).toBeVisible();
  const put = waitForPut(page, RECONCILE_PATH);
  await page.locator('#reconcileConfirmButton').click();
  await expectSuccess(await put);
  await expect(page.locator('#reconciledProvenanceText')).toHaveText(PROVENANCE);
}

/** Reconciles one day and returns its date (dd.MM.yyyy), with the grid reloaded. */
export async function reconcileDay(page: Page, worker: string, day: number): Promise<string> {
  const date = await openDay(page, worker, day);
  await reconcileOpenDay(page);
  await closeDayAfterLockChange(page);
  return date;
}

/** Typed-word unlock of the open boundary day (§8.4). The dialog closes and the grid reloads. */
export async function unlockOpenDay(page: Page): Promise<void> {
  await page.locator('#unlockButton').click();
  await page.locator('#unlockWordInput').fill(UNLOCK_WORD);
  const put = waitForPut(page, UNRECONCILE_PATH);
  const reload = waitForIndex(page, lastWeekMonday());
  await page.locator('#unlockConfirmButton').click();
  await expectSuccess(await put);
  await reload;
  await waitForSpinner(page);
}

export async function unlockDay(page: Page, worker: string, day: number): Promise<void> {
  await openDay(page, worker, day);
  await unlockOpenDay(page);
}

/** Leaves no dialog open, whatever the test was in the middle of. */
async function closeAnyDialog(page: Page): Promise<void> {
  const dialog = page.locator('mat-dialog-container');
  // The dialog sets disableClose while a PUT is in flight, and an Escape sent in that
  // window is swallowed for good. So press until the dialog actually goes, instead of
  // pressing once and waiting on a key that was already dropped.
  for (let attempt = 0; attempt < 20; attempt++) {
    if (await dialog.count() === 0) {
      return;
    }
    await page.keyboard.press('Escape');
    await page.waitForTimeout(500);
  }
  throw new Error('a dialog stayed open, so the row could not be cleaned up');
}

/**
 * Re-reads the grid from the server. Cleanup cannot trust what is on screen: a test
 * that failed between a lock change and the reload that follows it leaves a grid still
 * drawing the row as open. A scan of that stale grid finds nothing to unlock and
 * strands the lock for every spec after it.
 */
async function reloadGridFromServer(page: Page): Promise<void> {
  await waitForSpinner(page);
  const reload = waitForIndex(page, lastWeekMonday());
  await page.locator('#workingHoursReload').click();
  await reload;
  await waitForSpinner(page);
}

function describeError(error: unknown): string {
  return error instanceof Error ? error.message.split('\n')[0] : String(error);
}

/**
 * What the scan could establish about one row. `unknown` is a weaker claim than
 * `locked` and a different one from `clean`: it means a lock could be sitting there
 * unseen, which is why the two are reported separately.
 */
type RowVerdict =
  | { state: 'clean' }
  | { state: 'locked'; detail: string }
  | { state: 'unknown'; detail: string };

/**
 * Takes every mark off a worker's row, the newest first, because only the boundary can
 * be unlocked. Reports a verdict instead of throwing: the hook needs to know what the
 * row ended up as, and an exception would also cost it every row after this one.
 * Expects a freshly loaded grid.
 */
async function unlockRow(page: Page, worker: string): Promise<RowVerdict> {
  const boundary = rowOf(page, worker).locator('td.reconciled-background');

  /** The row's marks, or `null` when the row could not be looked at at all. */
  const marksOnRow = async (): Promise<number | null> => {
    try {
      // An empty grid would count 0 marks and read as clean, so the row itself has to
      // be on screen before a count of its cells means anything.
      if (await rowOf(page, worker).count() === 0) {
        return null;
      }
      return await boundary.count();
    } catch {
      return null;
    }
  };

  const unseen: RowVerdict = {
    state: 'unknown',
    detail: `${worker}: the row was not on screen, so a lock on it could not be seen`,
  };

  // One pass per mark on the row. The bound is the visible week plus slack, so a
  // cleanup that stops making progress gives up instead of looping forever.
  for (let pass = 0; pass < 10; pass++) {
    const marks = await marksOnRow();
    if (marks === null) {
      return unseen;
    }
    if (marks === 0) {
      return { state: 'clean' };
    }
    try {
      await boundary.first().locator('.plan-container').click();
      await unlockOpenDay(page);
    } catch (error) {
      // The unlock threw, so re-read the row rather than assume: a failure after the
      // PUT landed can still leave it clean.
      const remaining = await marksOnRow();
      if (remaining === null) {
        return unseen;
      }
      return remaining === 0
        ? { state: 'clean' }
        : { state: 'locked', detail: `${worker}: ${describeError(error)}` };
    }
  }
  return { state: 'locked', detail: `${worker}: still marked after 10 unlocks` };
}

export interface DashboardSession {
  /**
   * Reads the worker rendered at a grid position ONCE, and registers that row so the
   * cleanup unlocks it. Every worker a test touches goes through this, including one
   * it expects to leave untouched: if a bug locks that row anyway, cleanup catches it
   * instead of the next spec.
   */
  pickWorker(page: Page, rowIndex: number): Promise<string>;
}

/**
 * The frame every spec in this shard shares: log in, put the grid on last week, and
 * afterwards put the database back the way it was found. Call it once in the describe
 * body — it installs the hooks itself — and address rows only through `pickWorker`.
 *
 * The cleanup runs after a failed test too, which is the whole point: the shard shares
 * one database and runs its files in order (workers: 1, retries: 0), so a row left
 * locked by an assertion that failed halfway breaks every spec after it.
 */
export function useLastWeekDashboard(): DashboardSession {
  let picked: string[] = [];

  test.beforeEach(async ({ page }) => {
    picked = [];
    await page.goto('http://localhost:4200');
    await new LoginPage(page).login();
    await openDashboardLastWeek(page);
  });

  test.afterEach(async ({ page }) => {
    // Nothing picked means nothing could have been locked: a row can only be
    // addressed by a name that came from pickWorker.
    if (picked.length === 0) {
      return;
    }
    // Best-effort on purpose, NOT fail-fast. The only job of this hook is that the
    // next spec finds an unlocked database, and a throw from the dialog close or the
    // reload would abandon every row after it — the bulk spec alone carries three.
    //
    // The SCAN below is the only authority on whether this hook failed. Closing a
    // dialog and reloading the grid are how the scan gets to look at the rows; neither
    // is evidence about the database. Every test in the shard now runs that reload, so
    // failing on a reload that flaked while the scan then found every row clean would
    // report a leaked lock to whoever triages CI that does not exist. A step that
    // errored is recorded as an annotation instead, where it is visible without
    // reading as a failure.
    const notes: string[] = [];
    const attempt = async (what: string, step: () => Promise<void>): Promise<void> => {
      try {
        await step();
      } catch (error) {
        notes.push(`${what} failed (${describeError(error)})`);
      }
    };

    await attempt('closing the open dialog', () => closeAnyDialog(page));
    // Only the reload decides this: a dialog that would not close does not make the
    // grid stale, so a clean scan after that alone is still trustworthy.
    const notesBeforeReload = notes.length;
    await attempt('reloading the grid', () => reloadGridFromServer(page));
    const gridIsFresh = notes.length === notesBeforeReload;

    // A clean scan is only as good as the grid it read. reloadGridFromServer exists
    // because a test that aborts between a lock change and its reload leaves the row
    // still drawn as open — and that abort is exactly when the reload itself is most
    // likely to fail. So when the refresh was not confirmed, `clean` means only "no
    // lock VISIBLE", which is not the same claim as "no lock", and it is downgraded to
    // `unknown`. `locked` is kept as it is: a mark you can see is real whether or not
    // the grid is fresh, and the scan still gets to clear it. The asymmetry is
    // deliberate — a false alarm costs one re-run, a swallowed leak poisons every spec
    // that follows.
    const stillLocked: string[] = [];
    const unverified: string[] = [];
    for (const worker of picked) {
      let verdict: RowVerdict;
      try {
        verdict = await unlockRow(page, worker);
      } catch (error) {
        // unlockRow is written not to throw; if it ever does, that is one row unknown,
        // not a reason to skip the rest.
        verdict = { state: 'unknown', detail: `${worker}: ${describeError(error)}` };
      }
      if (verdict.state === 'clean' && !gridIsFresh) {
        verdict = {
          state: 'unknown',
          detail: `${worker}: the grid could not be refreshed, so a lock on this row would not have shown`,
        };
      }
      if (verdict.state === 'locked') {
        stillLocked.push(verdict.detail);
      } else if (verdict.state === 'unknown') {
        unverified.push(verdict.detail);
      }
    }

    if (notes.length > 0) {
      test.info().annotations.push({ type: 'cleanup', description: notes.join('; ') });
    }

    // A row left locked breaks every spec after this one, so it stays loud and names
    // the workers.
    if (stillLocked.length > 0) {
      throw new Error(`cleanup left rows locked for the specs that follow:\n  ${stillLocked.join('\n  ')}`);
    }
    // A weaker, different claim: nothing was seen locked, but the scan could not be
    // trusted to see it — the row was not on screen, or the grid could not be
    // refreshed — so a lock could be sitting there unseen. Each message says which.
    if (unverified.length > 0) {
      throw new Error(`cleanup could not verify the lock state of these rows:\n  ${unverified.join('\n  ')}`);
    }
  });

  return {
    async pickWorker(page: Page, rowIndex: number): Promise<string> {
      const worker = await workerAtRow(page, rowIndex);
      picked.push(worker);
      return worker;
    },
  };
}

/** Hovers a target and waits for its tooltip. Material renders tooltips in the overlay. */
export async function expectTooltipOnHover(
  page: Page, target: Locator, text: string | RegExp,
): Promise<void> {
  await target.hover();
  await expect(page.locator('.cdk-overlay-container .mat-mdc-tooltip-surface')
    .filter({ hasText: text })).toBeVisible({ timeout: 10000 });
}

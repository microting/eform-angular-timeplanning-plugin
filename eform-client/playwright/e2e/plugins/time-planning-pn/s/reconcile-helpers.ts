import { expect, Locator, Page, Response } from '@playwright/test';

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
 * one database and runs its specs in file order (workers: 1).
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

export async function openDashboardLastWeek(page: Page): Promise<void> {
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

/** The worker rendered at a grid position. Call it once per spec, to pick a worker. */
export async function workerAtRow(page: Page, rowIndex: number): Promise<string> {
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
  if (await page.locator('mat-dialog-container').count() === 0) {
    return;
  }
  await page.keyboard.press('Escape');
  await expect(page.locator('mat-dialog-container'),
    'a dialog stayed open, so the row could not be cleaned up').toHaveCount(0, { timeout: 10000 });
}

/**
 * Takes every mark off a worker's row, the newest first, because only the boundary
 * can be unlocked. Call it from afterEach: the shard shares one database and runs its
 * files in order, so a row left locked by a failed assertion breaks every spec after
 * it. It is a no-op when nothing is locked, and it closes any open dialog first.
 */
export async function unlockAll(page: Page, worker: string): Promise<void> {
  if (!worker) {
    return;
  }
  await closeAnyDialog(page);
  const boundary = rowOf(page, worker).locator('td.reconciled-background');
  // One pass per mark on the row. The bound is the visible week plus slack, so a
  // cleanup that stops making progress fails loudly instead of looping forever.
  for (let pass = 0; pass < 10; pass++) {
    if (await boundary.count() === 0) {
      return;
    }
    await boundary.first().locator('.plan-container').click();
    await unlockOpenDay(page);
  }
  throw new Error(`${worker}'s row is still locked after 10 unlocks`);
}

/** Hovers a target and waits for its tooltip. Material renders tooltips in the overlay. */
export async function expectTooltipOnHover(
  page: Page, target: Locator, text: string | RegExp,
): Promise<void> {
  await target.hover();
  await expect(page.locator('.cdk-overlay-container .mat-mdc-tooltip-surface')
    .filter({ hasText: text })).toBeVisible({ timeout: 10000 });
}

import { APIRequestContext, APIResponse, expect, Locator, Page, Response, request } from '@playwright/test';
import { LoginPage } from '../../../Page objects/Login.page';

/**
 * Shared by every spec in shard t, the web planner "Planlægning (beta)" (#5489).
 *
 * The shard runs on the shard-a seed plus t/post-migration.sql, which makes the
 * signed-in admin the manager of every worker (absence needs a manager). The
 * specs share one database and run in file order (workers: 1), so each spec
 * works on its OWN workers, found by name, never by grid position:
 *
 *   planner-absence ............ "k l"
 *   planner-grid-edit .......... "c d", "e f"
 *   planner-import ............. "o p" (recognised), "q r" (not recognised)
 *   planner-menu-permission .... a user of its own
 *   planner-pattern-copy ....... "i j" (source), "m n" (target)
 *   planner-pattern-rotation ... "g h"
 *
 * Data a spec needs beyond the seed is written through the planner's own API in
 * a beforeAll, signed in as the admin.
 */

export const BASE_URL = 'http://localhost:4200';
export const ADMIN_EMAIL = 'admin@admin.com';
export const ADMIN_PASSWORD = 'secretpassword';
const PLANNER_API = '/api/time-planning-pn/planner';
const GRID_PATH = `${PLANNER_API}/grid`;

// ---------------------------------------------------------------- dates

const pad = (n: number) => String(n).padStart(2, '0');

function localKey(d: Date): string {
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}`;
}

/** The Monday of the current week plus `weeks` weeks, as yyyy-MM-dd (the grid's own calendar). */
export function mondayKey(weeks = 0): string {
  const d = new Date();
  d.setHours(0, 0, 0, 0);
  d.setDate(d.getDate() - ((d.getDay() + 6) % 7) + weeks * 7);
  return localKey(d);
}

/** yyyy-MM-dd plus `days` days. */
export function addDaysKey(key: string, days: number): string {
  const [y, m, d] = key.split('-').map(Number);
  return localKey(new Date(y, m - 1, d + days));
}

/** A calendar date as the planner sends it: UTC midnight of that date. */
export function transport(key: string): string {
  return `${key}T00:00:00.000Z`;
}

/** The hours as the grid shows them in Danish: 7.5 -> "7,5", 22 -> "22". */
export function danishHours(value: number): string {
  return String(value).replace('.', ',');
}

// ------------------------------------------------------------------ API

/** OperationResult failures come back as HTTP 200 with success:false, so check both. */
export async function expectSuccess(response: Response | APIResponse): Promise<any> {
  expect(response.status(), `${response.url()} HTTP status`).toBeLessThan(400);
  const body = await response.json();
  expect(body.success, `${response.url()} failed: ${body.message}`).toBe(true);
  return body;
}

/** The host and planner API, signed in as some user with a bearer token. */
export class Api {
  private constructor(private readonly ctx: APIRequestContext, private readonly token: string) {}

  static async signIn(email = ADMIN_EMAIL, password = ADMIN_PASSWORD): Promise<Api> {
    const ctx = await request.newContext({ baseURL: BASE_URL });
    const response = await ctx.post('/api/auth/token', {
      form: { username: email, password, grant_type: 'password' },
    });
    expect(response.ok(), 'sign-in HTTP status').toBe(true);
    const body = await response.json();
    expect(body.success, `sign-in as ${email} failed: ${body.message}`).toBe(true);
    return new Api(ctx, body.model.accessToken);
  }

  async call(method: 'GET' | 'POST' | 'PUT', path: string, data?: unknown): Promise<any> {
    const response = await this.ctx.fetch(path, {
      method,
      data,
      headers: { Authorization: `Bearer ${this.token}` },
    });
    return (await expectSuccess(response)).model;
  }

  grid(fromKey: string, days: 7 | 14): Promise<any> {
    return this.call('GET', `${GRID_PATH}?from=${encodeURIComponent(transport(fromKey))}&days=${days}`);
  }

  /** The planner's AssignedSite id of each named worker. */
  async assignedSiteIds(...names: string[]): Promise<number[]> {
    const grid = await this.grid(mondayKey(), 7);
    return names.map(name => {
      const worker = grid.workers.find((w: any) => w.siteName === name);
      expect(worker, `worker "${name}" must be in the planner`).toBeTruthy();
      return worker.assignedSiteId as number;
    });
  }

  /** Writes one short form ("7-15/0,5", "Fri") to these days of one worker. */
  writeDays(assignedSiteId: number, keys: string[], shortForm: string): Promise<any> {
    return this.call('POST', `${PLANNER_API}/cells`, {
      cells: keys.map(key => ({ assignedSiteId, date: transport(key) })),
      shortForm,
      messageId: null,
      mode: 'onlyTheseDays',
      fromDate: null,
    });
  }

  /** Saves a pattern; `weeks[w][d]` is the short form of rotation week w, weekday d (0 = Monday). */
  savePattern(assignedSiteId: number, anchorKey: string, validFromKey: string, weeks: string[][]): Promise<any> {
    const days = weeks.flatMap((week, weekIndex) =>
      week.map((planText, dayOfWeek) => ({ weekIndex, dayOfWeek, planText })));
    return this.call('PUT', `${PLANNER_API}/patterns/${assignedSiteId}`, {
      rotationWeeks: weeks.length,
      anchorDate: transport(anchorKey),
      validFrom: transport(validFromKey),
      days,
      overwriteManualDays: true,
    });
  }

  dispose(): Promise<void> {
    return this.ctx.dispose();
  }
}

// ------------------------------------------------------------------- UI

export async function waitForSpinner(page: Page): Promise<void> {
  if (await page.locator('.overlay-spinner').count() > 0) {
    await page.locator('.overlay-spinner').waitFor({ state: 'hidden', timeout: 30000 });
  }
}

/** The planner's grid GET. With `fromKey` it only accepts a load of that period. */
export function waitForGrid(page: Page, fromKey?: string): Promise<Response> {
  return page.waitForResponse(r => {
    const url = new URL(r.url());
    return url.pathname === GRID_PATH
      && r.request().method() === 'GET'
      && (fromKey === undefined || (url.searchParams.get('from') ?? '').startsWith(fromKey));
  });
}

/** Any planner write: method and path below /api/time-planning-pn/planner. */
export function waitForPlanner(page: Page, method: string, path: RegExp): Promise<Response> {
  return page.waitForResponse(r =>
    r.request().method() === method && path.test(new URL(r.url()).pathname));
}

/** Opens the planner through the menu, as a user would, and waits for the grid. */
export async function openPlannerFromMenu(page: Page): Promise<void> {
  await page.locator('mat-nested-tree-node').filter({ hasText: 'Timeregistrering' }).click();
  const entry = page.locator('#time-planning-pn-planner');
  await expect(entry).toBeVisible();
  const grid = waitForGrid(page);
  await entry.click();
  await expectSuccess(await grid);
  await expect(page.locator('#planner-grid tbody tr').first()).toBeVisible();
  await waitForSpinner(page);
}

/** Signs in as the admin and opens the planner (current and next week). */
export async function openPlannerAsAdmin(page: Page): Promise<void> {
  await page.goto(BASE_URL);
  await new LoginPage(page).login();
  await openPlannerFromMenu(page);
}

/** Next period (14 days on), waiting for that period's grid. */
export async function nextPeriod(page: Page, fromKey: string): Promise<void> {
  const grid = waitForGrid(page, fromKey);
  await page.locator('#planner-next').click();
  await expectSuccess(await grid);
  await waitForSpinner(page);
}

export function rowOf(page: Page, assignedSiteId: number): Locator {
  return page.locator(`[data-testid="planner-row-${assignedSiteId}"]`);
}

export function cellOf(page: Page, assignedSiteId: number, key: string): Locator {
  return page.locator(`[data-testid="planner-cell-${assignedSiteId}-${key}"]`);
}

/** The absence label of a day (absent when the day has none). */
export function absenceOf(page: Page, assignedSiteId: number, key: string): Locator {
  return page.locator(`[data-testid="planner-absence-${assignedSiteId}-${key}"]`);
}

/** The "changed for this day" dot of a day (absent when the day has none). */
export function manualDotOf(page: Page, assignedSiteId: number, key: string): Locator {
  return page.locator(`[data-testid="planner-manual-${assignedSiteId}-${key}"]`);
}

export function weekSumOf(page: Page, assignedSiteId: number, mondayKeyOfWeek: string): Locator {
  return page.locator(`[data-testid="planner-week-${assignedSiteId}-${mondayKeyOfWeek}"] strong`);
}

/** Plain click: toggles one cell. */
export async function clickCell(page: Page, assignedSiteId: number, key: string): Promise<void> {
  const cell = cellOf(page, assignedSiteId, key);
  await cell.scrollIntoViewIfNeeded();
  await cell.click();
}

/** Shift-click: extends the selection from the last clicked cell to this one. */
export async function shiftClickCell(page: Page, assignedSiteId: number, key: string): Promise<void> {
  const cell = cellOf(page, assignedSiteId, key);
  await cell.scrollIntoViewIfNeeded();
  await cell.click({ modifiers: ['Shift'] });
}

/** Picks a mat-select option by its text (pass an anchored RegExp for an exact match). */
export async function selectOption(page: Page, select: Locator, text: string | RegExp): Promise<void> {
  await select.click();
  await page.locator('mat-option').filter({ hasText: text }).first().click();
  await expect(page.locator('.cdk-overlay-pane mat-option')).toHaveCount(0);
}

import {DatePipe} from '@angular/common';
import {TranslateService} from '@ngx-translate/core';
import {format} from 'date-fns';
import {Observable, Subscription, defaultIfEmpty, throwError, timeout} from 'rxjs';
import {OperationResult} from 'src/app/common/models';
import {PlanningPrDayModel, TimePlanningModel} from '../../models';

/**
 * Calendar-day key 'yyyy-MM-dd', for comparing days with no timezone involved.
 *
 * Server dates (PlanningPrDayModel.date, TimePlanningModel.lockedThrough) arrive as
 * server-local midnight with no offset, e.g. '2026-09-07T00:00:00', so the first ten
 * characters ARE the calendar day. Turning them into a Date and comparing instants is
 * how a boundary ends up a day off for part of every evening. A Date is formatted in
 * local time.
 */
export function dayKey(value: string | Date | null | undefined): string | null {
  if (!value) {
    return null;
  }
  if (value instanceof Date) {
    return isNaN(value.getTime()) ? null : format(value, 'yyyy-MM-dd');
  }
  return /^\d{4}-\d{2}-\d{2}/.test(value) ? value.slice(0, 10) : null;
}

// ---- The three day states (spec §8.1, ruling F20) ---------------------------------
//
// A row's lock is derived from one date, row.lockedThrough (the boundary). Every day
// at or before it is LOCKED. Among the locked days:
// - the BOUNDARY is the single day whose date is lockedThrough;
// - a SEALED day carries its own reconcile mark (reconciled === true). The boundary
//   is always sealed, and older days stay sealed after a later reconcile moves the
//   boundary on, so several days of a row can be sealed at once. The flag alone
//   therefore never makes a day the boundary.
// Every comparison is by calendar day (dayKey), never by instant.

/** The part of a grid row the day-state predicates read. */
export type DayLockRow = Pick<TimePlanningModel, 'lockedThrough' | 'planningPrDayModels'>;

/** All four states of one day, resolved in a single pass. */
export interface DayLockState {
  /** At or before the row's boundary, the boundary included. A locked day is read-only. */
  locked: boolean;
  /** The one day whose date is row.lockedThrough. Only it offers unlock. */
  boundary: boolean;
  /** A locked day with its own reconcile mark: the boundary, or an older reconciled day. */
  sealed: boolean;
  /** A day with no registration behind it (F17). It is locked, and it renders blank. */
  placeholder: boolean;
}

/**
 * The day under a grid column. The column field is the day's index in the row
 * ('0', '1', …): the grid reads days by position.
 */
export function dayAt(
  row: DayLockRow | null | undefined, field: string | number,
): PlanningPrDayModel | undefined {
  return row?.planningPrDayModels?.[Number(field)];
}

/**
 * F17: the server sends a non-persisted stand-in, with id 0, for a locked date that
 * has no registration, so that every later day stays under its own column header.
 * An id is therefore the one mark of a day that exists in the database: no id, no
 * row to open, to save or to reconcile, and nothing to render.
 */
export function hasRegistration(day: PlanningPrDayModel | null | undefined): boolean {
  return !!day?.id;
}

/**
 * The day's lock state, with the day looked up and the dates parsed once. The
 * template, the cell class and the dialog all read it through this, so they can
 * never disagree about a cell.
 */
export function dayLockState(
  row: DayLockRow | null | undefined, field: string | number,
): DayLockState {
  const day = dayAt(row, field);
  const placeholder = day !== undefined && day !== null && !hasRegistration(day);
  const boundaryKey = dayKey(row?.lockedThrough);
  if (boundaryKey === null) {
    // Nothing on this row is locked. That is every row of every tenant who has never
    // reconciled, so it is answered before a single day date is parsed.
    return {locked: false, boundary: false, sealed: false, placeholder};
  }
  const key = dayKey(day?.date);
  const locked = key !== null && key <= boundaryKey;
  return {
    locked,
    boundary: key === boundaryKey,
    // The lock decides, not the flag: an unlock clears the flag, so a mark that
    // outlived its lock would be stale.
    sealed: locked && day?.reconciled === true,
    placeholder,
  };
}

/** At or before the row's boundary, the boundary itself included. A locked day is read-only. */
export function isDayLocked(row: DayLockRow | null | undefined, field: string | number): boolean {
  return dayLockState(row, field).locked;
}

/** The boundary: the one day whose date is row.lockedThrough. Only it offers unlock. */
export function isBoundaryDay(row: DayLockRow | null | undefined, field: string | number): boolean {
  return dayLockState(row, field).boundary;
}

/** A locked day with its own reconcile mark: the boundary, or an older reconciled day. */
export function isDaySealed(row: DayLockRow | null | undefined, field: string | number): boolean {
  return dayLockState(row, field).sealed;
}

/** A stand-in for a locked date with no registration (F17): not openable, rendered blank. */
export function isPlaceholderDay(row: DayLockRow | null | undefined, field: string | number): boolean {
  return dayLockState(row, field).placeholder;
}

/**
 * "Afstemt 14.09.2026 kl. 10:32" (spec §8.1, §8.2). There is no "by whom":
 * ReconciledBy is a declared non-goal.
 *
 * ReconciledAt is written as DateTime.Now (Task 4) and read back from a datetime(6)
 * column, so EF materialises it as DateTimeKind.Unspecified. Newtonsoft
 * (RoundtripKind) serialises it with NO offset, and the browser reads it as local
 * wall-clock time, which is the server's clock on a Danish deployment. Do NOT pass
 * 'UTC' here, the way formatStamp does for the shift stamps.
 */
export function formatReconciledProvenance(
  reconciledAt: string | null | undefined,
  datePipe: DatePipe,
  translate: TranslateService,
): string {
  if (!reconciledAt) {
    // I1 says a reconciled day always has a timestamp; this covers a malformed row.
    return translate.instant('Reconciled');
  }
  return translate.instant('reconciledProvenance', {
    date: datePipe.transform(reconciledAt, 'dd.MM.yyyy'),
    time: datePipe.transform(reconciledAt, 'HH:mm'),
  });
}

// ---- One lock request, four outcomes -----------------------------------------------

/**
 * How long a reconcile, an unlock or a bulk reconcile may hang before the caller
 * stops waiting for it.
 *
 * It has to be shorter than HttpErrorInterceptor's own budget: its `default:` branch
 * retries a failing request five times, fifteen seconds apart, which is about 75
 * seconds before it gives up. Waiting that out behind a blocked backdrop, or behind a
 * disabled commit button, is not a state anyone should have to sit through.
 */
export const LOCK_REQUEST_TIMEOUT_MS = 30000;

/**
 * Thrown by the timeout above, so the failure handler can tell the one UNKNOWN error
 * apart from the errors that reached us from the server. Recognised by reference, not
 * by type, so a test needs no rxjs-internal error object to prove the branch.
 */
const LOCK_REQUEST_TIMED_OUT = {lockRequestTimedOut: true};

/**
 * What came back. Four outcomes, because the two that are NOT failures must not be
 * reported as one:
 * - `success`   — the server did it.
 * - `refused`   — KNOWN. The server itself answered "no", so nothing changed.
 *                 ApiBaseService has already toasted `message` when there is one.
 * - `error`     — KNOWN. The only error HttpErrorInterceptor lets through to a caller
 *                 is its 400 branch, where the request was rejected before anything
 *                 happened — and it has already toasted it.
 * - `unknown`   — our timeout, or the interceptor giving up after retrying against a
 *                 5xx or a dead connection. Neither says whether the write landed, so
 *                 the caller must refresh rather than claim nothing happened.
 */
export type LockRequestOutcome<T extends OperationResult> =
  | {kind: 'success'; result: T}
  | {kind: 'refused'; message: string | null}
  | {kind: 'error'}
  | {kind: 'unknown'};

/**
 * Closes a `switch` over the outcomes: put `return assertLockOutcomeHandled(outcome);`
 * after the last `case`, with every case returning.
 *
 * The argument narrows to `never` only while the union is exhausted, so adding a fifth
 * outcome fails the build at each call site instead of quietly falling through — which,
 * for a switch whose last branch means "we do not know what happened", would be the worst
 * possible silence.
 */
export function assertLockOutcomeHandled(outcome: never): never {
  return outcome;
}

/**
 * The one place a lock request is made, shared by the day dialog and the toolbar's
 * bulk reconcile so they cannot drift on what an unanswered request means.
 *
 * Callers do the rest themselves — their in-flight guards, their messages, their
 * refresh — because the dialog blocks its own dismissal while a request is out and the
 * toolbar does not. What they must NOT each re-derive is the outcome.
 */
export function sendLockRequest<T extends OperationResult>(
  request$: Observable<T>,
  handle: (outcome: LockRequestOutcome<T>) => void,
): Subscription {
  return request$.pipe(
    timeout({
      each: LOCK_REQUEST_TIMEOUT_MS,
      with: () => throwError(() => LOCK_REQUEST_TIMED_OUT),
    }),
    // HttpErrorInterceptor turns a sustained 5xx, and an offline network, into EMPTY
    // after its retries: a completion with no value and no error. This turns that into
    // an ordinary answer of "nothing", so it needs no state of its own to recognise.
    defaultIfEmpty(null),
  ).subscribe({
    next: (result: T | null) => {
      if (result && result.success) {
        handle({kind: 'success', result});
        return;
      }
      if (!result) {
        // The EMPTY above. The interceptor reaches it only after retrying against a
        // 5xx or a dead connection, and a 5xx can be written AFTER the save has
        // committed — the server saves before it writes the response. So a response
        // arriving is not evidence that nothing changed.
        handle({kind: 'unknown'});
        return;
      }
      handle({kind: 'refused', message: result.message ?? null});
    },
    error: error => handle(
      // Our own timeout aborts the request from this end, but a slow server may commit
      // it a moment later.
      error === LOCK_REQUEST_TIMED_OUT ? {kind: 'unknown'} : {kind: 'error'}),
  });
}

/** 'lock' = the boundary moves to a day on screen. 'skip' = already at or past the target. */
export type ReconcileRowOutcome = 'lock' | 'skip';

export interface ReconcilePreview {
  /** 'yyyy-MM-dd'. Sent to reconcile-through as it is. */
  target: string;
  /**
   * Workers sent to reconcile-through: every row in scope that the preview drew,
   * as 'lock' or as 'skip', each once. Skipped rows are sent too, so that the server
   * reports them. A row the preview could not draw is never sent (§8.3: the region
   * is previewed before it is committed).
   */
  siteIds: number[];
  /** Per worker: the day the mark lands on. */
  landingBySiteId: Record<number, string>;
  /** Per worker: the boundary they already have, so already-locked cells are not re-highlighted. */
  existingBySiteId: Record<number, string | null>;
  outcomeBySiteId: Record<number, ReconcileRowOutcome>;
  /** Workers whose boundary will move. */
  willReconcileCount: number;
  skipCount: number;
}

/**
 * The region a reconcile-through WILL lock, per worker (spec §8.3). It mirrors the
 * server rules in Task 4:
 * - A worker already reconciled at or past the target is skipped, never moved back.
 *   Moving back would be an unlock, which is a separate, heavier action (§8.4).
 * - Otherwise the mark lands on the latest day at or before the target that has a
 *   registration.
 *
 * Only days on screen are known here. A row with no registered day on screen
 * between its boundary and the target has nothing to draw, so it is left out of the
 * commit. The date field is limited to days on screen (12A) and the index creates a
 * registration for every visible day (fact 11), so this only happens when a visible
 * day failed to materialise.
 */
export function buildReconcilePreview(
  rows: TimePlanningModel[],
  scopeSiteIds: number[],
  target: string,
): ReconcilePreview {
  const inScope = new Set(scopeSiteIds);
  const preview: ReconcilePreview = {
    target,
    siteIds: [],
    landingBySiteId: {},
    existingBySiteId: {},
    outcomeBySiteId: {},
    willReconcileCount: 0,
    skipCount: 0,
  };

  for (const row of rows) {
    if (!inScope.has(row.siteId) || row.siteId in preview.outcomeBySiteId) {
      continue;
    }
    const existing = dayKey(row.lockedThrough);
    preview.existingBySiteId[row.siteId] = existing;

    if (existing !== null && existing >= target) {
      preview.outcomeBySiteId[row.siteId] = 'skip';
      preview.siteIds.push(row.siteId);
      preview.skipCount++;
      continue;
    }

    const landing = (row.planningPrDayModels ?? [])
      .filter(hasRegistration)
      .map(day => dayKey(day.date))
      .filter((key): key is string => key !== null && key <= target)
      .sort()
      .pop();
    if (!landing || (existing !== null && landing <= existing)) {
      continue;
    }

    preview.landingBySiteId[row.siteId] = landing;
    preview.outcomeBySiteId[row.siteId] = 'lock';
    preview.siteIds.push(row.siteId);
    preview.willReconcileCount++;
  }
  return preview;
}

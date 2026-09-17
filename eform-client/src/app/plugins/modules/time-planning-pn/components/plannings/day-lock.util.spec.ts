import {DatePipe} from '@angular/common';
import {EMPTY, of, Subject, throwError} from 'rxjs';
import {
  buildReconcilePreview,
  dayKey,
  dayLockState,
  formatReconciledProvenance,
  hasRegistration,
  isBoundaryDay,
  isDayLocked,
  isDaySealed,
  isPlaceholderDay,
  LOCK_REQUEST_TIMEOUT_MS,
  LockRequestOutcome,
  sendLockRequest,
} from './day-lock.util';

describe('day-lock util', () => {
  describe('dayKey', () => {
    it('takes the calendar day from a server date without parsing it', () => {
      expect(dayKey('2026-09-07T00:00:00')).toBe('2026-09-07');
    });

    it('formats a Date in local time', () => {
      expect(dayKey(new Date(2026, 8, 7, 23, 30))).toBe('2026-09-07');
    });

    it('puts the last second before local midnight and midnight itself on different days', () => {
      expect(dayKey(new Date(2026, 8, 7, 23, 59, 59))).toBe('2026-09-07');
      expect(dayKey(new Date(2026, 8, 8, 0, 0, 0))).toBe('2026-09-08');
    });

    it('keeps the calendar day a server date names, whatever the offset', () => {
      // Parsed as an instant and formatted locally, this is the 8th in UTC, which is
      // where CI runs. The key is the day the server means.
      expect(dayKey('2026-09-09T00:00:00+02:00')).toBe('2026-09-09');
    });

    it('returns null for missing or malformed input', () => {
      expect(dayKey(null)).toBeNull();
      expect(dayKey(undefined)).toBeNull();
      expect(dayKey('not a date')).toBeNull();
      expect(dayKey(new Date(NaN))).toBeNull();
    });
  });

  describe('day states', () => {
    const day = (date: string, extra: object = {}) =>
      ({id: 1, date: `${date}T00:00:00`, reconciled: false, reconciledAt: null, ...extra}) as any;
    const row = (lockedThrough: string | null, days: any[]) => ({
      lockedThrough: lockedThrough ? `${lockedThrough}T00:00:00` : null,
      planningPrDayModels: days,
    }) as any;

    // Two reconciles on one row: the 7th first, then the 9th. Both keep their mark;
    // the 9th is the boundary.
    const staircase = row('2026-09-09', [
      day('2026-09-07', {reconciled: true, reconciledAt: '2026-09-08T10:32:00Z'}),
      day('2026-09-08'),
      day('2026-09-09', {reconciled: true, reconciledAt: '2026-09-10T09:15:00Z'}),
      day('2026-09-10'),
    ]);

    it('makes only the day at lockedThrough the boundary, and it is locked and sealed', () => {
      expect(isBoundaryDay(staircase, '2')).toBe(true);
      expect(isDayLocked(staircase, '2')).toBe(true);
      expect(isDaySealed(staircase, '2')).toBe(true);
    });

    it('keeps an older reconciled day sealed and locked, but never the boundary', () => {
      expect(isBoundaryDay(staircase, '0')).toBe(false);
      expect(isDayLocked(staircase, '0')).toBe(true);
      expect(isDaySealed(staircase, '0')).toBe(true);
    });

    it('locks an unreconciled day below the boundary without sealing it', () => {
      expect(isBoundaryDay(staircase, '1')).toBe(false);
      expect(isDayLocked(staircase, '1')).toBe(true);
      expect(isDaySealed(staircase, '1')).toBe(false);
    });

    it('leaves a day after the boundary open', () => {
      expect(isBoundaryDay(staircase, '3')).toBe(false);
      expect(isDayLocked(staircase, '3')).toBe(false);
      expect(isDaySealed(staircase, '3')).toBe(false);
    });

    it('reads the column field as a string or a number', () => {
      expect(isBoundaryDay(staircase, 2)).toBe(true);
      expect(isDayLocked(staircase, 1)).toBe(true);
      expect(isDaySealed(staircase, 0)).toBe(true);
    });

    it('locks nothing on a row with no boundary', () => {
      const open = row(null, [day('2026-09-07', {reconciled: true})]);
      expect(isDayLocked(open, '0')).toBe(false);
      expect(isBoundaryDay(open, '0')).toBe(false);
      // A stray flag outside the lock seals nothing: the lock decides, not the flag.
      expect(isDaySealed(open, '0')).toBe(false);
    });

    it('answers false for a column with no day, or no row at all', () => {
      expect(isDayLocked(staircase, '9')).toBe(false);
      expect(isBoundaryDay(staircase, '9')).toBe(false);
      expect(isDaySealed(staircase, '9')).toBe(false);
      expect(isPlaceholderDay(staircase, '9')).toBe(false);
      expect(isDayLocked(null, '0')).toBe(false);
      expect(isBoundaryDay(undefined, '0')).toBe(false);
    });

    describe('near midnight', () => {
      it('matches a late-evening time to the boundary on its own calendar day', () => {
        // Compared as instants, 23:30 is after 00:00 on the boundary day, so this
        // day would drop out of the lock and lose the boundary.
        const late = row('2026-09-09', [day('2026-09-09', {date: '2026-09-09T23:30:00', reconciled: true})]);
        expect(isDayLocked(late, '0')).toBe(true);
        expect(isBoundaryDay(late, '0')).toBe(true);
      });

      it('keeps the next day open when the boundary carries a late time', () => {
        const lateBoundary = {lockedThrough: '2026-09-09T23:59:59', planningPrDayModels: [day('2026-09-10')]} as any;
        expect(isDayLocked(lateBoundary, '0')).toBe(false);
        expect(isBoundaryDay(lateBoundary, '0')).toBe(false);
      });

      it('never shifts a boundary written with an offset', () => {
        // Only the boundary carries the offset, so an instant comparison moves it to
        // the 8th in UTC (which is where CI runs) and the day stops being locked.
        const withOffset = {lockedThrough: '2026-09-09T00:00:00+02:00',
          planningPrDayModels: [day('2026-09-09'), day('2026-09-10')]} as any;
        expect(isBoundaryDay(withOffset, '0')).toBe(true);
        expect(isDayLocked(withOffset, '0')).toBe(true);
        expect(isDayLocked(withOffset, '1')).toBe(false);
      });
    });

    describe('placeholder days', () => {
      const withPlaceholder = row('2026-09-09', [
        day('2026-09-07'),
        day('2026-09-08', {id: 0}),
        day('2026-09-09', {reconciled: true, reconciledAt: '2026-09-10T09:15:00Z'}),
      ]);

      it('treats a day with no registration id as a placeholder, and no other day', () => {
        expect(isPlaceholderDay(withPlaceholder, '1')).toBe(true);
        expect(isPlaceholderDay(withPlaceholder, '0')).toBe(false);
        expect(isPlaceholderDay(withPlaceholder, '2')).toBe(false);
      });

      it('keeps a placeholder locked, so it carries the texture and the lock glyph', () => {
        expect(isDayLocked(withPlaceholder, '1')).toBe(true);
        expect(isDaySealed(withPlaceholder, '1')).toBe(false);
        expect(isBoundaryDay(withPlaceholder, '1')).toBe(false);
      });

      it('reads the registration mark the same way the preview does', () => {
        expect(hasRegistration(day('2026-09-07'))).toBe(true);
        expect(hasRegistration(day('2026-09-07', {id: 0}))).toBe(false);
        expect(hasRegistration(null)).toBe(false);
        expect(hasRegistration(undefined)).toBe(false);
      });
    });

    describe('dayLockState', () => {
      it('answers all four states in one read', () => {
        expect(dayLockState(staircase, '2')).toEqual(
          {locked: true, boundary: true, sealed: true, placeholder: false});
        expect(dayLockState(staircase, '0')).toEqual(
          {locked: true, boundary: false, sealed: true, placeholder: false});
        expect(dayLockState(staircase, '1')).toEqual(
          {locked: true, boundary: false, sealed: false, placeholder: false});
        expect(dayLockState(staircase, '3')).toEqual(
          {locked: false, boundary: false, sealed: false, placeholder: false});
      });

      it('reports a placeholder on a row that has no boundary at all', () => {
        // The early return for an unlocked row must not lose the placeholder, which
        // is what keeps a cell with nothing behind it from rendering zeros.
        const unlocked = row(null, [day('2026-09-07', {id: 0})]);
        expect(dayLockState(unlocked, '0'))
          .toEqual({locked: false, boundary: false, sealed: false, placeholder: true});
      });

      it('answers open for a column with no day', () => {
        expect(dayLockState(staircase, '9'))
          .toEqual({locked: false, boundary: false, sealed: false, placeholder: false});
      });
    });
  });

  describe('formatReconciledProvenance', () => {
    const translate = {instant: jest.fn((key: string, params?: object) => key)} as any;

    /** What the server sends after the projection tags it: a UTC instant, "Z" and all. */
    const SERVER_STAMP = '2026-09-14T10:32:11Z';

    it('formats the stamp in the viewer\'s own zone, with no timezone argument', () => {
      // ReconciledAt arrives as a UTC-tagged instant (the server writes UtcNow and the
      // read projection tags it Kind.Utc, so the JSON ends in "Z"). No zone argument is
      // passed, so DatePipe renders it in the VIEWER's zone -- which is the point.
      // Passing 'UTC' would pin every viewer to the server's clock instead.
      const datePipe = new DatePipe('en-US');
      const transform = jest.spyOn(datePipe, 'transform');

      formatReconciledProvenance(SERVER_STAMP, datePipe, translate);

      // Two arguments, never three: toHaveBeenCalledWith fails on an extra argument,
      // so THIS is what pins "no timezone reaches the pipe" -- and it means the same
      // thing on a UTC CI runner as on a machine at any other offset.
      expect(transform).toHaveBeenCalledWith(SERVER_STAMP, 'dd.MM.yyyy');
      expect(transform).toHaveBeenCalledWith(SERVER_STAMP, 'HH:mm');
      // Shape, not digits: the digits are the viewer's own clock, so they follow
      // whatever zone the test runs in.
      expect(translate.instant).toHaveBeenLastCalledWith('reconciledProvenance', {
        date: expect.stringMatching(/^\d{2}\.\d{2}\.\d{4}$/),
        time: expect.stringMatching(/^\d{2}:\d{2}$/),
      });
    });

    it('falls back to the bare state name when there is no timestamp', () => {
      formatReconciledProvenance(null, new DatePipe('en-US'), translate);
      expect(translate.instant).toHaveBeenLastCalledWith('Reconciled');
    });
  });

  describe('buildReconcilePreview', () => {
    const day = (date: string, id = 1) => ({id, date: `${date}T00:00:00`}) as any;
    const row = (siteId: number, lockedThrough: string | null, days: any[]) => ({
      siteId,
      lockedThrough: lockedThrough ? `${lockedThrough}T00:00:00` : null,
      planningPrDayModels: days,
    }) as any;
    const week = ['2026-09-07', '2026-09-08', '2026-09-09', '2026-09-10'].map(d => day(d));

    it('lands on the latest registered day at or before the target', () => {
      const preview = buildReconcilePreview([row(1, null, week)], [1], '2026-09-09');
      expect(preview.landingBySiteId[1]).toBe('2026-09-09');
      expect(preview.outcomeBySiteId[1]).toBe('lock');
      expect(preview.siteIds).toEqual([1]);
      expect(preview.willReconcileCount).toBe(1);
    });

    it('skips a worker at or past the target, never moves the line back, and still sends it', () => {
      const preview = buildReconcilePreview(
        [row(1, '2026-09-10', week), row(2, '2026-09-09', week)], [1, 2], '2026-09-09');
      expect(preview.outcomeBySiteId[1]).toBe('skip');
      expect(preview.outcomeBySiteId[2]).toBe('skip');
      expect(preview.siteIds).toEqual([1, 2]);
      expect(preview.skipCount).toBe(2);
      expect(preview.willReconcileCount).toBe(0);
    });

    it('ignores days without a registration id', () => {
      const days = [day('2026-09-07'), day('2026-09-08', 0), day('2026-09-09', 0)];
      const preview = buildReconcilePreview([row(1, null, days)], [1], '2026-09-09');
      expect(preview.landingBySiteId[1]).toBe('2026-09-07');
    });

    it('never sends a row it could not draw', () => {
      const unregistered = ['2026-09-07', '2026-09-08'].map(d => day(d, 0));
      const preview = buildReconcilePreview([row(1, null, unregistered)], [1], '2026-09-08');
      expect(preview.siteIds).toEqual([]);
      expect(preview.outcomeBySiteId[1]).toBeUndefined();
      expect(preview.willReconcileCount).toBe(0);
    });

    it('keeps the existing boundary so already-locked days are not highlighted again', () => {
      const preview = buildReconcilePreview([row(1, '2026-09-07', week)], [1], '2026-09-09');
      expect(preview.existingBySiteId[1]).toBe('2026-09-07');
      expect(preview.landingBySiteId[1]).toBe('2026-09-09');
    });

    it('previews only the workers in scope, and sends each once', () => {
      const preview = buildReconcilePreview([row(1, null, week), row(2, null, week)], [2, 2], '2026-09-08');
      expect(preview.siteIds).toEqual([2]);
      expect(preview.outcomeBySiteId[1]).toBeUndefined();
    });
  });
  /**
   * The four outcomes both the day dialog and the toolbar's bulk reconcile decide from.
   * Two of them are not failures, and reporting them as one is what makes a user redo a
   * write that may already have landed.
   */
  describe('sendLockRequest', () => {
    const outcomes = (source: any): LockRequestOutcome<any>[] => {
      const seen: LockRequestOutcome<any>[] = [];
      sendLockRequest<any>(source, outcome => seen.push(outcome));
      return seen;
    };

    it('passes a successful answer straight through', () => {
      const answer = {success: true, model: {applied: 2}};
      expect(outcomes(of(answer))).toEqual([{kind: 'success', result: answer}]);
    });

    it('reports a refusal as known, carrying whatever the server said', () => {
      expect(outcomes(of({success: false, message: 'DayIsReconciled'})))
        .toEqual([{kind: 'refused', message: 'DayIsReconciled'}]);
    });

    it('reports a refusal with no message as known all the same', () => {
      // The caller adds its own fallback line; what matters here is that nothing
      // changed on the server, so this must not be confused with an unanswered write.
      expect(outcomes(of({success: false}))).toEqual([{kind: 'refused', message: null}]);
    });

    it('reports an error that reached us as known, because someone already spoke', () => {
      // HttpErrorInterceptor toasts every 400 and rethrows an empty string.
      expect(outcomes(throwError(() => ''))).toEqual([{kind: 'error'}]);
    });

    it('reports a completion with no answer as unknown', () => {
      // The interceptor turns a sustained 5xx, and an offline network, into EMPTY.
      expect(outcomes(EMPTY)).toEqual([{kind: 'unknown'}]);
    });

    it('stops waiting for a request that never answers, and calls that unknown', () => {
      jest.useFakeTimers();
      const seen: LockRequestOutcome<any>[] = [];
      const request = new Subject<any>();
      sendLockRequest<any>(request, outcome => seen.push(outcome));

      expect(seen).toEqual([]);
      jest.advanceTimersByTime(LOCK_REQUEST_TIMEOUT_MS + 1);

      expect(seen).toEqual([{kind: 'unknown'}]);
      jest.useRealTimers();
    });

    it('lets no late answer through, because the abort is from this end only', () => {
      jest.useFakeTimers();
      const seen: LockRequestOutcome<any>[] = [];
      const request = new Subject<any>();
      sendLockRequest<any>(request, outcome => seen.push(outcome));
      jest.advanceTimersByTime(LOCK_REQUEST_TIMEOUT_MS + 1);

      request.next({success: true});
      request.complete();

      expect(seen).toEqual([{kind: 'unknown'}]);
      jest.useRealTimers();
    });
  });
});

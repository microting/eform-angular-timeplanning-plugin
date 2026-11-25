import {afterEach, describe, it, expect, vi} from 'vitest';
import {TestBed} from '@angular/core/testing';
import {Overlay} from '@angular/cdk/overlay';
import {MatDialog} from '@angular/material/dialog';
import {TranslateService} from '@ngx-translate/core';
import {ToastrService} from 'ngx-toastr';
import {EMPTY, Observable, of, Subject, throwError} from 'rxjs';
import {TimePlanningPnAbsenceRequestsService} from '../../../../services';
import {PlannerCellModel, PlannerGridModel, PlannerWorkerModel} from '../../models';
import {TimePlanningPnPlannerService} from '../../services';
import {da} from '../../../../i18n/da';
import {ALL_LANGUAGES} from '../../../../testing/all-languages';
import {PlannerRequestsPanelComponent, planFromPreview, staffingDays} from './planner-requests-panel.component';

/** Translations as "key{params}", so a test sees which text and which values were asked for. */
const instant = (key: string, params?: object) => params ? `${key}${JSON.stringify(params)}` : key;

function createPanel(getInbox: () => unknown, previewAbsence: (...args: unknown[]) => unknown = () => EMPTY): PlannerRequestsPanelComponent {
  TestBed.configureTestingModule({
    providers: [
      {provide: TimePlanningPnAbsenceRequestsService, useValue: {getInbox}},
      {provide: TimePlanningPnPlannerService, useValue: {previewAbsence}},
      {provide: MatDialog, useValue: {}},
      {provide: Overlay, useValue: {}},
      {provide: ToastrService, useValue: {error: vi.fn()}},
      {provide: TranslateService, useValue: {instant, currentLang: 'da', defaultLang: 'da'}},
    ],
  });
  return TestBed.runInInjectionContext(() => new PlannerRequestsPanelComponent());
}

function cell(date: string, planHours: number, messageId: number | null = null): PlannerCellModel {
  return {
    date, planRegistrationId: null, planText: '', planHours, messageId, absenceRequestId: null,
    planChangedByAdmin: false, locked: false, isPaidDay: false, hasPendingAbsenceRequest: false,
  };
}

function worker(assignedSiteId: number, siteId: number, siteName: string, cells: PlannerCellModel[]): PlannerWorkerModel {
  return {
    assignedSiteId, siteId, siteName, avatarUrl: null, tags: [], hasPattern: true, rotationWeeks: 1,
    useGoogleSheetAsDefault: false, canApproveAbsence: true, weeklyNormHours: 37, weeks: [], cells,
  };
}

const MON = '2026-10-05T00:00:00';
const TUE = '2026-10-06T00:00:00';

/**
 * Frederik (requester) works Monday and Tuesday. Monday: Bo and Carl work too.
 * Tuesday: Anna is on vacation (plan hours kept), Bo works.
 */
function grid(): PlannerGridModel {
  const workers = [
    worker(1, 101, 'Frederik Holm', [cell(MON, 5), cell(TUE, 5)]),
    worker(2, 102, 'Anna Hansen', [cell(MON, 0), cell(TUE, 7.5, 1)]),
    worker(3, 103, 'Bo Jensen', [cell(MON, 7.5), cell(TUE, 7.5)]),
    worker(4, 104, 'Carl Berg', [cell(MON, 7.5), cell(TUE, 0)]),
  ];
  return {
    from: MON, to: TUE, dates: [MON, TUE], workers, googleSheetRetired: false, pendingRequestCount: 1,
    dayTotals: [
      {date: MON, plannedHeadcount: 3, pendingAbsenceRequests: 1},
      {date: TUE, plannedHeadcount: 2, pendingAbsenceRequests: 1},
    ],
  };
}

const request = (extra: object = {}) => ({
  id: 7, status: 'Pending', requestedBySdkSitId: 101, dateFrom: MON, dateTo: TUE,
  requestedAtUtc: '2026-10-01T12:12:00Z', requestComment: 'Efterårstur', days: [{date: MON, messageId: 1}], ...extra,
});

describe('PlannerRequestsPanelComponent', () => {
  afterEach(() => {
    vi.useRealTimers();
  });

  it('stops loading when the inbox read gets no reply (the host interceptor answers with EMPTY)', () => {
    const panel = createPanel(vi.fn(() => EMPTY));

    panel.load();

    expect(panel.loading).toBe(false);
  });

  it('ignores an older inbox reply that lands after a newer read was started', () => {
    const first = new Subject<unknown>();
    const second = new Subject<unknown>();
    const panel = createPanel(vi.fn().mockReturnValueOnce(first).mockReturnValueOnce(second));

    panel.load();
    panel.load();
    second.next({success: true, model: []});
    first.next({success: true, model: [{status: 'Pending', requestedBySdkSitId: 1, dateFrom: '2026-11-19', dateTo: '2026-11-20', days: []}]});

    expect(panel.views).toEqual([]);
  });

  describe('"Sendt"', () => {
    it('says today, yesterday, or the date, with the local time', () => {
      vi.useFakeTimers({toFake: ['Date']});
      vi.setSystemTime(new Date(2026, 9, 7, 15, 0));
      const panel = createPanel(vi.fn(() => EMPTY));

      expect(panel.formatSent(new Date(2026, 9, 7, 8, 40))).toBe('plannerSentToday{"time":"08:40"}');
      expect(panel.formatSent(new Date(2026, 9, 6, 14, 12))).toBe('plannerSentYesterday{"time":"14:12"}');
      const older = new Date(2026, 9, 1, 9, 5);
      expect(panel.formatSent(older)).toBe(`plannerSentOn${JSON.stringify({
        date: older.toLocaleDateString('da', {day: 'numeric', month: 'short', year: 'numeric'}), time: '09:05',
      })}`);
    });

    it('reads a UTC string from the server in local time, and is empty without one', () => {
      const panel = createPanel(vi.fn(() => EMPTY));
      const utc = '2026-10-01T06:40:00Z';
      const local = new Date(utc);
      const hhmm = `${String(local.getHours()).padStart(2, '0')}:${String(local.getMinutes()).padStart(2, '0')}`;

      expect(panel.formatSent(utc)).toContain(`"time":"${hhmm}"`);
      expect(panel.formatSent(null)).toBe('');
    });
  });

  describe('"Fra planen"', () => {
    it('asks the absence preview for the worker\'s range and type, and shows its days and hours', () => {
      const previewAbsence = vi.fn(() => of({success: true, model: {
        workDays: 0, plannedHours: 0,
        days: [
          {date: MON, planText: '7-12', planHours: 5, outcome: 'overlap'},
          {date: TUE, planText: '7-12', planHours: 5, outcome: 'overlap'},
        ],
      }}));
      const panel = createPanel(vi.fn(() => of({success: true, model: [request()]})), previewAbsence);
      panel.grid = grid();

      panel.load();

      expect(previewAbsence).toHaveBeenCalledWith({
        assignedSiteId: 1, from: '2026-10-05T00:00:00.000Z', to: '2026-10-06T00:00:00.000Z', messageId: 1,
      });
      expect(panel.views[0].fromPlan).toBe('plannerWorkDaysHours{"days":2,"hours":"10,00"}');
    });

    it('says "1 arbejdsdag" for a single day', () => {
      const previewAbsence = vi.fn(() => of({success: true, model: {
        workDays: 1, plannedHours: 7.5, days: [{date: MON, planText: '7-15/0,5', planHours: 7.5, outcome: 'apply'}],
      }}));
      const panel = createPanel(vi.fn(() => of({success: true, model: [request({dateTo: MON})]})), previewAbsence);
      panel.grid = grid();

      panel.load();

      expect(panel.views[0].fromPlan).toBe('plannerOneWorkDayHours{"hours":"7,50"}');
    });

    it('is left out when the preview is refused, fails, or the worker is not in the grid', () => {
      for (const reply of [of({success: false, message: 'UnauthorizedToApprove'}), throwError(() => new Error('500')), EMPTY]) {
        TestBed.resetTestingModule();
        const panel = createPanel(vi.fn(() => of({success: true, model: [request()]})), vi.fn(() => reply));
        panel.grid = grid();
        panel.load();
        expect(panel.views[0].fromPlan).toBeNull();
      }

      TestBed.resetTestingModule();
      const previewAbsence = vi.fn(() => EMPTY);
      const panel = createPanel(vi.fn(() => of({success: true, model: [request({requestedBySdkSitId: 999})]})), previewAbsence);
      panel.grid = grid();
      panel.load();
      expect(previewAbsence).not.toHaveBeenCalled();
      expect(panel.views[0].fromPlan).toBeNull();
    });

    it('cancels the previews of the old views when the views are rebuilt', () => {
      const pending = new Subject<unknown>();
      let subscribed = 0;
      const previewAbsence = vi.fn(() => new Observable(subscriber => {
        subscribed++;
        const sub = pending.subscribe(subscriber);
        return () => { subscribed--; sub.unsubscribe(); };
      }));
      const panel = createPanel(vi.fn(() => of({success: true, model: [request()]})), previewAbsence);
      panel.grid = grid();
      panel.load();
      expect(subscribed).toBe(1);

      panel.ngOnChanges();

      expect(previewAbsence).toHaveBeenCalledTimes(2);
      expect(subscribed).toBe(1);
    });

    it('counts the plan days the request would take, not locked days or days without a plan', () => {
      expect(planFromPreview({workDays: 0, plannedHours: 0, days: [
        {date: MON, planText: '', planHours: 7.5, outcome: 'apply'},
        {date: TUE, planText: '', planHours: 5, outcome: 'overlap'},
        {date: '2026-10-07', planText: '', planHours: 8, outcome: 'locked'},
        {date: '2026-10-08', planText: '', planHours: 0, outcome: 'dayOff'},
      ]})).toEqual({days: 2, hours: 12.5});
    });
  });

  describe('staffing warning', () => {
    it('names the colleagues already absent, with n of m at work', () => {
      const days = staffingDays(grid(), grid().workers[0], new Date(2026, 9, 5), new Date(2026, 9, 6));

      // Monday: nobody absent and Bo and Carl still work, so no warning.
      // Tuesday: Anna is absent; planned 3 (Frederik, Anna, Bo), at work after approving: Bo.
      expect(days).toEqual([{date: new Date(2026, 9, 6), absentNames: ['Anna Hansen'], absentTypeKey: 'DayOff', working: 1, planned: 3}]);
    });

    it('still warns when approving leaves nobody at work', () => {
      const g = grid();
      g.workers = [g.workers[0]];
      g.dayTotals = [{date: MON, plannedHeadcount: 1, pendingAbsenceRequests: 1}];

      expect(staffingDays(g, g.workers[0], new Date(2026, 9, 5), new Date(2026, 9, 5)))
        .toEqual([{date: new Date(2026, 9, 5), absentNames: [], absentTypeKey: null, working: 0, planned: 1}]);
    });

    it('ignores days the worker is not planned, days outside the request and an unknown worker', () => {
      const g = grid();
      g.workers[0].cells = [cell(MON, 5), cell(TUE, 0)];

      expect(staffingDays(g, g.workers[0], new Date(2026, 9, 5), new Date(2026, 9, 6))).toEqual([]);
      expect(staffingDays(grid(), grid().workers[0], new Date(2026, 9, 5), new Date(2026, 9, 5))).toEqual([]);
      expect(staffingDays(grid(), null, new Date(2026, 9, 5), new Date(2026, 9, 6))).toEqual([]);
    });

    it('shows the warning text on the card', () => {
      const panel = createPanel(vi.fn(() => of({success: true, model: [request()]})));
      panel.grid = grid();

      panel.load();

      const day = new Date(2026, 9, 6).toLocaleDateString('da', {weekday: 'long', day: 'numeric', month: 'short'});
      expect(panel.views[0].staffing).toEqual([`plannerStaffingOthersAbsent${JSON.stringify({
        day: day.charAt(0).toLocaleUpperCase() + day.slice(1), n: 1, m: 3, names: 'Anna Hansen', type: 'dayoff',
      })}`]);
      expect(panel.views[0].initials).toBe('FH');
    });

    it('names the type when every absent colleague shares it, and says "fravær" when the types differ or are unknown', () => {
      const g = grid();
      // Tuesday: Carl is planned too, and on vacation like Anna.
      g.workers[1].cells = [cell(MON, 0), cell(TUE, 7.5, 2)];
      g.workers[3].cells = [cell(MON, 7.5), cell(TUE, 7.5, 2)];
      const range = [new Date(2026, 9, 6), new Date(2026, 9, 6)] as const;
      expect(staffingDays(g, g.workers[0], ...range)[0].absentTypeKey).toBe('Vacation');

      g.workers[3].cells = [cell(MON, 7.5), cell(TUE, 7.5, 3)];
      expect(staffingDays(g, g.workers[0], ...range)[0].absentTypeKey).toBeNull();

      g.workers[3].cells = [cell(MON, 7.5), cell(TUE, 7.5, 99)];
      g.workers[1].cells = [cell(MON, 0), cell(TUE, 7.5, 99)];
      expect(staffingDays(g, g.workers[0], ...range)[0].absentTypeKey).toBeNull();
    });

    it('writes the generic "fravær" in lower case for mixed types', () => {
      const g = grid();
      g.workers[3].cells = [cell(MON, 7.5), cell(TUE, 7.5, 3)];
      const panel = createPanel(vi.fn(() => of({success: true, model: [request()]})));
      panel.grid = g;

      panel.load();

      expect(panel.views[0].staffing[0]).toContain('"names":"Anna Hansen, Carl Berg","type":"plannerabsencegeneric"');
    });

    it('has a {{type}} in every language\'s staffing warning and a generic word for it', () => {
      for (const lang of ALL_LANGUAGES) {
        expect(lang['plannerStaffingOthersAbsent']).toContain('{{type}}');
        expect(lang['plannerAbsenceGeneric']).toBeTruthy();
      }
      expect(da.plannerStaffingOthersAbsent.replace('{{type}}', 'ferie'))
        .toBe('{{day}} har {{names}} allerede ferie – så er I {{n}} af {{m}} på arbejde.');
    });
  });
});

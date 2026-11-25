import {afterEach, beforeEach, describe, expect, it, vi} from 'vitest';
import {TestBed} from '@angular/core/testing';
import {provideNativeDateAdapter} from '@angular/material/core';
import {MAT_DIALOG_DATA, MatDialogRef} from '@angular/material/dialog';
import {provideTranslateService, TranslateService} from '@ngx-translate/core';
import {ToastrService} from 'ngx-toastr';
import {EMPTY, of, Subject, throwError} from 'rxjs';
import {PlannerAbsencePreviewModel, PlannerWorkerModel} from '../../models';
import {TimePlanningPnPlannerService} from '../../services';
import {detectChanges} from '../../../../testing/detect-changes';
import {da} from '../../../../i18n/da';
import {
  PlannerAbsenceDialogComponent, PlannerAbsenceDialogData, PREVIEW_DEBOUNCE_MS,
} from './planner-absence-dialog.component';

const VACATION = 2;
const DAY_OFF = 1;

const worker = (id: number, canApproveAbsence: boolean, siteName = `Worker ${id}`) => ({
  assignedSiteId: id, siteId: id + 100, siteName, canApproveAbsence, hasPattern: true,
} as PlannerWorkerModel);

const preview = (days: { date: string; outcome: string; planText?: string; planHours?: number }[]): PlannerAbsencePreviewModel => {
  const rows = days.map(d => ({date: d.date, planText: d.planText ?? '', planHours: d.planHours ?? 0, outcome: d.outcome}));
  const applied = rows.filter(r => r.outcome === 'apply');
  return {days: rows, workDays: applied.length, plannedHours: applied.reduce((s, r) => s + r.planHours, 0)};
};

/** Mon 9 - Fri 13 Nov work 7.5 h, Sat 14 + Sun 15 are skipped. */
const WEEK = preview([
  ...['09', '10', '11', '12', '13'].map(d => ({date: `2026-11-${d}T00:00:00`, outcome: 'apply', planText: '7-15/0,5', planHours: 7.5})),
  {date: '2026-11-14T00:00:00', outcome: 'dayOff'},
  {date: '2026-11-15T00:00:00', outcome: 'dayOff'},
]);

describe('PlannerAbsenceDialogComponent', () => {
  let service: { previewAbsence: ReturnType<typeof vi.fn>; createAbsence: ReturnType<typeof vi.fn> };
  let dialogRef: { close: ReturnType<typeof vi.fn> };

  const labels: Record<string, string> = {Vacation: 'Ferie', DayOff: 'Fridag'};
  const translate = {
    currentLang: 'da',
    defaultLang: 'da',
    instant: (key: string, params?: Record<string, unknown>) =>
      labels[key] ?? (params ? `${key} ${JSON.stringify(params)}` : key),
  };

  function create(data: PlannerAbsenceDialogData): PlannerAbsenceDialogComponent {
    TestBed.configureTestingModule({
      providers: [
        {provide: TimePlanningPnPlannerService, useValue: service},
        {provide: MatDialogRef, useValue: dialogRef},
        {provide: MAT_DIALOG_DATA, useValue: data},
        {provide: ToastrService, useValue: {success: vi.fn(), error: vi.fn(), warning: vi.fn()}},
        {provide: TranslateService, useValue: translate},
      ],
    });
    return TestBed.runInInjectionContext(() => new PlannerAbsenceDialogComponent());
  }

  beforeEach(() => {
    vi.useFakeTimers();
    service = {previewAbsence: vi.fn(() => of({success: true, model: WEEK})), createAbsence: vi.fn(() => EMPTY)};
    dialogRef = {close: vi.fn()};
  });

  afterEach(() => {
    vi.useRealTimers();
  });

  /** Lets the debounced preview request go out. */
  const settle = () => vi.advanceTimersByTime(PREVIEW_DEBOUNCE_MS);

  describe('why "Registrér" is disabled', () => {
    const full = (): PlannerAbsenceDialogData => ({
      workers: [worker(1, true), worker(2, false)],
      assignedSiteId: 1,
      from: new Date(2026, 10, 9),
      to: new Date(2026, 10, 15),
    });

    it('starts on vacation and is ready once the preview has working days', () => {
      const dialog = create(full());
      expect(dialog.messageId).toBe(VACATION);
      expect(dialog.previewState).toBe('loading');
      expect(dialog.canSubmit).toBe(false);
      expect(dialog.missingText).toBeNull();

      settle();
      expect(dialog.previewState).toBe('ready');
      expect(dialog.missing).toEqual([]);
      expect(dialog.canSubmit).toBe(true);
      expect(dialog.missingText).toBeNull();
    });

    it('needs a worker', () => {
      const dialog = create({...full(), assignedSiteId: null});
      expect(dialog.missing).toEqual(['worker']);
      expect(dialog.canSubmit).toBe(false);
      expect(dialog.missingText).toBe('plannerMissing {"items":"plannerMissingWorker"}');
      expect(service.previewAbsence).not.toHaveBeenCalled();
    });

    it('needs the user to be the worker\'s leader, and does not ask for a preview then', () => {
      const dialog = create({...full(), assignedSiteId: 2});
      settle();
      expect(dialog.missing).toEqual(['notLeader']);
      expect(dialog.canSubmit).toBe(false);
      expect(service.previewAbsence).not.toHaveBeenCalled();
    });

    it('needs a type', () => {
      const dialog = create(full());
      settle();
      dialog.messageId = null;
      expect(dialog.missing).toEqual(['type']);
      expect(dialog.canSubmit).toBe(false);
    });

    it('needs both dates, the first not after the last', () => {
      const dialog = create({...full(), to: null});
      expect(dialog.missing).toEqual(['dates']);
      dialog.to = new Date(2026, 10, 1);
      dialog.onDatesChange();
      expect(dialog.missing).toEqual(['dates']);
      expect(dialog.previewState).toBe('idle');
      expect(dialog.canSubmit).toBe(false);
    });

    it('needs at least one working day in the period', () => {
      service.previewAbsence.mockReturnValue(of({success: true, model: preview([
        {date: '2026-11-14T00:00:00', outcome: 'dayOff'}, {date: '2026-11-15T00:00:00', outcome: 'dayOff'},
      ])}));
      const dialog = create(full());
      settle();
      expect(dialog.missing).toEqual(['noWorkDays']);
      expect(dialog.canSubmit).toBe(false);
    });

    it('refuses a period with a locked day, as the server would', () => {
      service.previewAbsence.mockReturnValue(of({success: true, model: preview([
        {date: '2026-11-09T00:00:00', outcome: 'locked', planText: '7-15', planHours: 8},
        {date: '2026-11-10T00:00:00', outcome: 'apply', planText: '7-15', planHours: 8},
      ])}));
      const dialog = create(full());
      settle();
      expect(dialog.missing).toEqual(['locked']);
      expect(dialog.missingText).toContain('plannerMissingLocked {\\"count\\":1}');
      expect(dialog.canSubmit).toBe(false);
    });

    it('refuses a period with a day already held by another request', () => {
      service.previewAbsence.mockReturnValue(of({success: true, model: preview([
        {date: '2026-11-09T00:00:00', outcome: 'overlap', planText: '7-15', planHours: 8},
        {date: '2026-11-10T00:00:00', outcome: 'apply', planText: '7-15', planHours: 8},
      ])}));
      const dialog = create(full());
      settle();
      expect(dialog.previewRows[0].outcome).toBe('overlap');
      expect(dialog.missing).toEqual(['overlap']);
      expect(dialog.missingText).toContain('plannerMissingOverlap {\\"count\\":1}');
      expect(dialog.canSubmit).toBe(false);
    });

    it('lists every reason at once', () => {
      const dialog = create({workers: [worker(1, true)], assignedSiteId: null, from: null, to: null});
      dialog.assignedSiteId = null;
      dialog.messageId = null;
      expect(dialog.missing).toEqual(['worker', 'type', 'dates']);
    });

    it('does not block on a failed preview: the server checks again on create', () => {
      service.previewAbsence.mockReturnValue(throwError(() => new Error('down')));
      const dialog = create(full());
      settle();
      expect(dialog.previewState).toBe('failed');
      expect(dialog.canSubmit).toBe(true);
    });

    it('does not block when the preview gets no reply (the interceptor\'s EMPTY)', () => {
      service.previewAbsence.mockReturnValue(EMPTY);
      const dialog = create(full());
      settle();
      expect(dialog.previewState).toBe('failed');
      expect(dialog.canSubmit).toBe(true);
    });

    it('shows the server\'s refusal of the preview', () => {
      service.previewAbsence.mockReturnValue(of({success: false, message: 'Ikke tilladt'}));
      const dialog = create(full());
      settle();
      expect(dialog.previewState).toBe('failed');
      expect(dialog.previewError).toBe('Ikke tilladt');
    });
  });

  describe('when nobody can be approved', () => {
    it('tells a non-admin to be set up as leader', () => {
      const dialog = create({workers: [worker(1, false), worker(2, false)], isAdmin: false});
      expect(dialog.noApprovableWorker).toBe(true);
      expect(dialog.noApprovableMessageKey).toBe('plannerAbsenceNotLeaderOfAnyone');
    });

    it('does not give an admin the leader advice', () => {
      const dialog = create({workers: [worker(1, false)], isAdmin: true});
      expect(dialog.noApprovableMessageKey).toBe('plannerAbsenceNoneApprovable');
    });

    it('says there are no workers when the list is empty', () => {
      const dialog = create({workers: [], isAdmin: false});
      expect(dialog.noApprovableMessageKey).toBe('plannerNoWorkers');
    });

    it('says nothing while one worker is approvable', () => {
      const dialog = create({workers: [worker(1, false), worker(2, true)]});
      expect(dialog.noApprovableMessageKey).toBeNull();
    });
  });

  describe('preview', () => {
    const data = (): PlannerAbsenceDialogData => ({
      workers: [worker(1, true)], assignedSiteId: 1, from: new Date(2026, 10, 9), to: new Date(2026, 10, 15),
    });

    it('asks once per pause, with the dialog\'s worker, period and type', () => {
      const dialog = create(data());
      dialog.onDatesChange();
      dialog.onDatesChange();
      settle();
      expect(service.previewAbsence).toHaveBeenCalledTimes(1);
      expect(service.previewAbsence).toHaveBeenCalledWith({
        assignedSiteId: 1, from: '2026-11-09T00:00:00.000Z', to: '2026-11-15T00:00:00.000Z', messageId: VACATION,
      });
    });

    it('maps the days and their outcomes; an unknown outcome reads as unchanged', () => {
      service.previewAbsence.mockReturnValue(of({success: true, model: preview([
        {date: '2026-11-09T00:00:00', outcome: 'apply', planText: '7-15', planHours: 8},
        {date: '2026-11-14T00:00:00', outcome: 'dayOff'},
        {date: '2026-11-15T00:00:00', outcome: 'somethingNew'},
      ])}));
      const dialog = create(data());
      settle();
      expect(dialog.previewRows.map(r => r.outcome)).toEqual(['apply', 'dayOff', 'other']);
      expect(dialog.previewRows[0].date).toEqual(new Date(2026, 10, 9));
      expect(dialog.planLabel(dialog.previewRows[1])).toBe('plannerAbsencePlanWeekend');
      expect(dialog.previewHeadline).toBe('plannerAbsencePreviewHeadline {"days":1,"hours":"8,00","type":"ferie"}');
    });

    it('reads dates the host\'s DateInterceptor already turned into Dates (UTC midnight)', () => {
      service.previewAbsence.mockReturnValue(of({success: true, model: {
        days: [{date: new Date(Date.UTC(2026, 10, 9)), planText: '7-15', planHours: 8, outcome: 'apply'}], workDays: 1, plannedHours: 8,
      }}));
      const dialog = create(data());
      settle();
      expect(dialog.previewRows[0].key).toBe('2026-11-09');
      expect(dialog.previewRows[0].date).toEqual(new Date(2026, 10, 9));
    });

    it('drops a stale answer: only the latest request is shown', () => {
      const first = new Subject<unknown>();
      const second = new Subject<unknown>();
      service.previewAbsence.mockReturnValueOnce(first).mockReturnValueOnce(second);
      const dialog = create(data());
      settle();
      expect(first.observed).toBe(true);

      dialog.to = new Date(2026, 10, 10);
      dialog.onDatesChange();
      settle();
      expect(first.observed).toBe(false);
      expect(second.observed).toBe(true);

      // The older answer arrives late and is ignored.
      first.next({success: true, model: WEEK});
      expect(dialog.previewState).toBe('loading');

      second.next({success: true, model: preview([{date: '2026-11-09T00:00:00', outcome: 'apply', planHours: 7.5}])});
      expect(dialog.previewState).toBe('ready');
      expect(dialog.previewRows.length).toBe(1);
    });

    it('drops the reply in flight as soon as the dates change, not after the pause', () => {
      const old = new Subject<unknown>();
      service.previewAbsence.mockReturnValueOnce(old).mockReturnValue(new Subject<unknown>());
      const dialog = create(data());
      settle();
      expect(old.observed).toBe(true);

      vi.advanceTimersByTime(100);
      dialog.to = new Date(2026, 10, 10);
      dialog.onDatesChange();
      expect(old.observed).toBe(false);

      // The old period's reply lands inside the new pause: ignored.
      old.next({success: true, model: WEEK});
      expect(dialog.previewState).toBe('loading');
      expect(dialog.previewRows).toEqual([]);
      expect(dialog.canSubmit).toBe(false);
    });

    it('keeps the preview cleared when switching to a non-leader worker while a request is in flight', () => {
      const old = new Subject<unknown>();
      service.previewAbsence.mockReturnValueOnce(old);
      const dialog = create({...data(), workers: [worker(1, true), worker(2, false)]});
      settle();

      dialog.onWorkerChange(2);
      old.next({success: true, model: WEEK});
      settle();
      expect(dialog.previewState).toBe('idle');
      expect(dialog.previewRows).toEqual([]);
      expect(dialog.missing).toEqual(['notLeader']);
      expect(service.previewAbsence).toHaveBeenCalledTimes(1);
    });

    it('clears the preview when the period can no longer be previewed', () => {
      const dialog = create(data());
      settle();
      expect(dialog.previewRows.length).toBe(7);
      dialog.onWorkerChange(null);
      expect(dialog.previewState).toBe('idle');
      expect(dialog.previewRows).toEqual([]);
    });
  });

  describe('call to action', () => {
    it('follows the type', () => {
      const dialog = create({workers: [worker(1, true)], assignedSiteId: 1});
      expect(dialog.ctaText).toBe('plannerRegisterAbsenceType {"type":"ferie"}');
      dialog.pickType(DAY_OFF);
      expect(dialog.ctaText).toBe('plannerRegisterAbsenceType {"type":"fridag"}');
      dialog.messageId = null;
      expect(dialog.ctaText).toBe('plannerRegisterAbsence');
    });

    it('moves the type with the arrow keys', () => {
      const dialog = create({workers: [worker(1, true)], assignedSiteId: 1});
      const index = dialog.absenceTypes.findIndex(t => t.id === dialog.messageId);
      dialog.onTypeKeydown(new KeyboardEvent('keydown', {key: 'ArrowRight'}), index);
      expect(dialog.messageId).toBe(dialog.absenceTypes[index + 1].id);
      dialog.onTypeKeydown(new KeyboardEvent('keydown', {key: 'Home'}), index + 1);
      expect(dialog.messageId).toBe(dialog.absenceTypes[0].id);
    });

    it('sends the chosen type with the period', () => {
      const dialog = create({workers: [worker(1, true)], assignedSiteId: 1, from: new Date(2026, 10, 9), to: new Date(2026, 10, 15)});
      settle();
      dialog.submit();
      expect(service.createAbsence).toHaveBeenCalledWith(expect.objectContaining({
        assignedSiteId: 1, messageId: VACATION, from: '2026-11-09T00:00:00.000Z', to: '2026-11-15T00:00:00.000Z',
      }));
    });
  });
});

/** The template: what the user actually reads. */
describe('PlannerAbsenceDialogComponent template', () => {
  let service: { previewAbsence: ReturnType<typeof vi.fn>; createAbsence: ReturnType<typeof vi.fn> };

  async function render(data: PlannerAbsenceDialogData) {
    service = {previewAbsence: vi.fn(() => of({success: true, model: WEEK})), createAbsence: vi.fn(() => EMPTY)};
    await TestBed.configureTestingModule({
      imports: [PlannerAbsenceDialogComponent],
      providers: [
        provideTranslateService({fallbackLang: 'da'}),
        provideNativeDateAdapter(),
        {provide: TimePlanningPnPlannerService, useValue: service},
        {provide: MatDialogRef, useValue: {close: vi.fn()}},
        {provide: MAT_DIALOG_DATA, useValue: data},
        {provide: ToastrService, useValue: {success: vi.fn(), error: vi.fn(), warning: vi.fn()}},
      ],
    }).compileComponents();
    const translate = TestBed.inject(TranslateService);
    translate.setTranslation('da', da);
    translate.use('da');
    const fixture = TestBed.createComponent(PlannerAbsenceDialogComponent);
    detectChanges(fixture);
    return fixture;
  }

  const text = (el: HTMLElement, testId: string) =>
    el.querySelector(`[data-testid="${testId}"]`)?.textContent?.replace(/\s+/g, ' ').trim() ?? null;

  it('explains a non-leader why nothing can be registered, and keeps the button disabled', async () => {
    const fixture = await render({workers: [worker(1, false, 'Anna Hansen')], isAdmin: false});
    expect(fixture.componentInstance.assignedSiteId).toBe(1);
    const el: HTMLElement = fixture.nativeElement;
    expect(text(el, 'planner-absence-no-approvable')).toContain('ikke er sat op som leder');
    expect(text(el, 'planner-absence-no-approvable')).toContain('Plantimer');
    // The only worker is preselected, so the line names the real obstacle.
    expect(text(el, 'planner-absence-missing')).toBe('Mangler: du er ikke leder for medarbejderen, vælg fra- og til-dato');
    expect((el.querySelector('[data-testid="planner-absence-save"]') as HTMLButtonElement).disabled).toBe(true);
  });

  it('keeps a non-approvable worker selectable, so the hint shows', async () => {
    const fixture = await render({workers: [worker(1, false, 'Anna Hansen'), worker(2, true)], assignedSiteId: 1});
    const el: HTMLElement = fixture.nativeElement;
    expect(text(el, 'planner-absence-not-manager')).toBe(da.plannerNotManagerOfWorker);
    expect(text(el, 'planner-absence-missing')).toContain('du er ikke leder for medarbejderen');
    expect(el.querySelector('[data-testid="planner-absence-no-approvable"]')).toBeNull();
  });

  it('renders the preview and a CTA that names the type', async () => {
    vi.useFakeTimers();
    try {
      const fixture = await render({
        workers: [worker(1, true, 'Anna Hansen')], assignedSiteId: 1, from: new Date(2026, 10, 9), to: new Date(2026, 10, 15),
      });
      vi.advanceTimersByTime(PREVIEW_DEBOUNCE_MS);
      detectChanges(fixture);
      const el: HTMLElement = fixture.nativeElement;
      expect(text(el, 'planner-absence-preview-headline')).toBe('5 arbejdsdage · 37,50 planlagte timer bliver til ferie');
      expect(el.querySelector('[data-testid="planner-absence-preview-row-2026-11-09"]')?.getAttribute('data-outcome')).toBe('apply');
      expect(text(el, 'planner-absence-preview-row-2026-11-14')).toContain('springes over');
      expect(text(el, 'planner-absence-save')).toContain('Registrér ferie');
      expect(el.querySelector('[data-testid="planner-absence-missing"]')).toBeNull();
      expect((el.querySelector('[data-testid="planner-absence-save"]') as HTMLButtonElement).disabled).toBe(false);
      expect(el.querySelector('[data-testid="planner-absence-type-2"]')?.getAttribute('aria-checked')).toBe('true');
    } finally {
      vi.useRealTimers();
    }
  });
});

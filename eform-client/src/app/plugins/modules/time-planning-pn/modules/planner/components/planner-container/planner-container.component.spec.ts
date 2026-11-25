import {afterEach, describe, it, expect, beforeEach, vi} from 'vitest';
import {ElementRef, NgZone} from '@angular/core';
import {TestBed} from '@angular/core/testing';
import {Overlay} from '@angular/cdk/overlay';
import {MatDialog} from '@angular/material/dialog';
import {TranslateModule, TranslateService} from '@ngx-translate/core';
import {ToastrService} from 'ngx-toastr';
import {Store} from '@ngrx/store';
import {AppMenuStateService} from 'src/app/common/store';
import {EMPTY, of} from 'rxjs';
import {TimePlanningPnSettingsService} from '../../../../services';
import {TimePlanningPnPlannerService} from '../../services';
import {PlannerGridModel, PlannerWorkerModel} from '../../models';
import {da} from '../../../../i18n/da';
import {pageProviders} from '../../../../testing/page-providers';
import {PLANNER_HELP_DISMISSED_KEY, PlannerContainerComponent} from './planner-container.component';

const storeStub = (isAdmin = false) => ({provide: Store, useValue: {select: vi.fn(() => of(isAdmin))}});

/** The editor's guards, without rendering the grid. */
describe('PlannerContainerComponent short-form editor', () => {
  let component: PlannerContainerComponent;
  let updateCells: ReturnType<typeof vi.fn>;
  let getGrid: ReturnType<typeof vi.fn>;
  let toastr: { success: ReturnType<typeof vi.fn>; error: ReturnType<typeof vi.fn>; warning: ReturnType<typeof vi.fn> };

  beforeEach(() => {
    updateCells = vi.fn(() => of({success: true, model: {updatedCount: 1, lockedCells: [], errors: [], lockedMessage: null}}));
    getGrid = vi.fn(() => of({success: false}));
    toastr = {success: vi.fn(), error: vi.fn(), warning: vi.fn()};
    TestBed.configureTestingModule({
      providers: [
        {provide: TimePlanningPnPlannerService, useValue: {updateCells, getGrid}},
        storeStub(),
        {provide: TimePlanningPnSettingsService, useValue: {getAvailableTags: vi.fn(() => of({success: true, model: []}))}},
        {provide: MatDialog, useValue: {}},
        {provide: Overlay, useValue: {}},
        {provide: ToastrService, useValue: toastr},
        {provide: TranslateService, useValue: {instant: (key: string) => key, currentLang: 'da', defaultLang: 'da'}},
      ],
    });
    component = TestBed.runInInjectionContext(() => new PlannerContainerComponent());
    component.selection = new Set(['12|2026-11-09', '12|2026-11-10']);
  });

  it('does not clear the selected days when Enter is pressed in an empty box', () => {
    component.shortForm = '   ';
    component.onShortFormEnter();
    expect(updateCells).not.toHaveBeenCalled();
  });

  it('does not send unreadable text on Enter', () => {
    component.shortForm = '7-15 syg';
    component.onShortFormEnter();
    expect(updateCells).not.toHaveBeenCalled();
  });

  it('sends readable text on Enter, once while a save is in flight', () => {
    component.shortForm = '7-15/0,5';
    component.saving = true;
    component.onShortFormEnter();
    expect(updateCells).not.toHaveBeenCalled();

    component.saving = false;
    component.onShortFormEnter();
    expect(updateCells).toHaveBeenCalledTimes(1);
    expect(updateCells.mock.calls[0][0]).toMatchObject({shortForm: '7-15/0,5', messageId: null, mode: 'onlyTheseDays'});
  });

  it('shows the hours of readable shifts beside the box, in the grid\'s Danish format', () => {
    const instant = vi.spyOn(TestBed.inject(TranslateService), 'instant');

    component.shortForm = '7-15/0,5';
    expect(component.shortFormHours).toBe('plannerEditorHours');
    expect(instant).toHaveBeenLastCalledWith('plannerEditorHours', {hours: '7,50'});

    component.shortForm = '6-10;14-18/0,25';
    expect(component.shortFormHours).toBe('plannerEditorHours');
    expect(instant).toHaveBeenLastCalledWith('plannerEditorHours', {hours: '7,75'});
  });

  it('shows no hours for an empty box, "Fri", unreadable text or a shift without hours', () => {
    for (const text of ['', '   ', 'Fri', '7-15 syg', '7-8/2']) {
      component.shortForm = text;
      expect(component.shortFormHours, text).toBe('');
    }
  });

  it('still lets "Ryd" clear the days on purpose', () => {
    component.applyShortForm('');
    expect(updateCells).toHaveBeenCalledTimes(1);
    expect(updateCells.mock.calls[0][0]).toMatchObject({shortForm: ''});
  });

  it('releases saving and reloads the grid when the edit gets no reply', () => {
    // The host's HTTP interceptor completes with EMPTY on 401, failed refresh and exhausted retries.
    updateCells.mockReturnValue(EMPTY);
    component.shortForm = '7-15/0,5';
    component.onShortFormEnter();

    expect(component.saving).toBe(false);
    expect(getGrid).toHaveBeenCalledTimes(1);
    expect(toastr.warning).toHaveBeenCalledWith('plannerOutcomeUnknown');
    expect(component.selection.size).toBe(0);
  });

  it('releases loading when the grid read gets no reply', () => {
    getGrid.mockReturnValue(EMPTY);
    component.load();
    expect(component.loading).toBe(false);
  });
});

const worker = (id: number): PlannerWorkerModel => ({
  assignedSiteId: id, siteId: id + 100, siteName: `Worker ${id}`, avatarUrl: null, tags: [], hasPattern: true,
  rotationWeeks: 1, useGoogleSheetAsDefault: false, canApproveAbsence: true, weeklyNormHours: 37, weeks: [], cells: [],
});

const gridOf = (overrides: Partial<PlannerGridModel> = {}): PlannerGridModel => ({
  from: '2026-11-09T00:00:00', to: '2026-11-15T00:00:00', dates: [], dayTotals: [], workers: [worker(12), worker(13)],
  googleSheetRetired: true, pendingRequestCount: 0, ...overrides,
});

describe('PlannerContainerComponent periods, badge, help and dialogs', () => {
  let component: PlannerContainerComponent;
  let getGrid: ReturnType<typeof vi.fn>;
  let dialogOpen: ReturnType<typeof vi.fn>;

  const lastGridCall = () => getGrid.mock.calls[getGrid.mock.calls.length - 1];

  beforeEach(() => {
    vi.useFakeTimers();
    // Thursday 19 November 2026.
    vi.setSystemTime(new Date(2026, 10, 19, 10, 0, 0));
    try {
      localStorage.removeItem(PLANNER_HELP_DISMISSED_KEY);
    } catch {
      // jsdom always has storage; the guard matches the component's.
    }
    getGrid = vi.fn(() => of({success: true, model: gridOf()}));
    dialogOpen = vi.fn(() => ({afterClosed: () => EMPTY}));
    TestBed.configureTestingModule({
      providers: [
        {provide: TimePlanningPnPlannerService, useValue: {getGrid, updateCells: vi.fn()}},
        {provide: TimePlanningPnSettingsService, useValue: {getAvailableTags: vi.fn(() => of({success: true, model: []}))}},
        {provide: MatDialog, useValue: {open: dialogOpen}},
        {provide: Overlay, useValue: {scrollStrategies: {reposition: () => undefined}}},
        {provide: ToastrService, useValue: {success: vi.fn(), error: vi.fn(), warning: vi.fn()}},
        {provide: TranslateService, useValue: {instant: (key: string) => key, currentLang: 'da', defaultLang: 'da'}},
        storeStub(true),
      ],
    });
    component = TestBed.runInInjectionContext(() => new PlannerContainerComponent());
  });

  afterEach(() => {
    vi.useRealTimers();
  });

  describe('month view', () => {
    it('reads the whole calendar month from the 1st', () => {
      component.onViewChange('month');
      expect(component.from).toEqual(new Date(2026, 10, 1));
      expect(lastGridCall()[0]).toBe('2026-11-01T00:00:00.000Z');
      expect(lastGridCall()[1]).toBe(30);
    });

    it('steps by calendar month, with each month\'s own length', () => {
      component.onViewChange('month');
      component.next();
      expect(lastGridCall()[0]).toBe('2026-12-01T00:00:00.000Z');
      expect(lastGridCall()[1]).toBe(31);
      component.next();
      component.next();
      expect(lastGridCall()[0]).toBe('2027-02-01T00:00:00.000Z');
      expect(lastGridCall()[1]).toBe(28);
      component.previous();
      expect(lastGridCall()[0]).toBe('2027-01-01T00:00:00.000Z');
    });

    it('"I dag" goes to the current month, and back to weeks goes to today\'s week', () => {
      component.onViewChange('month');
      component.next();
      component.goToToday();
      expect(component.from).toEqual(new Date(2026, 10, 1));
      component.onViewChange('week');
      expect(component.from).toEqual(new Date(2026, 10, 16));
      expect(lastGridCall()[1]).toBe(7);
    });

    it('leaving the month that holds today lands on today\'s week', () => {
      component.onViewChange('month');
      component.onViewChange('twoWeeks');
      // Today is Thursday 19 November 2026.
      expect(component.from).toEqual(new Date(2026, 10, 16));
      expect(lastGridCall()[0]).toBe('2026-11-16T00:00:00.000Z');
      expect(lastGridCall()[1]).toBe(14);
    });

    it('leaving another month lands on the Monday of its first week', () => {
      component.onViewChange('month');
      component.next();
      component.onViewChange('week');
      // 1 December 2026 is a Tuesday: its week starts Monday 30 November.
      expect(component.from).toEqual(new Date(2026, 10, 30));
    });

    it('labels the month by name and keeps the range', () => {
      component.onViewChange('month');
      expect(component.periodTitle.toLowerCase()).toContain('2026');
      expect(component.periodEnd).toEqual(new Date(2026, 10, 30));
    });

    it('moves between the views with the arrow keys', () => {
      component.onViewKeydown(new KeyboardEvent('keydown', {key: 'ArrowRight'}), 1);
      expect(component.view).toBe('month');
      component.onViewKeydown(new KeyboardEvent('keydown', {key: 'ArrowRight'}), 2);
      expect(component.view).toBe('week');
      component.onViewKeydown(new KeyboardEvent('keydown', {key: 'End'}), 0);
      expect(component.view).toBe('month');
    });
  });

  describe('Ferieønsker count', () => {
    it('comes from the grid and is hidden at 0', () => {
      getGrid.mockReturnValue(of({success: true, model: gridOf({pendingRequestCount: 3})}));
      component.load();
      expect(component.pendingRequests).toBe(3);

      getGrid.mockReturnValue(of({success: true, model: gridOf({pendingRequestCount: 0})}));
      component.load();
      expect(component.pendingRequests).toBe(0);
    });
  });

  describe('help', () => {
    it('shows until dismissed, and remembers the dismissal', () => {
      expect(component.helpOpen).toBe(true);
      component.closeHelp();
      expect(component.helpOpen).toBe(false);
      expect(localStorage.getItem(PLANNER_HELP_DISMISSED_KEY)).toBe('true');

      const again = TestBed.runInInjectionContext(() => new PlannerContainerComponent());
      expect(again.helpOpen).toBe(false);
      again.toggleHelp();
      expect(again.helpOpen).toBe(true);
      expect(localStorage.getItem(PLANNER_HELP_DISMISSED_KEY)).toBeNull();
    });

    it('still works when storage throws', () => {
      const spy = vi.spyOn(Storage.prototype, 'setItem').mockImplementation(() => {
        throw new Error('blocked');
      });
      expect(() => component.closeHelp()).not.toThrow();
      expect(component.helpOpen).toBe(false);
      spy.mockRestore();
    });
  });

  describe('selection bar frame', () => {
    it('follows the grid when it resizes, measured inside the Angular zone', () => {
      let callback: () => void = () => undefined;
      const original = (globalThis as any).ResizeObserver;
      (globalThis as any).ResizeObserver = class {
        constructor(cb: () => void) { callback = cb; }
        observe() { return undefined; }
        disconnect() { return undefined; }
      };
      try {
        const zone = TestBed.inject(NgZone);
        const run = vi.spyOn(zone, 'run');
        let rect = {left: 300, width: 1500};
        const el = {getBoundingClientRect: () => rect} as unknown as HTMLElement;
        (component as any).gridScroll = new ElementRef(el);
        component.ngAfterViewInit();

        callback();
        expect(run).toHaveBeenCalled();
        expect(component.editorFrame).toEqual({left: 300, width: 1500});

        // The sidebar closes: the grid moves and widens, with no window resize.
        rect = {left: 40, width: 1760};
        callback();
        expect(component.editorFrame).toEqual({left: 40, width: 1760});
      } finally {
        (globalThis as any).ResizeObserver = original;
      }
    });
  });

  describe('dialogs', () => {
    beforeEach(() => {
      component.ngOnInit();
    });

    it('"Ferie" in the selection bar prefills one worker and the first to the last day', () => {
      component.selection = new Set(['12|2026-11-12', '12|2026-11-10', '12|2026-11-11']);
      component.openAbsence();
      const data = dialogOpen.mock.calls[0][1].data;
      expect(data.assignedSiteId).toBe(12);
      expect(data.from).toEqual(new Date(2026, 10, 10));
      expect(data.to).toEqual(new Date(2026, 10, 12));
      expect(data.isAdmin).toBe(true);
      expect(data.workers.map((w: PlannerWorkerModel) => w.assignedSiteId)).toEqual([12, 13]);
      expect(dialogOpen.mock.calls[0][1].width).toBe('720px');
    });

    it('leaves the worker open when several are selected', () => {
      component.selection = new Set(['12|2026-11-10', '13|2026-11-11']);
      component.openAbsence();
      expect(dialogOpen.mock.calls[0][1].data.assignedSiteId).toBeNull();
    });

    it('"Ugemønstre" opens on the picker, or on the one selected worker', () => {
      component.openPatterns();
      expect(dialogOpen.mock.calls[0][1].data.worker).toBeNull();
      expect(dialogOpen.mock.calls[0][1].data.workers.length).toBe(2);

      component.selection = new Set(['13|2026-11-10']);
      component.openPatterns();
      expect(dialogOpen.mock.calls[1][1].data.worker.assignedSiteId).toBe(13);
    });
  });
});

/** The week sum cell, rendered: the rotation week under "af N" for a rotating worker. */
describe('PlannerContainerComponent week sum', () => {
  const MONDAY = '2026-11-16T00:00:00';
  const weekSum = (rotationWeek: number | null) => ({
    isoYear: 2026, isoWeek: 47, weekStart: MONDAY, plannedHours: 37.5, normHours: 37, differenceHours: 0.5, rotationWeek,
  });

  async function render(workers: PlannerWorkerModel[]): Promise<HTMLElement> {
    const dates = Array.from({length: 7}, (_, i) => `2026-11-${16 + i}T00:00:00`);
    const getGrid = vi.fn(() => of({success: true, model: gridOf({from: dates[0], to: dates[6], dates, workers})}));
    TestBed.configureTestingModule({
      imports: [PlannerContainerComponent, TranslateModule.forRoot()],
      providers: [
        ...pageProviders(),
        // The page subheader reads its title from the menu.
        {provide: AppMenuStateService, useValue: {getTitleByUrl: () => ''}},
        {provide: TimePlanningPnPlannerService, useValue: {getGrid, updateCells: vi.fn()}},
        {provide: TimePlanningPnSettingsService, useValue: {getAvailableTags: vi.fn(() => of({success: true, model: []}))}},
        {provide: MatDialog, useValue: {open: vi.fn()}},
        {provide: ToastrService, useValue: {success: vi.fn(), error: vi.fn(), warning: vi.fn()}},
        storeStub(true),
      ],
    });
    const fixture = TestBed.createComponent(PlannerContainerComponent);
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();
    return fixture.nativeElement;
  }

  it('shows "rot.uge n/N" for a rotating worker, and nothing for a fixed week or an unknown rotation week', async () => {
    const el = await render([
      {...worker(12), rotationWeeks: 4, weeks: [weekSum(2)]},
      {...worker(13), rotationWeeks: 1, weeks: [weekSum(1)]},
      {...worker(14), rotationWeeks: 4, weeks: [weekSum(null)]},
    ]);

    const rotation = el.querySelector('[data-testid="planner-week-rotation-12-2026-11-16"]');
    expect(rotation?.textContent?.trim()).toBe('plannerRotationWeekOf');
    expect(rotation?.classList).toContain('planner-hint');
    // "af N" and the sum itself stay as they were.
    expect(el.querySelector('[data-testid="planner-week-12-2026-11-16"]')?.textContent).toContain('plannerOfNorm');
    expect(el.querySelector('[data-testid="planner-week-rotation-13-2026-11-16"]')).toBeNull();
    expect(el.querySelector('[data-testid="planner-week-rotation-14-2026-11-16"]')).toBeNull();
  });

  it('reads "rot.uge 2/4" in Danish', () => {
    expect(da.plannerRotationWeekOf.replace('{{week}}', '2').replace('{{weeks}}', '4')).toBe('rot.uge 2/4');
  });
});

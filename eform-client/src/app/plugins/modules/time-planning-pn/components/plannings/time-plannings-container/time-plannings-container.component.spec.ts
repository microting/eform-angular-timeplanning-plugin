import { readFileSync } from 'fs';
import { join } from 'path';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { TimePlanningsContainerComponent } from './time-plannings-container.component';
import { TimePlanningPnPlanningsService } from '../../../services/time-planning-pn-plannings.service';
import { TimePlanningPnSettingsService } from '../../../services/time-planning-pn-settings.service';
import { MatDialog } from '@angular/material/dialog';
import { Store } from '@ngrx/store';
import { selectCurrentUserIsFirstUser } from 'src/app/state';
import { BehaviorSubject, EMPTY, of, Subject, throwError } from 'rxjs';
import { addDays, endOfWeek, format, startOfWeek, subDays } from 'date-fns';
import { NO_ERRORS_SCHEMA } from '@angular/core';
import { TranslateModule, TranslateService } from '@ngx-translate/core';
import { ToastrService } from 'ngx-toastr';
import { HelpTourService } from '../../../help/services/help-tour.service';
import { HelpVisibilityService } from '../../../help/services/help-visibility.service';
import { LOCK_REQUEST_TIMEOUT_MS } from '../day-lock.util';

describe('TimePlanningsContainerComponent', () => {
  let component: TimePlanningsContainerComponent;
  let fixture: ComponentFixture<TimePlanningsContainerComponent>;
  let mockPlanningsService: jest.Mocked<TimePlanningPnPlanningsService>;
  let mockSettingsService: jest.Mocked<TimePlanningPnSettingsService>;
  let mockDialog: jest.Mocked<MatDialog>;
  let mockStore: jest.Mocked<Store>;

  beforeEach(async () => {
    mockPlanningsService = {
      getPlannings: jest.fn(),
      updatePlanning: jest.fn(),
      reconcileThrough: jest.fn(),
    } as any;
    mockSettingsService = {
      getAvailableSites: jest.fn(),
      getResignedSites: jest.fn(),
      getAssignedSite: jest.fn(),
      updateAssignedSite: jest.fn(),
      getAvailableTags: jest.fn(),
      getPayrollSettings: jest.fn(),
    } as any;
    mockDialog = {
      open: jest.fn(),
    } as any;
    mockStore = {
      select: jest.fn(),
    } as any;

    mockStore.select.mockReturnValue(of('en-US'));
    mockSettingsService.getAvailableSites.mockReturnValue(of({ success: true, model: [] }) as any);
    mockSettingsService.getAvailableTags.mockReturnValue(of({ success: true, model: [] }) as any);
    mockSettingsService.getPayrollSettings.mockReturnValue(of({ success: true, model: { payrollSystem: 0, cutoffDay: 19 } }) as any);
    mockPlanningsService.getPlannings.mockReturnValue(of({ success: true, model: [] }) as any);

    await TestBed.configureTestingModule({
      declarations: [TimePlanningsContainerComponent],
      imports: [TranslateModule.forRoot()],
      schemas: [NO_ERRORS_SCHEMA],
      providers: [
        { provide: TimePlanningPnPlanningsService, useValue: mockPlanningsService },
        { provide: TimePlanningPnSettingsService, useValue: mockSettingsService },
        { provide: MatDialog, useValue: mockDialog },
        { provide: Store, useValue: mockStore },
        {
          provide: ToastrService,
          useValue: { success: jest.fn(), warning: jest.fn(), error: jest.fn() },
        },
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(TimePlanningsContainerComponent);
    component = fixture.componentInstance;
    // Reconcile is the first user's, and most cases here never run ngOnInit, which is
    // where the flag would arrive from the store. Set once, so no case can quietly
    // exercise the bulk action as a user who would be offered nothing; the block that
    // proves the gate sets it per case instead.
    component.isFirstUser = true;
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  describe('Date Navigation', () => {
    beforeEach(() => {
      component.dateFrom = new Date(2024, 0, 15); // Jan 15, 2024
      component.dateTo = new Date(2024, 0, 21); // Jan 21, 2024
    });

    it('should move dates backward by 7 days when goBackward is called', () => {
      const expectedDateFrom = new Date(2024, 0, 8); // Jan 8, 2024
      const expectedDateTo = new Date(2024, 0, 14); // Jan 14, 2024

      component.goBackward();

      expect(component.dateFrom.getDate()).toBe(expectedDateFrom.getDate());
      expect(component.dateTo.getDate()).toBe(expectedDateTo.getDate());
      expect(mockPlanningsService.getPlannings).toHaveBeenCalled();
    });

    it('should move dates forward by 7 days when goForward is called', () => {
      const expectedDateFrom = new Date(2024, 0, 22); // Jan 22, 2024
      const expectedDateTo = new Date(2024, 0, 28); // Jan 28, 2024

      component.goForward();

      expect(component.dateFrom.getDate()).toBe(expectedDateFrom.getDate());
      expect(component.dateTo.getDate()).toBe(expectedDateTo.getDate());
      expect(mockPlanningsService.getPlannings).toHaveBeenCalled();
    });

    it('should not mutate original dates when navigating', () => {
      const originalDateFrom = new Date(component.dateFrom);
      const originalDateTo = new Date(component.dateTo);

      component.goForward();

      // The internal dates should have changed
      expect(component.dateFrom.getTime()).not.toBe(originalDateFrom.getTime());
      expect(component.dateTo.getTime()).not.toBe(originalDateTo.getTime());
    });
  });

  describe('Date Formatting', () => {
    it('should format date range correctly', () => {
      component.dateFrom = new Date(2024, 0, 15); // Jan 15, 2024
      component.dateTo = new Date(2024, 0, 21); // Jan 21, 2024

      const result = component.formatDateRange();

      expect(result).toBe('15.01.2024 - 21.01.2024');
    });

    it('should handle single digit days and months correctly', () => {
      component.dateFrom = new Date(2024, 0, 1); // Jan 1, 2024
      component.dateTo = new Date(2024, 0, 7); // Jan 7, 2024

      const result = component.formatDateRange();

      expect(result).toBe('01.01.2024 - 07.01.2024');
    });
  });

  describe('Event Handlers', () => {
    it('should call getPlannings when onTimePlanningChanged is triggered', () => {
      jest.spyOn(component, 'getPlannings');

      component.onTimePlanningChanged({});

      expect(component.getPlannings).toHaveBeenCalled();
    });

    it('should call getPlannings when onAssignedSiteChanged is triggered', () => {
      jest.spyOn(component, 'getPlannings');

      component.onAssignedSiteChanged({});

      expect(component.getPlannings).toHaveBeenCalled();
    });

    it('should update siteId and call getPlannings when onSiteChanged is triggered', () => {
      jest.spyOn(component, 'getPlannings');
      const testSiteId = 123;

      component.onSiteChanged(testSiteId);

      expect(component.siteId).toBe(testSiteId);
      expect(component.getPlannings).toHaveBeenCalled();
    });
  });

  describe('Dialog', () => {
    it('should open download excel dialog with available sites', () => {
      component.availableSites = [{ id: 1, name: 'Test Site' } as any];
      const mockDialogRef = { afterClosed: () => of(null) };
      mockDialog.open.mockReturnValue(mockDialogRef as any);

      component.openDownloadExcelDialog();

      expect(mockDialog.open).toHaveBeenCalled();
    });
  });

  describe('Show Resigned Sites', () => {
    it('should load resigned sites when showResignedSites is true', () => {
      mockSettingsService.getResignedSites.mockReturnValue(of({ success: true, model: [{ id: 1, name: 'Resigned Site' }] } as any));

      component.onShowResignedSitesChanged({ checked: true });

      expect(mockSettingsService.getResignedSites).toHaveBeenCalled();
      expect(component.showResignedSites).toBe(true);
    });

    it('should load available sites when showResignedSites is false', () => {
      component.showResignedSites = true;
      mockSettingsService.getAvailableSites.mockReturnValue(of({ success: true, model: [{ id: 1, name: 'Available Site' }] } as any));

      component.onShowResignedSitesChanged({ checked: false });

      expect(mockSettingsService.getAvailableSites).toHaveBeenCalled();
      expect(component.showResignedSites).toBe(false);
    });
  });

  describe('Tag Filtering', () => {
    it('should load available tags on init', () => {
      const mockTags = [
        { id: 1, name: 'Tag1' },
        { id: 2, name: 'Tag2' }
      ];
      mockSettingsService.getAvailableTags.mockReturnValue(of({ success: true, model: mockTags } as any));

      component.ngOnInit();

      expect(mockSettingsService.getAvailableTags).toHaveBeenCalled();
    });

    it('should update selectedTagIds and call getPlannings when onTagsChanged is triggered', () => {
      jest.spyOn(component, 'getPlannings');
      const testTagIds = [1, 2, 3];

      component.onTagsChanged(testTagIds);

      expect(component.selectedTagIds).toEqual(testTagIds);
      expect(component.getPlannings).toHaveBeenCalled();
    });

    it('should include tagIds in request when tags are selected', () => {
      component.selectedTagIds = [1, 2];
      component.dateFrom = new Date(2024, 0, 15);
      component.dateTo = new Date(2024, 0, 21);

      component.getPlannings();

      expect(mockPlanningsService.getPlannings).toHaveBeenCalledWith(
        expect.objectContaining({
          tagIds: [1, 2]
        })
      );
    });

    it('should not include tagIds in request when no tags are selected', () => {
      component.selectedTagIds = [];
      component.dateFrom = new Date(2024, 0, 15);
      component.dateTo = new Date(2024, 0, 21);

      component.getPlannings();

      expect(mockPlanningsService.getPlannings).toHaveBeenCalledWith(
        expect.objectContaining({
          tagIds: undefined
        })
      );
    });
  });

  describe('Page help tour', () => {
    let tour: HelpTourService;
    let start: jest.SpyInstance;

    beforeEach(() => {
      localStorage.clear();
      tour = TestBed.inject(HelpTourService);
      start = jest.spyOn(tour, 'start').mockImplementation(() => undefined);
      jest.useFakeTimers();
      component.dateFrom = new Date(2024, 0, 15);
      component.dateTo = new Date(2024, 0, 21);
    });

    afterEach(() => {
      jest.useRealTimers();
      start.mockRestore();
      localStorage.clear();
    });

    it('does not offer the tour while the grid has no rows', () => {
      // HelpTourService marks a tour seen as soon as it runs out of steps, and
      // that flag is persisted, so an empty first load would drop the three grid
      // steps and suppress them for good.
      mockPlanningsService.getPlannings.mockReturnValue(of({ success: true, model: [] }) as any);

      component.getPlannings();
      jest.runAllTimers();

      expect(start).not.toHaveBeenCalled();
    });

    it('offers the tour once rows have arrived', () => {
      mockPlanningsService.getPlannings.mockReturnValue(
        of({ success: true, model: [{ siteId: 1, siteName: 'A' }] }) as any);

      component.getPlannings();
      jest.runAllTimers();

      // Literals, not component.isAdmin / component.isFirstUser: reading the expected
      // values off the component under test asserts nothing about what was passed.
      expect(start).toHaveBeenCalledWith('page', { isAdmin: false, isFirstUser: true });
    });

    it('does not burn the once-per-page offer on a tour the help gate refuses', () => {
      // pageTourOffered is a latch for the life of the container. Setting it
      // around a start the gate refuses would mean this planner never gets the
      // tour on this page visit, even once help becomes visible to them.
      const visibility = TestBed.inject(HelpVisibilityService);
      const isVisible = jest.spyOn(visibility, 'isVisible', 'get').mockReturnValue(false);
      mockPlanningsService.getPlannings.mockReturnValue(
        of({ success: true, model: [{ siteId: 1, siteName: 'A' }] }) as any);

      component.getPlannings();
      jest.runAllTimers();
      expect(start).not.toHaveBeenCalled();

      isVisible.mockReturnValue(true);
      component.getPlannings();
      jest.runAllTimers();
      expect(start).toHaveBeenCalledWith('page', { isAdmin: false, isFirstUser: true });

      isVisible.mockRestore();
    });

    it('does not re-offer the tour on every reload', () => {
      mockPlanningsService.getPlannings.mockReturnValue(
        of({ success: true, model: [{ siteId: 1, siteName: 'A' }] }) as any);

      component.getPlannings();
      jest.runAllTimers();
      component.getPlannings();
      jest.runAllTimers();

      expect(start).toHaveBeenCalledTimes(1);
    });

    it('replays whichever tour the panel names, including the dialog one', () => {
      // start('dialog') is otherwise called from one place, gated on hasSeen, so
      // this is the only route back to the dialog tour once it has been skipped.
      component.replayTour('dialog');
      jest.runAllTimers();

      expect(start).toHaveBeenCalledWith('dialog', { isAdmin: false, isFirstUser: true });
    });

    it('does not offer a tour the planner has already seen', () => {
      tour.markSeen('page');
      mockPlanningsService.getPlannings.mockReturnValue(
        of({ success: true, model: [{ siteId: 1, siteName: 'A' }] }) as any);

      component.getPlannings();
      jest.runAllTimers();

      expect(start).not.toHaveBeenCalled();
    });
  });
  /**
   * Last week, so every visible day is in the past and a bulk target is never refused for
   * being today or later. Every date is derived from the clock rather than written down,
   * so no passing calendar date and no viewer timezone can turn these blocks red.
   */
  const lastWeekStart = startOfWeek(subDays(new Date(), 7), { weekStartsOn: 1 });
  const lastWeekEnd = endOfWeek(lastWeekStart, { weekStartsOn: 1 });
  const dayOf = (index: number) => addDays(lastWeekStart, index);
  const keyOf = (index: number) => format(dayOf(index), 'yyyy-MM-dd');
  /** A worker with a registration on each of the seven visible days. */
  const rowFor = (siteId: number, lockedThrough: string | null = null) => ({
    siteId,
    siteName: `Worker ${siteId}`,
    lockedThrough,
    planningPrDayModels: Array.from({ length: 7 }, (_, index) => ({
      id: siteId * 10 + index + 1,
      date: `${keyOf(index)}T00:00:00`,
    })),
  }) as any;

  /** Shows last week and loads these rows through the real load path. */
  const loadLastWeek = (...rows: any[]) => {
    component.dateFrom = lastWeekStart;
    component.dateTo = lastWeekEnd;
    mockPlanningsService.getPlannings.mockReturnValue(of({ success: true, model: rows }) as any);
    component.getPlannings();
  };

  describe('Bulk reconcile preview', () => {
    beforeEach(() => {
      loadLastWeek(rowFor(1), rowFor(2));
    });

    it('previews every visible worker when no row is ticked', () => {
      component.onReconcileDateChanged(dayOf(3));

      expect(component.reconcilePreview?.siteIds).toEqual([1, 2]);
      expect(component.reconcilePreview?.target).toBe(keyOf(3));
      expect(component.reconcilePreview?.landingBySiteId).toEqual({ 1: keyOf(3), 2: keyOf(3) });
      expect(component.reconcilePreview?.willReconcileCount).toBe(2);
    });

    it('previews only the ticked rows, never the rest of the grid', () => {
      component.onSelectionChanged([1]);
      component.onReconcileDateChanged(dayOf(3));

      expect(component.reconcilePreview?.siteIds).toEqual([1]);
      expect(component.reconcilePreview?.willReconcileCount).toBe(1);
    });

    it('shows a row already reconciled past the target as skipped, not moved backwards', () => {
      loadLastWeek(rowFor(1), rowFor(2, `${keyOf(5)}T00:00:00`));

      component.onReconcileDateChanged(dayOf(3));

      expect(component.reconcilePreview?.outcomeBySiteId).toEqual({ 1: 'lock', 2: 'skip' });
      expect(component.reconcilePreview?.willReconcileCount).toBe(1);
      expect(component.reconcilePreview?.skipCount).toBe(1);
      // Skipped rows are still sent, so the server reports them rather than the client
      // predicting them.
      expect(component.reconcilePreview?.siteIds).toEqual([1, 2]);
    });

    it('drops a target before the days on screen', () => {
      component.onReconcileDateChanged(subDays(lastWeekStart, 1));

      // The preview can only draw cells that are on screen, so an off-screen target is
      // dropped rather than committed blind.
      expect(component.reconcilePreview).toBeNull();
      expect(component.reconcileThroughDate).toBeNull();
    });

    it('drops a target at or after today, which can never be reconciled', () => {
      component.onReconcileDateChanged(new Date());

      expect(component.reconcilePreview).toBeNull();
      expect(component.reconcileThroughDate).toBeNull();
    });

    it('offers no target at all when the whole visible range is today or later', () => {
      component.dateFrom = new Date();
      component.dateTo = addDays(new Date(), 6);
      loadLastWeek(rowFor(1));

      // Both ends, or the field would advertise a minimum it will never accept.
      expect(component.reconcileMaxDate).toBeNull();
      expect(component.reconcileMinDate).toBeNull();
    });

    it('drops the preview when the week navigates away from the target', () => {
      component.onReconcileDateChanged(dayOf(3));
      expect(component.reconcilePreview).not.toBeNull();

      component.dateFrom = addDays(lastWeekStart, 7);
      component.dateTo = addDays(lastWeekEnd, 7);
      loadLastWeek(rowFor(1), rowFor(2));

      expect(component.reconcilePreview).toBeNull();
    });

    it('cancels, never widens, the preview when a reload drops the ticked rows', () => {
      component.onSelectionChanged([1]);
      component.onReconcileDateChanged(dayOf(3));
      expect(component.reconcilePreview?.siteIds).toEqual([1]);

      // A day save, the assigned-site dialog, Reload, a filter, the resigned toggle.
      component.getPlannings();

      expect(component.selectedSiteIds).toEqual([]);
      // Rebuilding here would fall back to "everyone visible" and put worker 2 in scope.
      expect(component.reconcilePreview).toBeNull();
    });

    it('cancels the preview when the grid drops the ticked rows by itself', () => {
      component.onSelectionChanged([1]);
      component.onReconcileDateChanged(dayOf(3));

      component.onSelectionReset();

      expect(component.selectedSiteIds).toEqual([]);
      expect(component.reconcilePreview).toBeNull();
    });

    it('redraws an unticked preview on the new rows, because that scope cannot widen', () => {
      component.onReconcileDateChanged(dayOf(3));

      loadLastWeek(rowFor(1), rowFor(2), rowFor(3));

      // Still "everyone visible", so the preview follows the rows instead of vanishing.
      expect(component.reconcilePreview?.siteIds).toEqual([1, 2, 3]);
    });

    it('ignores a reset the grid reports when nothing was ticked', () => {
      component.onReconcileDateChanged(dayOf(3));

      component.onSelectionReset();

      expect(component.reconcilePreview?.siteIds).toEqual([1, 2]);
    });

    it('returns to every visible worker when the last row is unticked by hand', () => {
      component.onSelectionChanged([1]);
      component.onReconcileDateChanged(dayOf(3));
      expect(component.reconcilePreview?.siteIds).toEqual([1]);

      component.onSelectionChanged([]);

      // The deliberate spec rule: the scope bar's count changes in plain sight.
      expect(component.reconcilePreview?.siteIds).toEqual([1, 2]);
    });

    it('names the target the way the scope bar reads it', () => {
      component.onReconcileDateChanged(dayOf(3));

      expect(component.reconcileTargetLabel).toBe(format(dayOf(3), 'dd.MM.yyyy'));
    });

    /**
     * The date-range controls change the day columns in the change-detection pass that
     * renders the scope bar, while their reload answers later. The grid reports its own
     * silent reset inside that same pass, so a scope still standing at that moment would
     * flip the bar from shown to gone after it had already been checked (NG0100). Each
     * handler therefore clears the scope before it touches the dates.
     */
    describe('clears the bulk scope before the reload answers', () => {
      const navigations: [string, () => void][] = [
        ['goForward', () => component.goForward()],
        ['goBackward', () => component.goBackward()],
        ['updateDateTo', () => component.updateDateTo({ value: addDays(lastWeekEnd, 7) } as any)],
      ];

      for (const [name, navigate] of navigations) {
        it(name, () => {
          component.onSelectionChanged([1]);
          component.onReconcileDateChanged(dayOf(3));
          expect(component.reconcilePreview).not.toBeNull();
          // A load that has NOT answered yet: this is the window the reset lands in.
          mockPlanningsService.getPlannings.mockReturnValue(new Subject<any>() as any);

          navigate();

          expect(component.selectedSiteIds).toEqual([]);
          expect(component.reconcilePreview).toBeNull();

          // The grid's reset now finds an empty scope and changes nothing, which is what
          // keeps the already-checked scope bar from moving.
          component.onSelectionReset();
          expect(component.reconcilePreview).toBeNull();
        });
      }
    });
  });

  describe('Reconcile through', () => {
    const resultModel = (overrides: any = {}) => ({
      landedOnBySiteId: { 1: `${keyOf(3)}T00:00:00` },
      applied: 1,
      skippedAlreadyFurtherForward: [2],
      skippedNoRegistration: [],
      alreadyReconciledSiteIds: [],
      ...overrides,
    });
    let toastr: any;
    let reloads: number;

    /** Worker 1 will be sealed on day 3; worker 2 is already further forward. */
    beforeEach(() => {
      toastr = TestBed.inject(ToastrService) as any;
      loadLastWeek(rowFor(1), rowFor(2, `${keyOf(5)}T00:00:00`));
      component.onReconcileDateChanged(addDays(lastWeekStart, 3));
      reloads = mockPlanningsService.getPlannings.mock.calls.length;
    });

    it('sends exactly the rows the preview drew, skipped ones included', () => {
      mockPlanningsService.reconcileThrough.mockReturnValue(
        of({ success: true, model: resultModel() }) as any);

      component.confirmReconcileThrough();

      expect(mockPlanningsService.reconcileThrough).toHaveBeenCalledWith(keyOf(3), [1, 2]);
    });

    it('toasts the counts the server decided, then clears the preview and reloads', () => {
      mockPlanningsService.reconcileThrough.mockReturnValue(
        of({ success: true, model: resultModel() }) as any);

      component.confirmReconcileThrough();

      // TranslateModule.forRoot() has no catalogue loaded, so instant() echoes the key.
      expect(toastr.success).toHaveBeenCalledWith('reconcileThroughResult');
      expect(component.reconcilePreview).toBeNull();
      expect(component.reconcileInFlight).toBe(false);
      expect(mockPlanningsService.getPlannings.mock.calls.length).toBe(reloads + 1);
    });

    it('reports the two skip reasons as separate figures, never merged', () => {
      const instant = jest.spyOn(TestBed.inject(TranslateService), 'instant');
      mockPlanningsService.reconcileThrough.mockReturnValue(of({
        success: true,
        model: resultModel({
          applied: 2,
          skippedAlreadyFurtherForward: [2, 5],
          skippedNoRegistration: [7, 8, 9],
        }),
      }) as any);

      component.confirmReconcileThrough();

      // "Already sealed further ahead" is benign; "no registration at all" means nothing
      // was sealed for those workers. One merged figure reads as the first and hides the
      // second, which is the thing the bulk action exists to surface.
      expect(instant).toHaveBeenCalledWith('reconcileThroughResult', { applied: 2, skipped: 2 });
      expect(instant).toHaveBeenCalledWith('reconcileThroughNoRegistration', { count: 3 });
      expect(instant).not.toHaveBeenCalledWith('reconcileThroughResult', { applied: 2, skipped: 5 });
      expect(toastr.success)
        .toHaveBeenCalledWith('reconcileThroughResult · reconcileThroughNoRegistration');
      instant.mockRestore();
    });

    it('leaves the no-registration figure out when there is none', () => {
      const instant = jest.spyOn(TestBed.inject(TranslateService), 'instant');
      mockPlanningsService.reconcileThrough.mockReturnValue(
        of({ success: true, model: resultModel() }) as any);

      component.confirmReconcileThrough();

      expect(instant).not.toHaveBeenCalledWith('reconcileThroughNoRegistration', expect.anything());
      expect(toastr.success).toHaveBeenCalledWith('reconcileThroughResult');
      instant.mockRestore();
    });

    it('says the state is uncertain when a success carries no counts', () => {
      // The rows were sealed; we simply cannot say how many. Saying nothing would leave a
      // commit with no acknowledgement at all.
      mockPlanningsService.reconcileThrough.mockReturnValue(
        of({ success: true, model: null }) as any);

      component.confirmReconcileThrough();

      expect(toastr.error).toHaveBeenCalledWith('lockRequestUncertain');
      expect(toastr.success).not.toHaveBeenCalled();
      expect(component.reconcilePreview).toBeNull();
      expect(mockPlanningsService.getPlannings.mock.calls.length).toBe(reloads + 1);
    });

    it('warns instead of celebrating when nothing was applied', () => {
      mockPlanningsService.reconcileThrough.mockReturnValue(
        of({ success: true, model: resultModel({ applied: 0, skippedNoRegistration: [1] }) }) as any);

      component.confirmReconcileThrough();

      // The whole message, so a regression to one merged "skipped" figure fails here
      // too: worker 1 has no registration, and that clause stays its own.
      expect(toastr.warning)
        .toHaveBeenCalledWith('reconcileThroughResult · reconcileThroughNoRegistration');
      expect(toastr.success).not.toHaveBeenCalled();
    });

    it('reports the workers that were already reconciled on the landing day', () => {
      mockPlanningsService.reconcileThrough.mockReturnValue(
        of({ success: true, model: resultModel({ alreadyReconciledSiteIds: [2] }) }) as any);

      component.confirmReconcileThrough();

      expect(toastr.success).toHaveBeenCalledWith('reconcileThroughResult · reconcileThroughUnchanged');
    });

    it('says nothing of its own when the server refused with a message', () => {
      // ApiBaseService has already toasted body.message.
      mockPlanningsService.reconcileThrough.mockReturnValue(
        of({ success: false, message: 'CannotReconcileTodayOrFuture' }) as any);

      component.confirmReconcileThrough();

      expect(toastr.error).not.toHaveBeenCalled();
      // A KNOWN refusal changed nothing, so the preview stays up to be retried.
      expect(component.reconcilePreview).not.toBeNull();
      expect(component.reconcileInFlight).toBe(false);
      expect(mockPlanningsService.getPlannings.mock.calls.length).toBe(reloads);
    });

    it('falls back to its own message when a refusal carries none', () => {
      mockPlanningsService.reconcileThrough.mockReturnValue(of({ success: false }) as any);

      component.confirmReconcileThrough();

      expect(toastr.error).toHaveBeenCalledWith('lockRequestFailed');
      expect(component.reconcilePreview).not.toBeNull();
      expect(mockPlanningsService.getPlannings.mock.calls.length).toBe(reloads);
    });

    it('says nothing of its own when an error reaches it, since the interceptor spoke', () => {
      // HttpErrorInterceptor toasts every 400 and rethrows an empty string.
      mockPlanningsService.reconcileThrough.mockReturnValue(throwError(() => '') as any);

      component.confirmReconcileThrough();

      expect(toastr.error).not.toHaveBeenCalled();
      expect(component.reconcileInFlight).toBe(false);
      expect(component.reconcilePreview).not.toBeNull();
      expect(mockPlanningsService.getPlannings.mock.calls.length).toBe(reloads);
    });

    it('treats a completion with no answer as uncertain, and reloads', () => {
      // The interceptor gives up with EMPTY after retrying against a 5xx or a dead
      // connection, and a 5xx can be written after the rows were already sealed.
      mockPlanningsService.reconcileThrough.mockReturnValue(EMPTY as any);

      component.confirmReconcileThrough();

      expect(toastr.error).toHaveBeenCalledWith('lockRequestUncertain');
      expect(toastr.success).not.toHaveBeenCalled();
      expect(component.reconcilePreview).toBeNull();
      expect(component.reconcileInFlight).toBe(false);
      expect(mockPlanningsService.getPlannings.mock.calls.length).toBe(reloads + 1);
    });

    it('stops waiting for a request that never answers, and reloads rather than claim failure', () => {
      jest.useFakeTimers();
      const tour = jest.spyOn(TestBed.inject(HelpTourService), 'start').mockImplementation(() => undefined);
      const request = new Subject<any>();
      mockPlanningsService.reconcileThrough.mockReturnValue(request as any);

      component.confirmReconcileThrough();
      expect(component.reconcileInFlight).toBe(true);

      jest.advanceTimersByTime(LOCK_REQUEST_TIMEOUT_MS + 1);

      expect(component.reconcileInFlight).toBe(false);
      expect(toastr.error).toHaveBeenCalledWith('lockRequestUncertain');
      expect(component.reconcilePreview).toBeNull();
      expect(mockPlanningsService.getPlannings.mock.calls.length).toBe(reloads + 1);

      // The abort is from this end only: a late answer must reach nobody.
      const after = mockPlanningsService.getPlannings.mock.calls.length;
      request.next({ success: true, model: resultModel() });
      request.complete();
      expect(toastr.success).not.toHaveBeenCalled();
      expect(mockPlanningsService.getPlannings.mock.calls.length).toBe(after);

      tour.mockRestore();
      jest.useRealTimers();
    });

    it('refuses to commit twice while a request is in flight', () => {
      const request = new Subject<any>();
      mockPlanningsService.reconcileThrough.mockReturnValue(request as any);

      component.confirmReconcileThrough();
      component.confirmReconcileThrough();

      expect(mockPlanningsService.reconcileThrough).toHaveBeenCalledTimes(1);
    });

    it('commits nothing when every row in the preview is skipped', () => {
      // Only worker 2, who is already further forward than the target.
      component.onSelectionChanged([2]);
      expect(component.reconcilePreview?.willReconcileCount).toBe(0);

      component.confirmReconcileThrough();

      expect(mockPlanningsService.reconcileThrough).not.toHaveBeenCalled();
    });

    it('commits nothing when there is no preview', () => {
      component.cancelReconcilePreview();

      component.confirmReconcileThrough();

      expect(mockPlanningsService.reconcileThrough).not.toHaveBeenCalled();
    });
  });

  /**
   * Only the first user may reconcile. Every case here is a pair: the same call as
   * someone else and as the first user, so nothing can pass because the setup was
   * simply too poor to produce a preview.
   */
  describe('the bulk reconcile belongs to the first user', () => {
    /** Loads the rows as the given user, since the load itself rebuilds the preview. */
    const loadAs = (isFirstUser: boolean) => {
      component.isFirstUser = isFirstUser;
      loadLastWeek(rowFor(1), rowFor(2));
    };

    it('previews nothing for anyone else, and everything for the first user', () => {
      loadAs(false);

      component.onReconcileDateChanged(dayOf(3));
      expect(component.reconcilePreview).toBeNull();

      loadAs(true);

      component.onReconcileDateChanged(dayOf(3));
      expect(component.reconcilePreview?.willReconcileCount).toBe(2);
    });

    it('takes a drawn preview down when the flag turns false under it', () => {
      // Otherwise the scope bar stays on screen with a Confirm button that silently
      // does nothing — a control disabled with no explanation, arrived at by accident.
      // The flag is not fixed for the life of the page: signing out resets it.
      const firstUser$ = new BehaviorSubject(true);
      mockStore.select.mockImplementation((selector: any) =>
        (selector === selectCurrentUserIsFirstUser ? firstUser$ : of('en-US')) as any);
      component = TestBed.createComponent(TimePlanningsContainerComponent).componentInstance;
      component.ngOnInit();
      loadAs(true);
      component.onReconcileDateChanged(dayOf(3));
      expect(component.reconcilePreview).not.toBeNull();

      firstUser$.next(false);

      expect(component.reconcilePreview).toBeNull();
      expect(component.reconcileThroughDate).toBeNull();
    });

    it('builds no preview for someone else who ticks rows either', () => {
      // The toolbar field is hidden, but a ticked row rebuilds the preview too.
      loadAs(false);
      component.onReconcileDateChanged(dayOf(3));

      component.onSelectionChanged([1]);

      expect(component.reconcilePreview).toBeNull();
    });

    it('commits nothing for anyone else, even with a preview already drawn', () => {
      // The preview is built as the first user and the flag then flips, which is the
      // only way anyone else can reach the commit at all: the scope bar holds it.
      loadAs(true);
      component.onReconcileDateChanged(dayOf(3));
      expect(component.reconcilePreview).not.toBeNull();
      mockPlanningsService.reconcileThrough.mockReturnValue(of({ success: true, model: null }) as any);

      component.isFirstUser = false;
      component.confirmReconcileThrough();

      expect(mockPlanningsService.reconcileThrough).not.toHaveBeenCalled();

      component.isFirstUser = true;
      component.confirmReconcileThrough();

      expect(mockPlanningsService.reconcileThrough).toHaveBeenCalledTimes(1);
    });

    it('hides the bulk target field rather than disabling it', () => {
      // Never rendered here (the bed has no Material), so the binding is read off the
      // file, as the day dialog's footer markup is. A disabled field would have to
      // explain itself, and no copy here may explain a restriction by appealing to
      // what some other user may do. Read for the RIGHT flag, too: isAdmin still gates
      // the payroll export button in this same toolbar.
      const template = readFileSync(
        join(__dirname, 'time-plannings-container.component.html'), 'utf8');
      const field = /<div[^>]*data-tp-help="toolbar\.reconcileThrough"[^>]*>/.exec(template);

      expect(field).not.toBeNull();
      expect(field![0]).toContain('*ngIf="isFirstUser"');
    });
  });
});

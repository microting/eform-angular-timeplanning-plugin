import { readFileSync } from 'fs';
import { join } from 'path';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { WorkdayEntityDialogComponent } from './workday-entity-dialog.component';
import { LOCK_REQUEST_TIMEOUT_MS } from '../../day-lock.util';
import { MAT_DIALOG_DATA, MatDialogRef } from '@angular/material/dialog';
import { FormBuilder, ReactiveFormsModule } from '@angular/forms';
import { TimePlanningPnPlanningsService, TimePlanningPnGpsCoordinatesService, TimePlanningPnPictureSnapshotsService } from '../../../../services';
import { TranslateService } from '@ngx-translate/core';
import { DatePipe, CommonModule } from '@angular/common';
import { EMPTY, of, Subject, throwError } from 'rxjs';
import { NO_ERRORS_SCHEMA } from '@angular/core';
import { TranslateModule } from '@ngx-translate/core';
import { Store } from '@ngrx/store';
import { provideMockStore } from '@ngrx/store/testing';
import { selectCurrentUserIsAdmin, selectCurrentUserIsFirstUser } from 'src/app/state';
import { DomSanitizer } from '@angular/platform-browser';
import { TemplateFilesService } from 'src/app/common/services';
import { HelpPanelService } from '../../../../help/services/help-panel.service';
import { HelpTourService, TOUR_STORAGE_KEY } from '../../../../help/services/help-tour.service';
import { ToastrService } from 'ngx-toastr';

describe('WorkdayEntityDialogComponent', () => {
  let component: WorkdayEntityDialogComponent;
  let fixture: ComponentFixture<WorkdayEntityDialogComponent>;
  let mockPlanningsService: jest.Mocked<TimePlanningPnPlanningsService>;
  let mockTranslateService: jest.Mocked<TranslateService>;
  let mockGpsCoordinatesService: jest.Mocked<TimePlanningPnGpsCoordinatesService>;
  let mockPictureSnapshotsService: jest.Mocked<TimePlanningPnPictureSnapshotsService>;
  let mockDomSanitizer: jest.Mocked<DomSanitizer>;
  let mockTemplateFilesService: jest.Mocked<TemplateFilesService>;
  let mockDialogRef: jest.Mocked<MatDialogRef<WorkdayEntityDialogComponent>>;
  let mockToastrService: jest.Mocked<ToastrService>;

  const mockData = {
    planningPrDayModels: {
      id: 1,
      date: new Date().toISOString(),
      planHours: 8,
      actualHours: 0,
      nettoHoursOverride: null,
      nettoHoursOverrideActive: false,
      paidOutFlex: 0,
      message: null,
      commentOffice: null,
      workerComment: null,
      sumFlexStart: 0,
      sumFlexEnd: 0,
      plannedStartOfShift1: 480, // 08:00
      plannedEndOfShift1: 1020, // 17:00
      plannedBreakOfShift1: 60, // 1 hour
      plannedStartOfShift2: 0,
      plannedEndOfShift2: 0,
      plannedBreakOfShift2: 0,
      plannedStartOfShift3: 0,
      plannedEndOfShift3: 0,
      plannedBreakOfShift3: 0,
      plannedStartOfShift4: 0,
      plannedEndOfShift4: 0,
      plannedBreakOfShift4: 0,
      plannedStartOfShift5: 0,
      plannedEndOfShift5: 0,
      plannedBreakOfShift5: 0,
      start1StartedAt: null,
      stop1StoppedAt: null,
      pause1Id: 0,
      start2StartedAt: null,
      stop2StoppedAt: null,
      pause2Id: 0,
      start3StartedAt: null,
      stop3StoppedAt: null,
      pause3Id: 0,
      start4StartedAt: null,
      stop4StoppedAt: null,
      pause4Id: 0,
      start5StartedAt: null,
      stop5StoppedAt: null,
      pause5Id: 0,
      start1Id: 0,
      stop1Id: 0,
      start2Id: 0,
      stop2Id: 0,
      start3Id: 0,
      stop3Id: 0,
      start4Id: 0,
      stop4Id: 0,
      start5Id: 0,
      stop5Id: 0,
      workDayStarted: false,
      workDayEnded: false
    },
    assignedSiteModel: {
      id: 1,
      siteId: 1,
      siteName: 'Test Site',
      useOnlyPlanHours: false,
      thirdShiftActive: false,
      fourthShiftActive: false,
      fifthShiftActive: false
    }
  };

  beforeEach(async () => {
    mockPlanningsService = {
      updatePlanning: jest.fn(),
      reconcileDay: jest.fn(),
      unreconcileDay: jest.fn(),
    } as any;
    mockTranslateService = {
      instant: jest.fn(),
      stream: jest.fn(),
      onLangChange: of({ lang: 'en' }),
    } as any;
    mockGpsCoordinatesService = {
      getByPlanRegistrationId: jest.fn().mockReturnValue(of({ success: false, model: null })),
    } as any;
    mockPictureSnapshotsService = {
      getByPlanRegistrationId: jest.fn().mockReturnValue(of({ success: false, model: null })),
    } as any;
    mockDomSanitizer = {
      bypassSecurityTrustResourceUrl: jest.fn().mockImplementation((url) => url),
    } as any;
    mockTemplateFilesService = {
      getImage: jest.fn().mockReturnValue(of(new Blob())),
    } as any;
    mockToastrService = {
      error: jest.fn(),
      success: jest.fn(),
    } as any;
    mockDialogRef = {
      close: jest.fn(),
      // The reconcile/unlock PUTs flip this while they are in flight, so it has to
      // start as the real MatDialogRef default rather than undefined.
      disableClose: false,
      _containerInstance: {
        _config: {
          width: '600px',
          height: 'auto'
        }
      }
    } as any;

    mockTranslateService.instant.mockReturnValue('Translated');
    mockTranslateService.stream.mockReturnValue(of('Translated'));

    await TestBed.configureTestingModule({
      declarations: [WorkdayEntityDialogComponent],
      imports: [CommonModule, ReactiveFormsModule, TranslateModule.forRoot()],
      schemas: [NO_ERRORS_SCHEMA],
      providers: [
        FormBuilder,
        DatePipe,
        // The bed has no auth slice, so the real projectors read `state.auth` as
        // undefined and throw on the first ngOnInit. That error reaches a subscribe()
        // with no error handler, so RxJS reports it on a timer — which a fake-timer
        // case then flushes into whatever test happens to be running. Overriding the
        // selectors the component actually asks for (a string key overrides nothing)
        // keeps the projectors from running at all.
        provideMockStore({
          initialState: {},
          selectors: [
            { selector: selectCurrentUserIsAdmin, value: false },
            { selector: selectCurrentUserIsFirstUser, value: false }
          ]
        }),
        { provide: MAT_DIALOG_DATA, useValue: mockData },
        { provide: MatDialogRef, useValue: mockDialogRef },
        { provide: TimePlanningPnPlanningsService, useValue: mockPlanningsService },
        { provide: TranslateService, useValue: mockTranslateService },
        { provide: TimePlanningPnGpsCoordinatesService, useValue: mockGpsCoordinatesService },
        { provide: TimePlanningPnPictureSnapshotsService, useValue: mockPictureSnapshotsService },
        { provide: DomSanitizer, useValue: mockDomSanitizer },
        { provide: TemplateFilesService, useValue: mockTemplateFilesService },
        { provide: ToastrService, useValue: mockToastrService }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(WorkdayEntityDialogComponent);
    component = fixture.componentInstance;
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  describe('Help wiring', () => {
    it('tells the panel it was opened from the dialog', async () => {
      // The panel is mounted once, on the page behind this dialog. Without the
      // surface its "Take the tour" button replays the PAGE tour, whose anchors
      // are all behind this dialog's backdrop.
      const panel = TestBed.inject(HelpPanelService);
      const surfaces: string[] = [];
      panel.surface$.subscribe(surface => surfaces.push(surface));

      component.openHelp('dayCell.save');

      expect(surfaces[surfaces.length - 1]).toBe('dialog');
    });

    it('offers the dialog tour with the real isAdmin, not a hardcoded false', () => {
      jest.useFakeTimers();
      localStorage.removeItem(TOUR_STORAGE_KEY);
      const tour = TestBed.inject(HelpTourService);
      const start = jest.spyOn(tour, 'start').mockImplementation(() => undefined);

      component.isAdmin = true;
      (component as any).startDialogTourOnce();
      jest.runAllTimers();

      expect(start).toHaveBeenCalledWith('dialog', { isAdmin: true });

      start.mockRestore();
      jest.useRealTimers();
    });
  });

  describe('Time Conversion Utilities', () => {
    describe('convertMinutesToTime', () => {
      it('should return null for zero minutes', () => {
        expect(component.convertMinutesToTime(0)).toBeNull();
      });

      it('should return null for null input', () => {
        expect(component.convertMinutesToTime(null)).toBeNull();
      });

      it('should return null for undefined input', () => {
        expect(component.convertMinutesToTime(undefined)).toBeNull();
      });

      it('should convert minutes to time format HH:MM', () => {
        expect(component.convertMinutesToTime(60)).toBe('01:00');
        expect(component.convertMinutesToTime(90)).toBe('01:30');
        expect(component.convertMinutesToTime(480)).toBe('08:00'); // 8 hours
        expect(component.convertMinutesToTime(1020)).toBe('17:00'); // 17 hours
      });

      it('should handle minutes with remainders correctly', () => {
        expect(component.convertMinutesToTime(125)).toBe('02:05'); // 2 hours 5 minutes
        expect(component.convertMinutesToTime(517)).toBe('08:37'); // 8 hours 37 minutes
      });

      it('should pad single digit hours and minutes with zeros', () => {
        expect(component.convertMinutesToTime(5)).toBe('00:05');
        expect(component.convertMinutesToTime(65)).toBe('01:05');
      });
    });

    describe('padZero', () => {
      it('should pad single digit numbers with zero', () => {
        expect(component.padZero(0)).toBe('00');
        expect(component.padZero(5)).toBe('05');
        expect(component.padZero(9)).toBe('09');
      });

      it('should not pad double digit numbers', () => {
        expect(component.padZero(10)).toBe('10');
        expect(component.padZero(59)).toBe('59');
        expect(component.padZero(99)).toBe('99');
      });
    });

    describe('getMinutes', () => {
      it('should convert time string to minutes', () => {
        expect(component.getMinutes('00:00')).toBe(0);
        expect(component.getMinutes('01:00')).toBe(60);
        expect(component.getMinutes('01:30')).toBe(90);
        expect(component.getMinutes('08:00')).toBe(480);
        expect(component.getMinutes('17:00')).toBe(1020);
      });

      it('should return 0 for null or empty input', () => {
        expect(component.getMinutes(null)).toBe(0);
        expect(component.getMinutes('')).toBe(0);
      });

      it('should return 0 for invalid time format', () => {
        expect(component.getMinutes('invalid')).toBe(0);
        expect(component.getMinutes('25:00')).toBe(0); // Invalid hour
        expect(component.getMinutes('12:60')).toBe(0); // Invalid minute
      });

      it('should handle edge cases correctly', () => {
        expect(component.getMinutes('00:01')).toBe(1);
        expect(component.getMinutes('23:59')).toBe(1439);
      });
    });

    describe('convertTimeToMinutes', () => {
      it('should convert time to minutes', () => {
        expect(component.convertTimeToMinutes('00:00')).toBe(0);
        expect(component.convertTimeToMinutes('01:00')).toBe(60);
        expect(component.convertTimeToMinutes('08:30')).toBe(510);
      });

      it('should return null for empty or null input', () => {
        expect(component.convertTimeToMinutes('')).toBeNull();
        expect(component.convertTimeToMinutes(null)).toBeNull();
      });

      it('should handle 5-minute intervals when isFiveNumberIntervals is true', () => {
        expect(component.convertTimeToMinutes('01:00', true)).toBe(13); // (60/5) + 1
        expect(component.convertTimeToMinutes('00:30', true)).toBe(7); // (30/5) + 1
      });

      it('should handle stop time at midnight with 5-minute intervals', () => {
        expect(component.convertTimeToMinutes('00:00', true, true)).toBe(289); // Special case for stop at midnight
      });
    });

    describe('convertHoursToTime', () => {
      it('should convert hours to time format', () => {
        expect(component.convertHoursToTime(0)).toBe('00:00');
        expect(component.convertHoursToTime(1)).toBe('01:00');
        expect(component.convertHoursToTime(1.5)).toBe('01:30');
        expect(component.convertHoursToTime(8.25)).toBe('08:15');
      });

      it('should handle negative hours correctly', () => {
        expect(component.convertHoursToTime(-1.5)).toBe('-1:30');
        expect(component.convertHoursToTime(-0.25)).toBe('-0:15');
      });

      it('should round minutes correctly', () => {
        expect(component.convertHoursToTime(1.016666666)).toBe('01:01'); // 1 hour and ~1 minute
      });
    });
  });

  describe('Shift Duration Calculations', () => {
    describe('getMaxDifference', () => {
      it('should calculate difference between start and end times', () => {
        expect(component.getMaxDifference('08:00', '17:00')).toBe('9:0');
        expect(component.getMaxDifference('08:00', '12:00')).toBe('4:0');
      });

      it('should handle midnight crossing correctly', () => {
        const result = component.getMaxDifference('22:00', '00:00');
        expect(result).toBe('2:0'); // 2 hours to midnight
      });

      it('should return 0:0 for invalid inputs', () => {
        expect(component.getMaxDifference('', '')).toBe('0:0');
      });

      it('should handle times with minutes', () => {
        expect(component.getMaxDifference('08:30', '17:45')).toBe('9:15');
      });
    });
  });

  describe('Form Initialization', () => {
    it('should initialize workday form with correct structure', () => {
      component.ngOnInit();

      expect(component.workdayForm).toBeDefined();
      expect(component.workdayForm.get('planned')).toBeDefined();
      expect(component.workdayForm.get('actual')).toBeDefined();
      expect(component.workdayForm.get('planHours')).toBeDefined();
    });

    it('should create shift forms for all 5 shifts', () => {
      component.ngOnInit();

      for (let i = 1; i <= 5; i++) {
        expect(component.workdayForm.get(`planned.shift${i}`)).toBeDefined();
        expect(component.workdayForm.get(`actual.shift${i}`)).toBeDefined();
      }
    });

    it('should populate form with initial data values', () => {
      component.ngOnInit();

      const plannedShift1 = component.workdayForm.get('planned.shift1');
      expect(plannedShift1?.get('start')?.value).toBe('08:00');
      expect(plannedShift1?.get('stop')?.value).toBe('17:00');
      expect(plannedShift1?.get('break')?.value).toBe('01:00');
    });

    it('should set isInTheFuture correctly for future dates', () => {
      const futureDate = new Date();
      futureDate.setDate(futureDate.getDate() + 5);
      component.data.planningPrDayModels.date = futureDate.toISOString();

      component.ngOnInit();

      expect(component.isInTheFuture).toBe(true);
    });

    it('should set isInTheFuture correctly for past dates', () => {
      const pastDate = new Date();
      pastDate.setDate(pastDate.getDate() - 5);
      component.data.planningPrDayModels.date = pastDate.toISOString();

      component.ngOnInit();

      expect(component.isInTheFuture).toBe(false);
    });
  });

  describe('Date Time Conversion', () => {
    it('should convert time to datetime of today', () => {
      component.ngOnInit();

      const result = component.convertTimeToDateTimeOfToday('08:00');

      expect(result).toBeTruthy();
      expect(result).toContain('08:00:00');
    });

    it('should return null for empty time', () => {
      const result = component.convertTimeToDateTimeOfToday('');

      expect(result).toBeNull();
    });

    it('should return null for null time', () => {
      const result = component.convertTimeToDateTimeOfToday(null);

      expect(result).toBeNull();
    });
  });

  describe('Flex Calculation', () => {
    it('should calculate todays flex as difference between actual and plan hours', () => {
      component.data.planningPrDayModels.actualHours = 9;
      component.data.planningPrDayModels.planHours = 8;

      component.ngOnInit();

      expect(component.todaysFlex).toBe(1);
    });
  });

  describe('Pause override (Approach C) save wiring', () => {
    // `mockData` is a single module-level object shared (by reference) across
    // every test via MAT_DIALOG_DATA. Tests in this block write the pause
    // override fields on `planningPrDayModels`, and the outer `beforeEach`
    // never deep-resets that object — so without this local reset a sibling
    // test could leave the override/Specified fields dirty and poison the next
    // one (the historical order-dependent CI failures). Reset every field this
    // block touches to a known-clean baseline before each test so every test is
    // self-contained and order-independent.
    beforeEach(() => {
      const m = component.data.planningPrDayModels as any;
      for (let shift = 1; shift <= 5; shift++) {
        m[`pause${shift}OverrideMinutes`] = null;
        m[`pause${shift}OverrideMinutesSpecified`] = false;
      }
      m.clearPauseOverrides = false;
    });

    it('sets pause1OverrideMinutes + Specified when the pause field changes', () => {
      // Drive the save-wiring unit directly with an explicit zero baseline so a
      // 45-min pause is an unambiguous change. This bypasses the form-group →
      // value plumbing in onUpdateWorkDayEntity (which can yield undefined pause
      // values under jsdom) and tests applyPauseOverrideForShift's change
      // detection deterministically, free of the shared fixture.
      const m = component.data.planningPrDayModels as any;
      component.ngOnInit();

      // current (45) !== loaded (0) → genuine change.
      (component as any).loadedPauseMinutes = { 1: 0, 2: 0, 3: 0, 4: 0, 5: 0 };
      (component as any).applyPauseOverrideForShift(1, '00:45');

      expect(m.pause1OverrideMinutesSpecified).toBe(true);
      expect(m.pause1OverrideMinutes).toBe(45);
    });

    it('leaves Specified=false when the pause field is unchanged', () => {
      // current === loaded → no override written. Drive the unit directly with
      // an explicit baseline so this never depends on the shared model's state
      // or on jsdom form-value extraction.
      const m = component.data.planningPrDayModels as any;
      component.ngOnInit();

      (component as any).loadedPauseMinutes = { 1: 0, 2: 0, 3: 0, 4: 0, 5: 0 };
      // pauseOverrideCleared[1] is false (reset by ngOnInit), current = 0 = loaded.
      (component as any).applyPauseOverrideForShift(1, '00:00');

      expect(m.pause1OverrideMinutesSpecified).toBe(false);
    });

    it('clear affordance signals revert-to-recorded (Specified=true, override=null)', () => {
      // resetPauseToRecorded marks shift 1 cleared and resets its picker to the
      // recorded sum (0 here → null hh:mm). As long as the picker still shows
      // that value, applyPauseOverrideForShift must emit Specified=true with a
      // null override. Drive it directly with the cleared value to avoid the
      // form-extraction path in onUpdateWorkDayEntity.
      const m = component.data.planningPrDayModels as any;
      component.ngOnInit();

      component.resetPauseToRecorded(1);
      const clearedMinutes = (component as any).pauseOverrideClearedMinutes[1];
      // clearedMinutes is the raw minutes the picker was reset to (null when 0).
      const clearedHhmm = component.convertMinutesToTime(clearedMinutes);
      (component as any).applyPauseOverrideForShift(1, clearedHhmm);

      expect(m.pause1OverrideMinutesSpecified).toBe(true);
      expect(m.pause1OverrideMinutes).toBeNull();
    });

    it('prefers the served override for the displayed pause value', () => {
      // Display precedence: a served override projects onto the pause picker.
      (component.data.planningPrDayModels as any).pause1OverrideMinutes = 30;

      component.ngOnInit();

      expect(component.workdayForm.get('actual.shift1.pause')?.value).toBe('00:30');
    });
  });

  describe('Flag Change Handling', () => {
    it('should turn off other flags when one is turned on', () => {
      component.ngOnInit();

      const flags = component.workdayForm.get('flags');

      // Simulate turning on a flag (if flags exist)
      if (flags && Object.keys((flags as any).controls).length > 0) {
        const firstKey = Object.keys((flags as any).controls)[0];
        component.onFlagChange(firstKey);

        // Verify only one flag is true
        let trueCount = 0;
        Object.keys((flags as any).controls).forEach(key => {
          if (flags.get(key)?.value === true) {
            trueCount++;
          }
        });

        expect(trueCount).toBeLessThanOrEqual(1);
      }
    });
  });


  describe('Reconciled day lock', () => {
    /**
     * A date `offset` days from today, in the shape the server sends: local calendar
     * midnight with no offset. Computed rather than hardcoded so "yesterday" stays
     * yesterday, and offset-free so no viewer timezone can shift the calendar day.
     */
    const dayString = (offset: number): string => {
      const d = new Date();
      d.setHours(0, 0, 0, 0);
      d.setDate(d.getDate() + offset);
      const pad = (n: number) => String(n).padStart(2, '0');
      return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}T00:00:00`;
    };

    /**
     * `mockData` is one module-level object shared by reference through
     * MAT_DIALOG_DATA, and the outer beforeEach never deep-resets it. Snapshot
     * whatever the describes before this one left behind, and put it back afterwards,
     * so this block is order-independent in both directions.
     */
    let dataSnapshot: string;
    beforeAll(() => {
      dataSnapshot = JSON.stringify(mockData);
    });
    afterAll(() => {
      const restored = JSON.parse(dataSnapshot);
      for (const key of Object.keys(mockData)) {
        delete (mockData as any)[key];
      }
      Object.assign(mockData, restored);
    });

    /** An open, past day: reconcilable, nothing locked. */
    const openPastDay = () => {
      const m = component.data.planningPrDayModels as any;
      m.date = dayString(-1);
      m.id = 1;
      m.siteName = 'Test Worker';
      m.reconciled = false;
      m.reconciledAt = null;
      component.data.isLocked = false;
      component.data.isBoundary = false;
      component.data.isSealed = false;
      component.data.lockedThrough = null;
    };

    /** The boundary: locked, sealed, and the row's lockedThrough. */
    const boundaryDay = () => {
      openPastDay();
      const m = component.data.planningPrDayModels as any;
      m.reconciled = true;
      m.reconciledAt = '2026-09-14T10:32:11';
      component.data.isLocked = true;
      component.data.isBoundary = true;
      component.data.isSealed = true;
      component.data.lockedThrough = m.date;
    };

    /** Opens the dialog on the day just configured and arms the reconcile confirm. */
    const armReconcile = () => {
      component.ngOnInit();
      component.onReconcileStart();
    };

    /** Opens the dialog on the day just configured and arms the unlock confirm. */
    const armUnlock = () => {
      component.ngOnInit();
      component.onUnlockStart();
    };

    beforeEach(() => {
      openPastDay();
      mockPlanningsService.reconcileDay.mockReturnValue(of({ success: true, message: '' } as any));
      mockPlanningsService.unreconcileDay.mockReturnValue(of({ success: true, message: '' } as any));
      mockTranslateService.instant.mockImplementation((key: string, params?: any) => {
        if (key === 'UNLOCK') {
          return 'LÅS OP';
        }
        if (key === 'reconciledProvenance') {
          return `Afstemt ${params.date} kl. ${params.time}`;
        }
        return key;
      });
    });

    describe('a locked day opens read-only', () => {
      it('disables the whole form, and the cascade cannot reopen it', () => {
        component.data.isLocked = true;

        component.ngOnInit();

        expect(component.isLocked).toBe(true);
        expect(component.workdayForm.disabled).toBe(true);
        // updateDisabledStates() runs again on every field change, and its shift-1
        // branch enables this control unconditionally once a start time is set.
        (component as any).updateDisabledStates();
        expect(component.workdayForm.get('planned.shift1.stop')?.disabled).toBe(true);
        expect(component.workdayForm.get('commentOffice')?.disabled).toBe(true);
      });

      it('leaves the same control enabled on an open day', () => {
        // The negative control: without it the assertion above could pass on a
        // control the cascade never enables in the first place.
        component.ngOnInit();
        (component as any).updateDisabledStates();

        expect(component.workdayForm.get('planned.shift1.stop')?.enabled).toBe(true);
      });

      it('keeps the unlock word typeable, because that control is not in the form', () => {
        component.data.isLocked = true;

        component.ngOnInit();

        expect(component.unlockWordCtrl.enabled).toBe(true);
      });

      it('ignores the reset affordances, which write through a disabled control', () => {
        // patchValue/setValue do not respect AbstractControl.disabled, so without the
        // handler guards a click on the delete icons blanked a sealed day on screen.
        component.data.isLocked = true;
        component.ngOnInit();
        const before = JSON.stringify(component.workdayForm.getRawValue());

        component.resetPlannedTimes(1);
        component.resetActualTimes(1);
        component.resetPauseToRecorded(1);

        expect(JSON.stringify(component.workdayForm.getRawValue())).toBe(before);
        expect(component.actualTimesReadOnly).toBe(true);
      });

      it('still resets an open day, and still refuses the registered times of a future one', () => {
        // The negative control for the guard above, and the pre-existing hole it
        // also closes: a future day's registered times are built disabled, and the
        // reset wrote through that too.
        component.ngOnInit();
        component.resetPlannedTimes(1);
        expect(component.workdayForm.get('planned.shift1.start')?.value).toBeNull();

        component.data.planningPrDayModels.date = dayString(3);
        component.ngOnInit();
        expect(component.isInTheFuture).toBe(true);
        expect(component.actualTimesReadOnly).toBe(true);
        const before = JSON.stringify(component.workdayForm.getRawValue());
        component.resetActualTimes(1);
        component.resetPauseToRecorded(1);
        expect(JSON.stringify(component.workdayForm.getRawValue())).toBe(before);
      });
    });

    describe('the two-step reconcile confirm', () => {
      it('arms on the first click and sends nothing', () => {
        component.ngOnInit();
        expect(component.canReconcile).toBe(true);
        expect(component.reconcileEligible).toBe(true);

        component.onReconcileStart();

        expect(component.footerMode).toBe('confirmReconcile');
        expect(mockPlanningsService.reconcileDay).not.toHaveBeenCalled();
        expect(component.lockStateChanged).toBe(false);
      });

      it('commits on the second click and turns the open dialog read-only', () => {
        armReconcile();

        component.onReconcileConfirm();

        expect(mockPlanningsService.reconcileDay).toHaveBeenCalledTimes(1);
        expect(mockPlanningsService.reconcileDay).toHaveBeenCalledWith(1);
        expect(component.isLocked).toBe(true);
        expect(component.data.isLocked).toBe(true);
        expect(component.workdayForm.disabled).toBe(true);
        expect(component.data.isSealed).toBe(true);
        expect(component.data.isBoundary).toBe(true);
        expect(component.data.lockedThrough).toBe(component.data.planningPrDayModels.date);
        expect(component.data.planningPrDayModels.reconciled).toBe(true);
        expect(component.data.planningPrDayModels.reconciledAt)
          .toMatch(/^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}$/);
        expect(component.lockStateChanged).toBe(true);
        expect(component.footerMode).toBe('actions');
        // The dialog stays open: the same one turns read-only.
        expect(mockDialogRef.close).not.toHaveBeenCalled();
      });

      it('cancelling the confirm commits nothing and leaves the day open', () => {
        armReconcile();

        component.onReconcileCancel();

        expect(component.footerMode).toBe('actions');
        expect(mockPlanningsService.reconcileDay).not.toHaveBeenCalled();
        expect(component.isLocked).toBe(false);
        expect(component.lockStateChanged).toBe(false);
      });

      it('refuses to arm, and refuses to commit, while the form is dirty', () => {
        // Reconcile writes only the flag, so unsaved edits would be frozen behind
        // the seal and then lost.
        component.ngOnInit();
        component.workdayForm.markAsDirty();

        component.onReconcileStart();
        expect(component.footerMode).toBe('actions');

        // Armed on a clean form, then edited while the confirm was showing.
        component.workdayForm.markAsPristine();
        component.onReconcileStart();
        expect(component.footerMode).toBe('confirmReconcile');
        component.workdayForm.markAsDirty();

        component.onReconcileConfirm();

        expect(mockPlanningsService.reconcileDay).not.toHaveBeenCalled();
        expect(component.footerMode).toBe('actions');
      });

      it('never sends a reconcile for a locked day, for today or for the future', () => {
        // Each case arms the confirm first, so that footerMode returning to 'actions'
        // is a real transition and not just its initial value.
        for (const setUp of [
          () => { openPastDay(); component.data.isLocked = true; },
          () => { openPastDay(); component.data.planningPrDayModels.date = dayString(0); },
          () => { openPastDay(); component.data.planningPrDayModels.date = dayString(3); },
        ]) {
          setUp();
          component.ngOnInit();
          expect(component.reconcileEligible).toBe(false);

          // Even reached directly — an armed confirm whose day changed underneath.
          component.footerMode = 'confirmReconcile';
          component.onReconcileConfirm();

          expect(component.footerMode).toBe('actions');
          expect(mockPlanningsService.reconcileDay).not.toHaveBeenCalled();
        }
      });

      it('blocks dismissal while the PUT is in flight, and ignores a second click', () => {
        const put = new Subject<any>();
        mockPlanningsService.reconcileDay.mockReturnValue(put.asObservable());
        armReconcile();

        component.onReconcileConfirm();

        expect(component.lockRequestInFlight).toBe(true);
        expect(mockDialogRef.disableClose).toBe(true);
        component.onReconcileConfirm();
        expect(mockPlanningsService.reconcileDay).toHaveBeenCalledTimes(1);

        put.next({ success: true, message: '' });
        put.complete();

        // lockStateChanged is set before the close, and the guard lifts with it.
        expect(component.lockStateChanged).toBe(true);
        expect(component.lockRequestInFlight).toBe(false);
        expect(mockDialogRef.disableClose).toBe(false);
      });

      it('leaves the day open when the server refuses, and says so', () => {
        mockPlanningsService.reconcileDay
          .mockReturnValue(of({ success: false, message: '' } as any));
        armReconcile();

        component.onReconcileConfirm();

        expect(component.isLocked).toBe(false);
        // KNOWN: the server itself answered "no", so there is nothing to re-read.
        // This is what separates a message-less refusal from an empty completion.
        expect(component.lockStateChanged).toBe(false);
        expect(component.footerMode).toBe('actions');
        expect(component.lockRequestInFlight).toBe(false);
        expect(mockDialogRef.disableClose).toBe(false);
        // ApiBaseService toasts body.message, but only when there is one.
        expect(mockToastrService.error).toHaveBeenCalledWith('lockRequestFailed');
      });

      it('says nothing of its own when someone else already spoke', () => {
        // A refusal that carries a message: ApiBaseService toasts it.
        mockPlanningsService.reconcileDay
          .mockReturnValue(of({ success: false, message: 'CannotReconcileTodayOrFuture' } as any));
        armReconcile();
        component.onReconcileConfirm();
        expect(mockToastrService.error).not.toHaveBeenCalled();

        // And an error that reached us: the only one HttpErrorInterceptor lets
        // through is its 400 branch, which has already toasted every errorMessage.
        mockPlanningsService.reconcileDay.mockReturnValue(throwError(() => ''));
        armReconcile();
        component.onReconcileConfirm();
        expect(mockToastrService.error).not.toHaveBeenCalled();
        // Recovered all the same, and KNOWN: the server rejected the request before
        // doing anything, so no reload is needed.
        expect(component.lockRequestInFlight).toBe(false);
        expect(mockDialogRef.disableClose).toBe(false);
        expect(component.footerMode).toBe('actions');
        expect(component.lockStateChanged).toBe(false);
      });

      it('treats a completion with no answer as unknown too', () => {
        // HttpErrorInterceptor turns a sustained 5xx, and an offline network, into
        // EMPTY after its retries: complete, with no value and no error. Left
        // unhandled this strands the user — the backdrop is blocked, both confirm
        // buttons are disabled, and neither carries mat-dialog-close. And a 5xx can
        // be written after the save already committed, so a response arriving is not
        // evidence that nothing changed.
        mockPlanningsService.reconcileDay.mockReturnValue(EMPTY);
        armReconcile();

        component.onReconcileConfirm();

        expect(component.lockRequestInFlight).toBe(false);
        expect(mockDialogRef.disableClose).toBe(false);
        expect(component.footerMode).toBe('actions');
        expect(mockToastrService.error).toHaveBeenCalledWith('lockRequestUncertain');
        // Not sealed in place off an answer we never got — the reload decides.
        expect(component.isLocked).toBe(false);
        expect(component.lockStateChanged).toBe(true);
      });

      it('treats its own timeout as unknown, not failed, because the answer may be late', () => {
        // The belt to the EMPTY braces: a request that neither emits nor completes
        // inside a window shorter than the interceptor's own ~75s retry budget.
        // Timing out aborts the request from this end, but the server may commit a
        // moment later, so the day's state is UNKNOWN rather than unchanged.
        const tour = TestBed.inject(HelpTourService);
        const start = jest.spyOn(tour, 'start').mockImplementation(() => undefined);
        jest.useFakeTimers();
        const put = new Subject<any>();
        mockPlanningsService.reconcileDay.mockReturnValue(put.asObservable());
        armReconcile();

        component.onReconcileConfirm();
        expect(component.lockRequestInFlight).toBe(true);
        expect(mockDialogRef.disableClose).toBe(true);

        jest.advanceTimersByTime(LOCK_REQUEST_TIMEOUT_MS + 1);

        // Settled and usable again...
        expect(component.lockRequestInFlight).toBe(false);
        expect(mockDialogRef.disableClose).toBe(false);
        expect(component.footerMode).toBe('actions');
        expect(mockToastrService.error).toHaveBeenCalledWith('lockRequestUncertain');
        // ...and the close must reload the grid, or it would go on drawing a day the
        // server may have sealed, which the user would then act against.
        expect(component.lockStateChanged).toBe(true);

        // The late 200: timeout has unsubscribed, so it reaches nobody. The dialog
        // must not seal itself in place off a stale answer — the reload decides.
        put.next({ success: true, message: '' });
        put.complete();

        expect(component.isLocked).toBe(false);
        expect(component.data.isSealed).toBe(false);
        expect(component.lockStateChanged).toBe(true);
        expect(mockDialogRef.close).not.toHaveBeenCalled();

        jest.useRealTimers();
        start.mockRestore();
      });

      it('leaves the unlock confirm usable when its request strands', () => {
        // The same recovery on the other verb: the word stays typed, and Cancel works.
        mockPlanningsService.unreconcileDay.mockReturnValue(EMPTY);
        boundaryDay();
        armUnlock();
        component.unlockWordCtrl.setValue('LÅS OP');

        component.onUnlockConfirm();

        expect(component.lockRequestInFlight).toBe(false);
        expect(mockDialogRef.disableClose).toBe(false);
        expect(component.footerMode).toBe('confirmUnlock');
        expect(mockToastrService.error).toHaveBeenCalledWith('lockRequestUncertain');
        expect(mockDialogRef.close).not.toHaveBeenCalled();
        // Unknown, so closing this dialog still reloads the grid.
        expect(component.lockStateChanged).toBe(true);
      });
    });

    describe('the typed-word unlock', () => {
      it('sends nothing until the word matches, and ignores case and spacing', () => {
        boundaryDay();
        component.ngOnInit();
        expect(component.isBoundaryDay).toBe(true);

        component.onUnlockStart();
        expect(component.footerMode).toBe('confirmUnlock');
        expect(component.unlockWordCtrl.value).toBe('');
        expect(component.unlockWordMatches).toBe(false);
        component.onUnlockConfirm();
        expect(mockPlanningsService.unreconcileDay).not.toHaveBeenCalled();

        // Half the word is not the word.
        component.unlockWordCtrl.setValue('LÅS');
        expect(component.unlockWordMatches).toBe(false);
        component.onUnlockConfirm();
        expect(mockPlanningsService.unreconcileDay).not.toHaveBeenCalled();

        // The friction is the word, not the Shift key or the spacing.
        component.unlockWordCtrl.setValue('  lås   op ');
        expect(component.unlockWordMatches).toBe(true);

        component.onUnlockConfirm();

        expect(mockPlanningsService.unreconcileDay).toHaveBeenCalledTimes(1);
        expect(mockPlanningsService.unreconcileDay).toHaveBeenCalledWith(1);
        // The form was built locked, so the only way back to a clean cascade is to
        // close and reopen from a reloaded grid.
        expect(component.lockStateChanged).toBe(true);
        expect(mockDialogRef.close).toHaveBeenCalled();
      });

      it('lifts the dismissal guard before it closes, not after', () => {
        // Closing while disableClose is still true is how a dialog gets stuck.
        boundaryDay();
        armUnlock();
        component.unlockWordCtrl.setValue('LÅS OP');
        let disableCloseAtClose: boolean | undefined;
        (mockDialogRef.close as jest.Mock)
          .mockImplementation(() => { disableCloseAtClose = mockDialogRef.disableClose; });

        component.onUnlockConfirm();

        expect(disableCloseAtClose).toBe(false);
      });

      it('backing out clears the word and keeps the day sealed', () => {
        boundaryDay();
        armUnlock();
        component.unlockWordCtrl.setValue('LÅS OP');

        component.onUnlockCancel();

        expect(component.footerMode).toBe('actions');
        expect(component.unlockWordCtrl.value).toBe('');
        expect(mockPlanningsService.unreconcileDay).not.toHaveBeenCalled();
        expect(component.isLocked).toBe(true);
        expect(component.data.isSealed).toBe(true);
        expect(mockDialogRef.close).not.toHaveBeenCalled();
      });

      it('keeps the day sealed when the server refuses', () => {
        mockPlanningsService.unreconcileDay
          .mockReturnValue(of({ success: false, message: 'OnlyLatestReconciledDayCanBeUnlocked' } as any));
        boundaryDay();
        armUnlock();
        component.unlockWordCtrl.setValue('LÅS OP');

        component.onUnlockConfirm();

        expect(component.lockStateChanged).toBe(false);
        expect(mockDialogRef.close).not.toHaveBeenCalled();
        // Still in the confirm, so the word need not be typed again.
        expect(component.footerMode).toBe('confirmUnlock');
        expect(component.lockRequestInFlight).toBe(false);
      });

      it('is not offered on a locked day that is not the boundary', () => {
        // The lock is derived: every day at or before lockedThrough is locked, so
        // unlocking a lower day would need an editable day under a sealed one.
        boundaryDay();
        component.data.isBoundary = false;
        component.data.lockedThrough = dayString(0);
        component.ngOnInit();

        expect(component.isBoundaryDay).toBe(false);

        component.onUnlockStart();
        expect(component.footerMode).toBe('actions');

        // And the request is refused even with the word in the control.
        component.unlockWordCtrl.setValue('LÅS OP');
        component.footerMode = 'confirmUnlock';
        component.onUnlockConfirm();
        expect(mockPlanningsService.unreconcileDay).not.toHaveBeenCalled();
      });

      it('names the day to free first on every day but the boundary', () => {
        boundaryDay();
        component.data.isBoundary = false;
        component.data.lockedThrough = '2026-09-14T00:00:00';
        component.ngOnInit();

        expect(component.freeFirstDate).toBe('14.09.2026');
      });
    });

    describe('the provenance line', () => {
      it('reads the stored timestamp on a sealed day', () => {
        boundaryDay();
        component.ngOnInit();

        expect(component.data.isSealed).toBe(true);
        expect(component.reconciledProvenance).toBe('Afstemt 14.09.2026 kl. 10:32');
      });

      it('falls back to the bare word when a sealed day carries no timestamp', () => {
        boundaryDay();
        component.data.planningPrDayModels.reconciledAt = null;
        component.ngOnInit();

        expect(component.reconciledProvenance).toBe('Reconciled');
      });

      it('labels the day the confirm sentence names', () => {
        component.data.planningPrDayModels.date = '2026-09-14T00:00:00';
        component.ngOnInit();

        expect(component.dayLabel).toBe('14.09.2026');
      });
    });

    describe('the footer markup', () => {
      // This suite never renders the template (the TranslateService here is a mock),
      // so the structural invariants of the footer are read off the file. The
      // Playwright specs in shard `s` exercise the rendered version.
      const template = readFileSync(
        join(__dirname, 'workday-entity-dialog.component.html'), 'utf8');

      const buttonWithId = (id: string): string => {
        const match = new RegExp(`<button[^>]*\\bid="${id}"[^>]*>`).exec(template);
        expect(match).not.toBeNull();
        return (match as RegExpExecArray)[0];
      };

      it('removes Save from the DOM on a locked day rather than disabling it', () => {
        // [mat-dialog-close] fires regardless of (click) and regardless of
        // [disabled], so a merely disabled Save would still close with the payload
        // the table then tries to save.
        const save = buttonWithId('saveButton');
        expect(save).toContain('*ngIf="!isLocked"');
        expect(save).toContain('[mat-dialog-close]="data"');
      });

      it('offers unlock only on the boundary', () => {
        expect(buttonWithId('unlockButton')).toContain('*ngIf="isBoundaryDay"');
      });

      it('offers reconcile only on an open, past, clean day', () => {
        expect(buttonWithId('reconcileButton'))
          .toContain('*ngIf="reconcileEligible && !workdayForm.dirty"');
      });

      it('keeps every footer button out of the implicit form submit', () => {
        // The row sits inside <form>, whose first button is the version-history one
        // in the title; an implicit submit would click that.
        for (const id of ['cancelButton', 'reconcileButton', 'unlockButton',
          'reconcileCancelButton', 'reconcileConfirmButton',
          'unlockCancelButton', 'unlockConfirmButton']) {
          expect(buttonWithId(id)).toContain('type="button"');
        }
      });

      it('guards every reset affordance, not only the form controls', () => {
        // Each delete/restore icon must state the rule; a bare (click) is the hole
        // this round closed.
        const resets = [...template.matchAll(
          /<button[^>]*\(click\)="reset(PlannedTimes|ActualTimes|PauseToRecorded)\([^>]*>/g)];
        expect(resets).toHaveLength(7);
        for (const [tag, kind] of resets) {
          expect(tag).toContain(
            kind === 'PlannedTimes' ? '[disabled]="isLocked"' : '[disabled]="actualTimesReadOnly"');
        }
      });
    });
  });
});

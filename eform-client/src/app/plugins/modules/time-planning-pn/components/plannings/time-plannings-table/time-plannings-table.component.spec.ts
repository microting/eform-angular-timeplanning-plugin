import { ComponentFixture, TestBed } from '@angular/core/testing';
import { TimePlanningsTableComponent } from './time-plannings-table.component';
import { TimePlanningPnPlanningsService } from '../../../services/time-planning-pn-plannings.service';
import { TimePlanningPnSettingsService } from '../../../services/time-planning-pn-settings.service';
import { MatDialog } from '@angular/material/dialog';
import { TranslateService } from '@ngx-translate/core';
import { DatePipe } from '@angular/common';
import { ChangeDetectorRef, NO_ERRORS_SCHEMA } from '@angular/core';
import { Store } from '@ngrx/store';
import { of } from 'rxjs';
import { TranslateModule } from '@ngx-translate/core';
import { registerTestLocales } from '../../../testing/register-test-locales';

// The header row formats each day through DatePipe in the user's language, and this
// component defaults to 'da'. main.ts registers that data before the app runs; a bed
// has to do it itself or the pipe throws NG0701.
registerTestLocales();

describe('TimePlanningsTableComponent', () => {
  let component: TimePlanningsTableComponent;
  let fixture: ComponentFixture<TimePlanningsTableComponent>;
  let mockPlanningsService: jest.Mocked<TimePlanningPnPlanningsService>;
  let mockSettingsService: jest.Mocked<TimePlanningPnSettingsService>;
  let mockDialog: jest.Mocked<MatDialog>;
  let mockTranslateService: jest.Mocked<TranslateService>;
  let mockStore: jest.Mocked<Store>;

  beforeEach(async () => {
    mockPlanningsService = {
      getPlannings: jest.fn(),
      updatePlanning: jest.fn(),
    } as any;
    mockSettingsService = {
      getAssignedSite: jest.fn(),
      updateAssignedSite: jest.fn(),
    } as any;
    mockDialog = {
      open: jest.fn(),
    } as any;
    mockTranslateService = {
      stream: jest.fn(),
      instant: jest.fn(),
      onLangChange: of({ lang: 'en' }),
    } as any;
    mockStore = {
      select: jest.fn(),
    } as any;

    mockStore.select.mockReturnValue(of(true));
    mockTranslateService.stream.mockReturnValue(of('Translated'));
    mockTranslateService.instant.mockReturnValue('Translated');

    await TestBed.configureTestingModule({
      declarations: [TimePlanningsTableComponent],
      imports: [TranslateModule.forRoot()],
      schemas: [NO_ERRORS_SCHEMA],
      providers: [
        { provide: TimePlanningPnPlanningsService, useValue: mockPlanningsService },
        { provide: TimePlanningPnSettingsService, useValue: mockSettingsService },
        { provide: MatDialog, useValue: mockDialog },
        { provide: TranslateService, useValue: mockTranslateService },
        { provide: Store, useValue: mockStore },
        DatePipe,
        ChangeDetectorRef
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(TimePlanningsTableComponent);
    component = fixture.componentInstance;
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  describe('Time Conversion Utilities', () => {
    describe('convertMinutesToTime', () => {
      it('should convert 0 minutes to 00:00', () => {
        expect(component.convertMinutesToTime(0)).toBe('00:00');
      });

      it('should convert 60 minutes to 01:00', () => {
        expect(component.convertMinutesToTime(60)).toBe('01:00');
      });

      it('should convert 90 minutes to 01:30', () => {
        expect(component.convertMinutesToTime(90)).toBe('01:30');
      });

      it('should convert 125 minutes to 02:05', () => {
        expect(component.convertMinutesToTime(125)).toBe('02:05');
      });

      it('should handle large values correctly', () => {
        expect(component.convertMinutesToTime(1440)).toBe('24:00'); // 24 hours
      });
    });

    describe('convertHoursToTime', () => {
      it('should convert 0 hours to 00:00', () => {
        expect(component.convertHoursToTime(0)).toBe('00:00');
      });

      it('should convert 1 hour to 01:00', () => {
        expect(component.convertHoursToTime(1)).toBe('01:00');
      });

      it('should convert 1.5 hours to 01:30', () => {
        expect(component.convertHoursToTime(1.5)).toBe('01:30');
      });

      it('should convert 2.25 hours to 02:15', () => {
        expect(component.convertHoursToTime(2.25)).toBe('02:15');
      });

      it('should handle negative hours correctly', () => {
        expect(component.convertHoursToTime(-1.5)).toBe('-1:30');
      });

      it('should handle negative hours with single digit minutes', () => {
        expect(component.convertHoursToTime(-0.15)).toBe('-0:09');
      });

      it('should round minutes correctly', () => {
        expect(component.convertHoursToTime(1.016666666)).toBe('01:01'); // 1 hour and ~1 minute
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
  });

  describe('getCellClass', () => {
    it('should return white-background for cell with no plan and no work started', () => {
      const row = {
        planningPrDayModels: {
          '0': {
            planHours: 0,
            start1StartedAt: null,
            start2StartedAt: null,
            workDayEnded: false,
            plannedStartOfShift1: null,
            message: null,
            workerComment: null,
            nettoHoursOverrideActive: false
          }
        }
      };

      expect(component.getCellClass(row, '0')).toBe('white-background');
    });

    it('should return grey-background for cell with plan hours but not started', () => {
      const row = {
        planningPrDayModels: {
          '0': {
            planHours: 8,
            start1StartedAt: null,
            start2StartedAt: null,
            workDayEnded: false,
            plannedStartOfShift1: null,
            message: null,
            workerComment: null,
            nettoHoursOverrideActive: false
          }
        }
      };

      expect(component.getCellClass(row, '0')).toBe('red-background');
    });

    it('should return green-background for cell with work started and ended', () => {
      const row = {
        planningPrDayModels: {
          '0': {
            planHours: 8,
            start1StartedAt: '2024-01-15T08:00:00',
            start2StartedAt: null,
            workDayEnded: true,
            plannedStartOfShift1: null,
            message: null,
            workerComment: null,
            nettoHoursOverrideActive: false
          }
        }
      };

      expect(component.getCellClass(row, '0')).toBe('green-background');
    });

    it('should return grey-background for cell with work started but not ended', () => {
      const row = {
        planningPrDayModels: {
          '0': {
            planHours: 8,
            start1StartedAt: '2024-01-15T08:00:00',
            start2StartedAt: null,
            workDayEnded: false,
            plannedStartOfShift1: null,
            message: null,
            workerComment: null,
            nettoHoursOverrideActive: false
          }
        }
      };

      expect(component.getCellClass(row, '0')).toBe('grey-background');
    });

    it('should return green-background when nettoHoursOverrideActive is true', () => {
      const row = {
        planningPrDayModels: {
          '0': {
            planHours: 8,
            start1StartedAt: null,
            start2StartedAt: null,
            workDayEnded: false,
            plannedStartOfShift1: null,
            message: null,
            workerComment: null,
            nettoHoursOverrideActive: true
          }
        }
      };

      expect(component.getCellClass(row, '0')).toBe('green-background');
    });

    it('should return empty string when cell data is missing', () => {
      const row = {
        planningPrDayModels: {}
      };

      expect(component.getCellClass(row, '0')).toBe('');
    });

    it('should return red-background for no plan hours but work started and not ended', () => {
      const row = {
        planningPrDayModels: {
          '0': {
            planHours: 0,
            start1StartedAt: '2024-01-15T08:00:00',
            start2StartedAt: null,
            workDayEnded: false,
            plannedStartOfShift1: null,
            message: null,
            workerComment: null,
            nettoHoursOverrideActive: false
          }
        }
      };

      expect(component.getCellClass(row, '0')).toBe('red-background');
    });

    it('should return grey-background when plannedStartOfShift1 is set but no work started', () => {
      const row = {
        planningPrDayModels: {
          '0': {
            planHours: 0,
            start1StartedAt: null,
            start2StartedAt: null,
            workDayEnded: false,
            plannedStartOfShift1: '08:00',
            message: null,
            workerComment: null,
            nettoHoursOverrideActive: false
          }
        }
      };

      expect(component.getCellClass(row, '0')).toBe('red-background');
    });

    it('should return grey-background when message is set', () => {
      const row = {
        planningPrDayModels: {
          '0': {
            planHours: 0,
            start1StartedAt: null,
            start2StartedAt: null,
            workDayEnded: false,
            plannedStartOfShift1: null,
            message: 'Some message',
            workerComment: null,
            nettoHoursOverrideActive: false
          }
        }
      };

      expect(component.getCellClass(row, '0')).toBe('grey-background');
    });

    it('should return grey-background when workerComment is set', () => {
      const row = {
        planningPrDayModels: {
          '0': {
            planHours: 0,
            start1StartedAt: null,
            start2StartedAt: null,
            workDayEnded: false,
            plannedStartOfShift1: null,
            message: null,
            workerComment: 'Worker comment',
            nettoHoursOverrideActive: false
          }
        }
      };

      expect(component.getCellClass(row, '0')).toBe('grey-background');
    });

    // The reconciled ("Afstemt") day lock layers on top of the four state
    // backgrounds. Ruling F20: only the day at lockedThrough is the boundary, and it
    // alone gets the border class. Every other day at or before it is locked,
    // whether or not it carries a reconcile mark of its own.
    describe('the day lock', () => {
      const lockedRow = (date: string, extra: any = {}) => ({
        lockedThrough: '2026-09-09T00:00:00',
        planningPrDayModels: {
          '0': {
            id: 1,
            date: `${date}T00:00:00`,
            reconciled: false,
            planHours: 0,
            start1StartedAt: null,
            start2StartedAt: null,
            workDayEnded: false,
            plannedStartOfShift1: null,
            message: null,
            workerComment: null,
            nettoHoursOverrideActive: false,
            ...extra
          }
        }
      });

      it('gives the boundary day the reconciled background, on top of its state', () => {
        expect(component.getCellClass(lockedRow('2026-09-09', { reconciled: true }), '0'))
          .toBe('white-background reconciled-background');
      });

      it('gives an older reconciled day the locked background, never the boundary border', () => {
        expect(component.getCellClass(lockedRow('2026-09-07', { reconciled: true }), '0'))
          .toBe('white-background locked-background');
      });

      it('gives a day locked by a later reconciled day the locked background', () => {
        expect(component.getCellClass(lockedRow('2026-09-08'), '0'))
          .toBe('white-background locked-background');
      });

      it('leaves a day after the boundary as it was', () => {
        expect(component.getCellClass(lockedRow('2026-09-10'), '0')).toBe('white-background');
      });

      it('leaves every day as it was when the row has no boundary', () => {
        const row = lockedRow('2026-09-07', { reconciled: true });
        row.lockedThrough = null as any;
        expect(component.getCellClass(row, '0')).toBe('white-background');
      });
    });
  });

  // The legend under the grid explains the hatch, so it appears exactly when a
  // locked day is on screen.
  describe('hasLockedDayInView', () => {
    const rowWithDays = (lockedThrough: string | null) => ({
      lockedThrough,
      planningPrDayModels: [
        { id: 1, date: '2026-09-08T00:00:00', reconciled: false },
        { id: 1, date: '2026-09-09T00:00:00', reconciled: true }
      ]
    }) as any;

    it('is true once a row in view has a locked day', () => {
      component.timePlannings = [rowWithDays('2026-09-09T00:00:00')];
      component.ngOnChanges({ timePlannings: { currentValue: component.timePlannings } } as any);
      expect(component.hasLockedDayInView).toBe(true);
    });

    it('is false when nothing in view is locked', () => {
      component.timePlannings = [rowWithDays(null)];
      component.ngOnChanges({ timePlannings: { currentValue: component.timePlannings } } as any);
      expect(component.hasLockedDayInView).toBe(false);
    });
  });

  describe('onDayColumnClick', () => {
    const dayRow = (date: string, extra: any = {}) => ({
      siteId: 7,
      tags: [],
      lockedThrough: '2026-09-09T00:00:00',
      planningPrDayModels: {
        '0': { id: 1, date: `${date}T00:00:00`, reconciled: false, ...extra }
      }
    });

    const dialogData = () => (mockDialog.open as jest.Mock).mock.calls[0][1].data;

    beforeEach(() => {
      mockSettingsService.getAssignedSite.mockReturnValue(of({ success: true, model: {} }) as any);
      (mockDialog.open as jest.Mock).mockReturnValue({ afterClosed: () => of(undefined) } as any);
    });

    it('does not open a placeholder day, which has no registration behind it', () => {
      // F17: id 0 stands in for a locked date with no row, so there is nothing to
      // fetch and nothing to open.
      component.onDayColumnClick(dayRow('2026-09-08', { id: 0 }), '0');

      expect(mockSettingsService.getAssignedSite).not.toHaveBeenCalled();
      expect(mockDialog.open).not.toHaveBeenCalled();
    });

    it('tells the dialog that the boundary day is locked, sealed and the boundary', () => {
      component.onDayColumnClick(dayRow('2026-09-09', { reconciled: true }), '0');

      expect(mockDialog.open).toHaveBeenCalled();
      expect(dialogData()).toMatchObject({
        isLocked: true,
        isBoundary: true,
        isSealed: true,
        lockedThrough: '2026-09-09T00:00:00'
      });
    });

    it('tells the dialog that an older reconciled day is sealed but not the boundary', () => {
      component.onDayColumnClick(dayRow('2026-09-07', { reconciled: true }), '0');

      expect(dialogData()).toMatchObject({ isLocked: true, isBoundary: false, isSealed: true });
    });

    it('tells the dialog that a cascade-locked day carries no mark of its own', () => {
      component.onDayColumnClick(dayRow('2026-09-08'), '0');

      expect(dialogData()).toMatchObject({ isLocked: true, isBoundary: false, isSealed: false });
    });

    it('tells the dialog that a day after the boundary is open', () => {
      component.onDayColumnClick(dayRow('2026-09-10'), '0');

      expect(dialogData()).toMatchObject({ isLocked: false, isBoundary: false, isSealed: false });
    });

    describe('the close contract after a reconcile or unlock', () => {
      /** A dialog that closed with `payload`, having reported `lockStateChanged`. */
      const dialogThatClosed = (lockStateChanged: boolean, payload: any) => {
        (mockDialog.open as jest.Mock).mockReturnValue({
          componentInstance: { lockStateChanged },
          afterClosed: () => of(payload),
        } as any);
      };

      it('reloads the grid and sends no save when the dialog reconciled the day', () => {
        const changed = jest.fn();
        component.timePlanningChanged.subscribe(changed);
        // A reconcile can close through Cancel, Esc or the backdrop, so the close
        // payload proves nothing either way; lockStateChanged is what decides.
        dialogThatClosed(true, { planningPrDayModels: { id: 1 } });

        component.onDayColumnClick(dayRow('2026-09-10'), '0');

        // The day is locked now, so the save would be refused; and the grid memoises
        // its cell classes on the row reference, so only a reload redraws it.
        expect(mockPlanningsService.updatePlanning).not.toHaveBeenCalled();
        expect(changed).toHaveBeenCalledTimes(1);
      });

      it('still saves a plain close that carries a payload', () => {
        // The negative control: without it the assertion above could pass on a close
        // path that never saved in the first place.
        mockPlanningsService.updatePlanning.mockReturnValue(of({ success: true }) as any);
        dialogThatClosed(false, { planningPrDayModels: { id: 1 } });

        component.onDayColumnClick(dayRow('2026-09-10'), '0');

        expect(mockPlanningsService.updatePlanning).toHaveBeenCalledWith({ id: 1 }, 1);
      });
    });
  });

  describe('isInOlderThanToday', () => {
    it('should return false for null date', () => {
      expect(component.isInOlderThanToday(null as any)).toBe(false);
    });

    it('should return false for undefined date', () => {
      expect(component.isInOlderThanToday(undefined as any)).toBe(false);
    });

    it('should return true for date in the past', () => {
      const pastDate = new Date();
      pastDate.setDate(pastDate.getDate() - 5);
      expect(component.isInOlderThanToday(pastDate)).toBe(true);
    });

    it('should return false for today', () => {
      const today = new Date();
      expect(component.isInOlderThanToday(today)).toBe(false);
    });

    it('should return false for future date', () => {
      const futureDate = new Date();
      futureDate.setDate(futureDate.getDate() + 5);
      expect(component.isInOlderThanToday(futureDate)).toBe(false);
    });

    it('should handle string dates', () => {
      const pastDateString = '2020-01-01';
      expect(component.isInOlderThanToday(pastDateString as any)).toBe(true);
    });

    it('should return false for invalid date string', () => {
      const invalidDate = 'invalid-date';
      expect(component.isInOlderThanToday(invalidDate as any)).toBe(false);
    });
  });

  describe('getStopTimeDisplay', () => {
    it('should return empty string when startedAt is null', () => {
      expect(component.getStopTimeDisplay(null, '2024-01-15T10:00:00')).toBe('');
    });

    it('should return empty string when stoppedAt is null', () => {
      expect(component.getStopTimeDisplay('2024-01-15T08:00:00', null)).toBe('');
    });

    it('should return 24:00 when stopped date is different from started date', () => {
      const result = component.getStopTimeDisplay('2024-01-15T23:00:00', '2024-01-16T01:00:00');
      expect(result).toBe('24:00');
    });

    it('should format time correctly when on same day', () => {
      // This test depends on the DatePipe transform which we've mocked
      jest.spyOn(component['datePipe'], 'transform').mockReturnValue('10:30');
      const result = component.getStopTimeDisplay('2024-01-15T08:00:00', '2024-01-15T10:30:00');
      expect(result).toBe('10:30');
    });
  });

  // ------------------------------------------------------------------
  // formatStamp / getStopTimeDisplayWithSeconds — always HH:mm
  // (`useOneMinuteIntervals` retained on the row but no longer affects display)
  // ------------------------------------------------------------------
  describe('formatStamp', () => {
    it('returns empty string when value is falsy', () => {
      expect(component.formatStamp({ useOneMinuteIntervals: true }, null)).toBe('');
      expect(component.formatStamp({ useOneMinuteIntervals: true }, undefined as any)).toBe('');
      expect(component.formatStamp({ useOneMinuteIntervals: true }, '')).toBe('');
    });

    it("uses HH:mm format when row.useOneMinuteIntervals is false", () => {
      const transformSpy = jest
        .spyOn(component['datePipe'], 'transform')
        .mockReturnValue('07:03');
      const result = component.formatStamp(
        { useOneMinuteIntervals: false },
        '2026-05-15T07:03:53Z',
      );
      expect(transformSpy).toHaveBeenCalledWith('2026-05-15T07:03:53Z', 'HH:mm', 'UTC');
      expect(result).toBe('07:03');
    });

    it("uses HH:mm format when row.useOneMinuteIntervals is true", () => {
      const transformSpy = jest
        .spyOn(component['datePipe'], 'transform')
        .mockReturnValue('07:03');
      const result = component.formatStamp(
        { useOneMinuteIntervals: true },
        '2026-05-15T07:03:53Z',
      );
      expect(transformSpy).toHaveBeenCalledWith('2026-05-15T07:03:53Z', 'HH:mm', 'UTC');
      expect(result).toBe('07:03');
    });

    it("falls back to HH:mm when row is null/undefined (defensive)", () => {
      const transformSpy = jest
        .spyOn(component['datePipe'], 'transform')
        .mockReturnValue('07:03');
      const result = component.formatStamp(null as any, '2026-05-15T07:03:53Z');
      expect(transformSpy).toHaveBeenCalledWith('2026-05-15T07:03:53Z', 'HH:mm', 'UTC');
      expect(result).toBe('07:03');
    });
  });

  describe('getStopTimeDisplayWithSeconds', () => {
    it('returns empty string when either timestamp is falsy', () => {
      expect(
        component.getStopTimeDisplayWithSeconds({ useOneMinuteIntervals: true }, null, '2026-05-15T10:00:00Z'),
      ).toBe('');
      expect(
        component.getStopTimeDisplayWithSeconds({ useOneMinuteIntervals: true }, '2026-05-15T08:00:00Z', null),
      ).toBe('');
    });

    it('returns 24:00 when stop is on a later day regardless of flag', () => {
      expect(
        component.getStopTimeDisplayWithSeconds(
          { useOneMinuteIntervals: true },
          '2026-05-15T23:00:00Z',
          '2026-05-16T01:00:00Z',
        ),
      ).toBe('24:00');
    });

    it("uses HH:mm format when flag off (legacy parity)", () => {
      const transformSpy = jest
        .spyOn(component['datePipe'], 'transform')
        .mockReturnValue('15:30');
      const result = component.getStopTimeDisplayWithSeconds(
        { useOneMinuteIntervals: false },
        '2026-05-15T07:00:00Z',
        '2026-05-15T15:30:11Z',
      );
      expect(transformSpy).toHaveBeenCalledWith('2026-05-15T15:30:11Z', 'HH:mm', 'UTC');
      expect(result).toBe('15:30');
    });

    it("uses HH:mm format when flag on", () => {
      const transformSpy = jest
        .spyOn(component['datePipe'], 'transform')
        .mockReturnValue('15:30');
      const result = component.getStopTimeDisplayWithSeconds(
        { useOneMinuteIntervals: true },
        '2026-05-15T07:00:00Z',
        '2026-05-15T15:30:11Z',
      );
      expect(transformSpy).toHaveBeenCalledWith('2026-05-15T15:30:11Z', 'HH:mm', 'UTC');
      expect(result).toBe('15:30');
    });
  });

  describe('formatDuration', () => {
    beforeEach(() => {
      jest
        .spyOn(component['translateService'], 'instant')
        .mockImplementation((key: string) => key);
    });

    it('formats a sub-hour duration with decimal', () => {
      expect(component.formatDuration(3 / 60)).toBe('0 t 3 min (0.05 timer)');
    });
    it('formats hours + minutes with decimal', () => {
      expect(component.formatDuration(7.58)).toBe('7 t 35 min (7.58 timer)');
    });
    it('shows a whole-hour value with zero minutes', () => {
      expect(component.formatDuration(2)).toBe('2 t 0 min (2.00 timer)');
    });
    it('formats a negative duration with a leading minus on both parts', () => {
      expect(component.formatDuration(-0.53)).toBe('-0 t 32 min (-0.53 timer)');
    });
    it('omits the decimal part when withDecimal is false', () => {
      expect(component.formatDuration(30 / 60, false)).toBe('0 t 30 min');
    });
    it('renders zero as a non-negative zero', () => {
      expect(component.formatDuration(0)).toBe('0 t 0 min (0.00 timer)');
      expect(component.formatDuration(-0.0001)).toBe('0 t 0 min (0.00 timer)');
    });
    it('carries rounding into the hour', () => {
      expect(component.formatDuration(0.999)).toBe('1 t 0 min (1.00 timer)');
    });
    it('parses a numeric string (paid-out flex) and handles comma decimals', () => {
      expect(component.formatDuration('2,00')).toBe('2 t 0 min (2.00 timer)');
    });
    it('treats null/NaN as zero', () => {
      expect(component.formatDuration(null as any)).toBe('0 t 0 min (0.00 timer)');
    });
    it('derives the decimal from the true value, not the rounded minutes', () => {
      // 58.36 h -> minutes round to 58 t 22 min, but the decimal must stay 58.36
      // (deriving it from rounded minutes would wrongly yield 58.37).
      expect(component.formatDuration(58.36)).toBe('58 t 22 min (58.36 timer)');
    });
  });

  // Dormant helper — production display no longer uses seconds.
  describe('convertHoursToTimeWithSeconds', () => {
    it('formats whole-hour values with seconds suffix', () => {
      expect(component.convertHoursToTimeWithSeconds(8)).toBe('08:00:00');
    });

    it('handles fractional hours that include sub-minute precision', () => {
      // 7h 3m 53s → 7 + 3/60 + 53/3600 ≈ 7.06472222...
      const sevenThreeFiftyThree = 7 + 3 / 60 + 53 / 3600;
      expect(component.convertHoursToTimeWithSeconds(sevenThreeFiftyThree)).toBe('07:03:53');
    });

    it('emits negative sign for negative values', () => {
      expect(component.convertHoursToTimeWithSeconds(-1.5)).toBe('-1:30:00');
    });
  });
  /**
   * Rebuilding the day columns flushes change detection twice, so mtx-grid sees the
   * emptied [columns] before the new ones. What these blocks assert is the map the
   * rebuild produces, so the flush is stubbed out rather than driven through a live
   * view.
   */
  const stubHeaderFlush = (target: any) => {
    target.cdr = { detectChanges: jest.fn() };
  };

  /**
   * The worker set for a bulk reconcile. mtx-grid rebuilds its SelectionModel empty on
   * ANY input change and emits nothing, so the component has to report that itself —
   * and report it as a RESET, because an empty selection means "everyone visible" and
   * would silently widen a previewed scope.
   */
  describe('row selection', () => {
    const changed = jest.fn();
    const reset = jest.fn();

    beforeEach(() => {
      changed.mockClear();
      reset.mockClear();
      component.selectionChanged.subscribe(changed);
      component.selectionReset.subscribe(reset);
    });

    it('emits the ticked rows as site ids', () => {
      component.onRowSelected([{ siteId: 4 }, { siteId: 9 }]);

      expect(changed).toHaveBeenCalledWith([4, 9]);
      expect(reset).not.toHaveBeenCalled();
    });

    it('survives a grid that hands it nothing', () => {
      component.onRowSelected(null as any);

      expect(changed).toHaveBeenCalledWith([]);
    });

    it('reports new rows dropping a live selection as a reset, not as an empty selection', () => {
      component.onRowSelected([{ siteId: 4 }]);
      changed.mockClear();

      component.timePlannings = [];
      component.ngOnChanges({ timePlannings: { currentValue: [] } } as any);

      expect(reset).toHaveBeenCalledTimes(1);
      // An empty selection here would be read as "every visible worker".
      expect(changed).not.toHaveBeenCalled();
    });

    it('stays quiet when new rows arrive and nothing was ticked', () => {
      component.timePlannings = [];
      component.ngOnChanges({ timePlannings: { currentValue: [] } } as any);

      expect(reset).not.toHaveBeenCalled();
    });

    it('reports the reset only once, since the grid drops the selection only once', () => {
      component.onRowSelected([{ siteId: 4 }]);

      component.ngOnChanges({ timePlannings: { currentValue: [] } } as any);
      component.ngOnChanges({ timePlannings: { currentValue: [] } } as any);

      expect(reset).toHaveBeenCalledTimes(1);
    });

    it('reports new columns dropping a live selection too', () => {
      // Rebuilding the headers changes [columns] and [headerTemplate], and mtx-grid
      // empties its SelectionModel on those just as it does on new data.
      stubHeaderFlush(component);
      component.onRowSelected([{ siteId: 4 }]);
      component.dateFrom = new Date(2026, 8, 7);
      component.dateTo = new Date(2026, 8, 13);

      component.ngOnChanges({ dateTo: { currentValue: component.dateTo } } as any);

      expect(reset).toHaveBeenCalledTimes(1);
    });
  });

  describe('the past-day header', () => {
    beforeEach(() => stubHeaderFlush(component));

    /** Three days ending yesterday, so every column is in the past. */
    const showLastThreeDays = () => {
      const yesterday = new Date();
      yesterday.setHours(0, 0, 0, 0);
      yesterday.setDate(yesterday.getDate() - 1);
      component.dateFrom = new Date(yesterday);
      component.dateFrom.setDate(yesterday.getDate() - 2);
      component.dateTo = yesterday;
      component.ngOnChanges({ dateTo: { currentValue: component.dateTo } } as any);
      return yesterday;
    };

    it('offers the clickable header on past days only', () => {
      const today = new Date();
      today.setHours(0, 0, 0, 0);
      component.dateFrom = new Date(today);
      component.dateFrom.setDate(today.getDate() - 1);
      component.dateTo = new Date(today);
      component.dateTo.setDate(today.getDate() + 1);

      component.ngOnChanges({ dateTo: { currentValue: component.dateTo } } as any);

      // Yesterday, today, tomorrow: nothing at or after today may be reconciled.
      expect(Object.keys(component.dayHeaderTemplates)).toEqual(['0']);
    });

    it('asks for a preview through the day its column stands for', () => {
      const requested: Date[] = [];
      component.reconcileDateRequested.subscribe(date => requested.push(date));
      const yesterday = showLastThreeDays();

      component.onDayHeaderClick('2');

      expect(requested).toHaveLength(1);
      expect(requested[0].toDateString()).toBe(yesterday.toDateString());
    });

    it('asks for nothing when the column is not one it knows', () => {
      const requested: Date[] = [];
      component.reconcileDateRequested.subscribe(date => requested.push(date));
      showLastThreeDays();

      component.onDayHeaderClick('siteName');

      expect(requested).toHaveLength(0);
    });
  });

  /**
   * The preview classes are read from the template on every pass, because mtx-grid
   * stamps the <td> class through a pure pipe that cannot follow the container's
   * preview.
   */
  describe('the bulk reconcile preview', () => {
    const row = (siteId: number) => ({
      siteId,
      planningPrDayModels: ['2026-09-07', '2026-09-08', '2026-09-09', '2026-09-10']
        .map((date, index) => ({ id: index + 1, date: `${date}T00:00:00` })),
    }) as any;

    beforeEach(() => {
      component.reconcilePreview = {
        target: '2026-09-09',
        siteIds: [1, 2, 3],
        landingBySiteId: { 1: '2026-09-09', 3: '2026-09-09' },
        existingBySiteId: { 1: null, 2: '2026-09-10', 3: '2026-09-07' },
        outcomeBySiteId: { 1: 'lock', 2: 'skip', 3: 'lock' },
        willReconcileCount: 2,
        skipCount: 1,
      };
    });

    it('draws every day up to the landing day of an unlocked row', () => {
      expect(component.isPreviewLocked(row(1), '0')).toBe(true);
      expect(component.isPreviewLocked(row(1), '2')).toBe(true);
      expect(component.isPreviewBoundary(row(1), '2')).toBe(true);
    });

    it('leaves the days after the landing day alone', () => {
      expect(component.isPreviewLocked(row(1), '3')).toBe(false);
      expect(component.isPreviewBoundary(row(1), '3')).toBe(false);
    });

    it('draws a locked row only from its existing boundary forward', () => {
      // Row 3 is already sealed through the 7th; re-hatching that day would say the
      // preview is about to do something it is not.
      expect(component.isPreviewLocked(row(3), '0')).toBe(false);
      expect(component.isPreviewLocked(row(3), '1')).toBe(true);
      expect(component.isPreviewBoundary(row(3), '2')).toBe(true);
    });

    it('draws nothing on a row the preview skipped, and labels it instead', () => {
      expect(component.isPreviewSkipped(row(2))).toBe(true);
      expect(component.isPreviewLocked(row(2), '0')).toBe(false);
      expect(component.isPreviewBoundary(row(2), '2')).toBe(false);
    });

    it('draws nothing on a row outside the scope', () => {
      expect(component.isPreviewLocked(row(9), '0')).toBe(false);
      expect(component.isPreviewSkipped(row(9))).toBe(false);
    });

    it('draws nothing at all once the preview is cancelled', () => {
      component.reconcilePreview = null;

      expect(component.isPreviewLocked(row(1), '0')).toBe(false);
      expect(component.isPreviewBoundary(row(1), '2')).toBe(false);
      expect(component.isPreviewSkipped(row(2))).toBe(false);
    });
  });
});

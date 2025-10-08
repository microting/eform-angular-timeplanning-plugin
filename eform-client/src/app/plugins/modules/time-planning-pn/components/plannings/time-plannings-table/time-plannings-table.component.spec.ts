import { ComponentFixture, TestBed } from '@angular/core/testing';
import { TimePlanningsTableComponent } from './time-plannings-table.component';
import { TimePlanningPnPlanningsService } from '../../../services/time-planning-pn-plannings.service';
import { TimePlanningPnSettingsService } from '../../../services/time-planning-pn-settings.service';
import { MatDialog } from '@angular/material/dialog';
import { TranslateService } from '@ngx-translate/core';
import { DatePipe } from '@angular/common';
import { ChangeDetectorRef } from '@angular/core';
import { Store } from '@ngrx/store';
import { of } from 'rxjs';

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

      expect(component.getCellClass(row, '0')).toBe('grey-background');
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

      expect(component.getCellClass(row, '0')).toBe('grey-background');
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
});

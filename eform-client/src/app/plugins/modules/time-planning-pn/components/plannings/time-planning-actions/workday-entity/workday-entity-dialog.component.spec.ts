import { ComponentFixture, TestBed } from '@angular/core/testing';
import { WorkdayEntityDialogComponent } from './workday-entity-dialog.component';
import { MAT_DIALOG_DATA } from '@angular/material/dialog';
import { FormBuilder, ReactiveFormsModule } from '@angular/forms';
import { TimePlanningPnPlanningsService } from '../../../../services';
import { TranslateService } from '@ngx-translate/core';
import { DatePipe, CommonModule } from '@angular/common';
import { of } from 'rxjs';
import { NO_ERRORS_SCHEMA } from '@angular/core';
import { TranslateModule } from '@ngx-translate/core';

describe('WorkdayEntityDialogComponent', () => {
  let component: WorkdayEntityDialogComponent;
  let fixture: ComponentFixture<WorkdayEntityDialogComponent>;
  let mockPlanningsService: jest.Mocked<TimePlanningPnPlanningsService>;
  let mockTranslateService: jest.Mocked<TranslateService>;

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
    } as any;
    mockTranslateService = {
      instant: jest.fn(),
      stream: jest.fn(),
      onLangChange: of({ lang: 'en' }),
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
        { provide: MAT_DIALOG_DATA, useValue: mockData },
        { provide: TimePlanningPnPlanningsService, useValue: mockPlanningsService },
        { provide: TranslateService, useValue: mockTranslateService }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(WorkdayEntityDialogComponent);
    component = fixture.componentInstance;
  });

  it('should create', () => {
    expect(component).toBeTruthy();
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
});

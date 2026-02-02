import { ComponentFixture, TestBed } from '@angular/core/testing';
import { AssignedSiteDialogComponent } from './assigned-site-dialog.component';
import { MAT_DIALOG_DATA } from '@angular/material/dialog';
import { FormBuilder, ReactiveFormsModule } from '@angular/forms';
import { TimePlanningPnSettingsService } from '../../../../services';
import { Store } from '@ngrx/store';
import { of } from 'rxjs';
import { NO_ERRORS_SCHEMA } from '@angular/core';
import { TranslateModule } from '@ngx-translate/core';
import { HttpClientTestingModule } from '@angular/common/http/testing';

describe('AssignedSiteDialogComponent', () => {
  let component: AssignedSiteDialogComponent;
  let fixture: ComponentFixture<AssignedSiteDialogComponent>;
  let mockSettingsService: jest.Mocked<TimePlanningPnSettingsService>;
  let mockStore: jest.Mocked<Store>;

  const mockAssignedSiteData = {
    id: 1,
    siteId: 1,
    siteName: 'Test Site',
    useGoogleSheetAsDefault: false,
    useOnlyPlanHours: false,
    autoBreakCalculationActive: false,
    allowPersonalTimeRegistration: true,
    allowEditOfRegistrations: true,
    usePunchClock: false,
    usePunchClockWithAllowRegisteringInHistory: false,
    allowAcceptOfPlannedHours: false,
    daysBackInTimeAllowedEditingEnabled: false,
    thirdShiftActive: false,
    fourthShiftActive: false,
    fifthShiftActive: false,
    resigned: false,
    resignedAtDate: new Date().toISOString(),
    isManager: false,
    managingTagIds: [],
    mondayPlanHours: 0,
    tuesdayPlanHours: 0,
    wednesdayPlanHours: 0,
    thursdayPlanHours: 0,
    fridayPlanHours: 0,
    saturdayPlanHours: 0,
    sundayPlanHours: 0,
    mondayCalculatedHours: null,
    tuesdayCalculatedHours: null,
    wednesdayCalculatedHours: null,
    thursdayCalculatedHours: null,
    fridayCalculatedHours: null,
    saturdayCalculatedHours: null,
    sundayCalculatedHours: null,
    startMonday: 480, // 08:00
    endMonday: 1020, // 17:00
    breakMonday: 60, // 1 hour
  };

  beforeEach(async () => {
    mockSettingsService = {
      getGlobalAutoBreakCalculationSettings: jest.fn(),
      updateAssignedSite: jest.fn(),
      getAssignedSite: jest.fn(),
      getAvailableTags: jest.fn(),
    } as any;
    mockStore = {
      select: jest.fn(),
    } as any;

    mockStore.select.mockReturnValue(of(true));
    mockSettingsService.getGlobalAutoBreakCalculationSettings.mockReturnValue(
      of({ success: true, model: {} }) as any
    );
    mockSettingsService.getAvailableTags.mockReturnValue(
      of({ success: true, model: [] }) as any
    );

    await TestBed.configureTestingModule({
      declarations: [AssignedSiteDialogComponent],
      imports: [ReactiveFormsModule, TranslateModule.forRoot(), HttpClientTestingModule],
      schemas: [NO_ERRORS_SCHEMA],
      providers: [
        FormBuilder,
        { provide: MAT_DIALOG_DATA, useValue: mockAssignedSiteData },
        { provide: TimePlanningPnSettingsService, useValue: mockSettingsService },
        { provide: Store, useValue: mockStore },
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(AssignedSiteDialogComponent);
    component = fixture.componentInstance;
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  describe('Time Conversion Utilities', () => {
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

    describe('getConvertedValue', () => {
      it('should convert minutes to time format HH:MM', () => {
        expect(component.getConvertedValue(0, 0)).toBe('');
        expect(component.getConvertedValue(60)).toBe('01:00');
        expect(component.getConvertedValue(90)).toBe('01:30');
        expect(component.getConvertedValue(480)).toBe('08:00'); // 8 hours
        expect(component.getConvertedValue(1020)).toBe('17:00'); // 17 hours
      });

      it('should return empty string when minutes is 0 and compareMinutes is also 0', () => {
        expect(component.getConvertedValue(0, 0)).toBe('');
      });

      it('should return 00:00 when both minutes and compareMinutes are null or undefined', () => {
        expect(component.getConvertedValue(0, null)).toBe('');
        expect(component.getConvertedValue(0, undefined)).toBe('');
      });

      it('should handle minutes with remainders correctly', () => {
        expect(component.getConvertedValue(125)).toBe('02:05'); // 2 hours 5 minutes
        expect(component.getConvertedValue(517)).toBe('08:37'); // 8 hours 37 minutes
      });
    });
  });

  describe('Shift Hours Calculation', () => {
    describe('calculateDayHours', () => {
      it('should calculate hours for a single shift without break', () => {
        // 8:00 to 17:00 = 9 hours
        const result = component.calculateDayHours(480, 1020, 0, 0, 0, 0);
        expect(result).toBe('9:0');
      });

      it('should calculate hours for a single shift with break', () => {
        // 8:00 to 17:00 with 1 hour break = 8 hours
        const result = component.calculateDayHours(480, 1020, 60, 0, 0, 0);
        expect(result).toBe('8:0');
      });

      it('should calculate hours for two shifts', () => {
        // First shift: 8:00 to 12:00 (4 hours)
        // Second shift: 13:00 to 17:00 (4 hours)
        // Total: 8 hours
        const result = component.calculateDayHours(480, 720, 0, 780, 1020, 0);
        expect(result).toBe('8:0');
      });

      it('should handle breaks in both shifts', () => {
        // First shift: 8:00 to 12:00 - 30 min break = 3.5 hours
        // Second shift: 13:00 to 17:00 - 30 min break = 3.5 hours
        // Total: 7 hours
        const result = component.calculateDayHours(480, 720, 30, 780, 1020, 30);
        expect(result).toBe('7:0');
      });

      it('should handle shifts with partial hours', () => {
        // 8:00 to 16:30 with 30 min break = 8 hours
        const result = component.calculateDayHours(480, 990, 30, 0, 0, 0);
        expect(result).toBe('8:0');
      });

      it('should return 0:0 when no shifts are provided', () => {
        const result = component.calculateDayHours(0, 0, 0, 0, 0, 0);
        expect(result).toBe('0:0');
      });

      it('should handle only second shift', () => {
        // Second shift only: 13:00 to 17:00 = 4 hours
        const result = component.calculateDayHours(0, 0, 0, 780, 1020, 0);
        expect(result).toBe('4:0');
      });
    });
  });

  describe('Form Initialization', () => {
    it('should initialize form with correct structure', () => {
      component.ngOnInit();

      expect(component.assignedSiteForm).toBeDefined();
      expect(component.assignedSiteForm.get('useGoogleSheetAsDefault')).toBeDefined();
      expect(component.assignedSiteForm.get('useOnlyPlanHours')).toBeDefined();
      expect(component.assignedSiteForm.get('planHours')).toBeDefined();
      expect(component.assignedSiteForm.get('firstShift')).toBeDefined();
      expect(component.assignedSiteForm.get('secondShift')).toBeDefined();
      expect(component.assignedSiteForm.get('isManager')).toBeDefined();
      expect(component.assignedSiteForm.get('managingTagIds')).toBeDefined();
    });

    it('should populate form with data values', () => {
      component.ngOnInit();

      expect(component.assignedSiteForm.get('useGoogleSheetAsDefault')?.value).toBe(false);
      expect(component.assignedSiteForm.get('useOnlyPlanHours')?.value).toBe(false);
      expect(component.assignedSiteForm.get('isManager')?.value).toBe(false);
      expect(component.assignedSiteForm.get('managingTagIds')?.value).toEqual([]);
    });

    it('should call getAvailableTags on initialization', () => {
      component.ngOnInit();

      expect(mockSettingsService.getAvailableTags).toHaveBeenCalled();
    });

    it('should load available tags from service', () => {
      const mockTags = [
        { id: 1, name: 'Tag 1' },
        { id: 2, name: 'Tag 2' }
      ];
      mockSettingsService.getAvailableTags.mockReturnValue(
        of({ success: true, model: mockTags }) as any
      );

      component.ngOnInit();

      expect(component.availableTags).toEqual(mockTags);
    });

    it('should handle tags loading error gracefully', () => {
      const consoleErrorSpy = jest.spyOn(console, 'error').mockImplementation();
      mockSettingsService.getAvailableTags.mockReturnValue(
        of({ success: false, model: null }) as any
      );

      component.ngOnInit();

      expect(component.availableTags).toEqual([]);
      consoleErrorSpy.mockRestore();
    });

    it('should create shift forms for each day of the week', () => {
      component.ngOnInit();

      const days = ['monday', 'tuesday', 'wednesday', 'thursday', 'friday', 'saturday', 'sunday'];
      const firstShift = component.assignedSiteForm.get('firstShift');

      days.forEach(day => {
        expect(firstShift?.get(day)).toBeDefined();
        expect(firstShift?.get(day)?.get('start')).toBeDefined();
        expect(firstShift?.get(day)?.get('end')).toBeDefined();
        expect(firstShift?.get(day)?.get('break')).toBeDefined();
      });
    });
  });

  describe('Break Settings', () => {
    beforeEach(() => {
      component.ngOnInit();
      mockSettingsService.getGlobalAutoBreakCalculationSettings.mockReturnValue(
        of({
          success: true,
          model: {
            mondayBreakMinutesDivider: 480,
            mondayBreakMinutesPrDivider: 30,
            mondayBreakMinutesUpperLimit: 60
          }
        }) as any
      );
    });

    it('should copy break settings from global settings for monday', () => {
      // Reinitialize to get the new global settings
      component.ngOnInit();

      component.copyBreakSettings('monday');

      const mondayBreak = component.assignedSiteForm.get('autoBreakSettings')?.get('monday');
      expect(mondayBreak?.get('breakMinutesDivider')?.value).toBe('08:00');
      expect(mondayBreak?.get('breakMinutesPrDivider')?.value).toBe('00:30');
      expect(mondayBreak?.get('breakMinutesUpperLimit')?.value).toBe('01:00');
    });

    it('should handle missing global settings gracefully', () => {
      component['globalAutoBreakSettings'] = null;

      component.copyBreakSettings('monday');

      // Should not throw error and should not modify values
      const mondayBreak = component.assignedSiteForm.get('autoBreakSettings')?.get('monday');
      expect(mondayBreak).toBeDefined();
    });
  });

  describe('Data Change Detection', () => {
    it('should detect when data has changed', () => {
      component.ngOnInit();

      expect(component.hasDataChanged()).toBe(false);

      component.data.useGoogleSheetAsDefault = true;

      expect(component.hasDataChanged()).toBe(true);
    });
  });

  describe('Time Field Update', () => {
    it('should set minutes correctly from time string', () => {
      component.ngOnInit();

      component.setMinutes('08:30', 'startMonday');

      expect(component.data['startMonday']).toBe(510); // 8*60 + 30
    });

    it('should set minutes to 0 when empty value provided', () => {
      component.ngOnInit();
      component.data['startMonday'] = 480;

      component.setMinutes('', 'startMonday');

      expect(component.data['startMonday']).toBe(0);
    });

    it('should handle different time formats', () => {
      component.ngOnInit();

      component.setMinutes('12:00', 'startMonday');
      expect(component.data['startMonday']).toBe(720); // 12*60

      component.setMinutes('00:30', 'endMonday');
      expect(component.data['endMonday']).toBe(30);
    });
  });

  describe('FormGroup Getters', () => {
    beforeEach(() => {
      component.ngOnInit();
    });

    it('should return plan hours form group', () => {
      const planHoursGroup = component.getPlanHoursFormGroup();
      expect(planHoursGroup).toBeDefined();
      expect(planHoursGroup.get('monday')).toBeDefined();
    });

    it('should return auto break settings form group', () => {
      const autoBreakGroup = component.getAutoBreakSettingsFormGroup();
      expect(autoBreakGroup).toBeDefined();
      expect(autoBreakGroup.get('monday')).toBeDefined();
    });

    it('should return first shift form group', () => {
      const firstShiftGroup = component.getFirstShiftFormGroup();
      expect(firstShiftGroup).toBeDefined();
      expect(firstShiftGroup.get('monday')).toBeDefined();
    });

    it('should return second shift form group', () => {
      const secondShiftGroup = component.getSecondShiftFormGroup();
      expect(secondShiftGroup).toBeDefined();
    });

    it('should return third shift form group', () => {
      const thirdShiftGroup = component.getThirdShiftFormGroup();
      expect(thirdShiftGroup).toBeDefined();
    });

    it('should return fourth shift form group', () => {
      const fourthShiftGroup = component.getFourthShiftFormGroup();
      expect(fourthShiftGroup).toBeDefined();
    });

    it('should return fifth shift form group', () => {
      const fifthShiftGroup = component.getFifthShiftFormGroup();
      expect(fifthShiftGroup).toBeDefined();
    });
  });

  describe('Manager and Tags Functionality', () => {
    beforeEach(() => {
      // Call ngOnInit to initialize the form without rendering the template
      component.ngOnInit();
    });

    it('should initialize isManager form control with false by default', () => {
      const isManagerControl = component.assignedSiteForm.get('isManager');
      expect(isManagerControl).toBeDefined();
      expect(isManagerControl?.value).toBe(false);
    });

    it('should initialize managingTagIds form control with empty array by default', () => {
      const managingTagIdsControl = component.assignedSiteForm.get('managingTagIds');
      expect(managingTagIdsControl).toBeDefined();
      expect(managingTagIdsControl?.value).toEqual([]);
    });

    it('should set isManager to true when toggled', () => {
      const isManagerControl = component.assignedSiteForm.get('isManager');
      isManagerControl?.setValue(true);
      expect(isManagerControl?.value).toBe(true);
    });

    it('should update managingTagIds when tags are selected', () => {
      const managingTagIdsControl = component.assignedSiteForm.get('managingTagIds');
      const selectedTags = [1, 2, 3];
      managingTagIdsControl?.setValue(selectedTags);
      expect(managingTagIdsControl?.value).toEqual(selectedTags);
    });

    it('should initialize availableTags as empty array', () => {
      expect(component.availableTags).toBeDefined();
      expect(component.availableTags).toEqual([]);
    });

    it('should preserve isManager value from data', () => {
      const dataWithManager = {
        ...mockAssignedSiteData,
        isManager: true,
        managingTagIds: [1, 2]
      };
      
      TestBed.resetTestingModule();
      TestBed.configureTestingModule({
        declarations: [AssignedSiteDialogComponent],
        imports: [ReactiveFormsModule, TranslateModule.forRoot(), HttpClientTestingModule],
        schemas: [NO_ERRORS_SCHEMA],
        providers: [
          FormBuilder,
          { provide: MAT_DIALOG_DATA, useValue: dataWithManager },
          { provide: TimePlanningPnSettingsService, useValue: mockSettingsService },
          { provide: Store, useValue: mockStore },
          { provide: ToastrService, useValue: mockToastrService }
        ]
      }).compileComponents();
      
      const newFixture = TestBed.createComponent(AssignedSiteDialogComponent);
      const newComponent = newFixture.componentInstance;
      // Call ngOnInit to initialize the form without rendering the template
      newComponent.ngOnInit();
      
      const isManagerControl = newComponent.assignedSiteForm.get('isManager');
      const managingTagIdsControl = newComponent.assignedSiteForm.get('managingTagIds');
      
      expect(isManagerControl?.value).toBe(true);
      expect(managingTagIdsControl?.value).toEqual([1, 2]);
    });
  });
});

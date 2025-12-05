import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ShiftTabComponent } from './shift-tab.component';
import { FormBuilder, ReactiveFormsModule } from '@angular/forms';
import { NO_ERRORS_SCHEMA } from '@angular/core';
import { TranslateModule } from '@ngx-translate/core';
import { AssignedSiteModel } from '../../../../../models';

describe('ShiftTabComponent', () => {
  let component: ShiftTabComponent;
  let fixture: ComponentFixture<ShiftTabComponent>;
  let fb: FormBuilder;

  const mockAssignedSiteData: AssignedSiteModel = {
    id: 1,
    siteId: 1,
    siteName: 'Test Site',
    useGoogleSheetAsDefault: false,
    useOnlyPlanHours: false,
    autoBreakCalculationActive: false,
    globalAutoBreakCalculationActive: true,
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
  } as AssignedSiteModel;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      declarations: [ShiftTabComponent],
      imports: [ReactiveFormsModule, TranslateModule.forRoot()],
      schemas: [NO_ERRORS_SCHEMA],
      providers: [FormBuilder]
    }).compileComponents();

    fixture = TestBed.createComponent(ShiftTabComponent);
    component = fixture.componentInstance;
    fb = TestBed.inject(FormBuilder);
    
    component.data = mockAssignedSiteData;
    component.shiftForm = fb.group({
      monday: fb.group({
        start: ['08:00'],
        end: ['17:00'],
        break: ['01:00'],
        calculatedHours: ['8:0']
      })
    });
    component.shiftSuffix = '';
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  it('should accept isAdmin as input', () => {
    component.isAdmin = true;
    expect(component.isAdmin).toBe(true);
  });

  it('should have default isAdmin value as false', () => {
    expect(component.isAdmin).toBe(false);
  });

  it('should emit minutesSet event when setMinutes is called', () => {
    const emitSpy = jest.spyOn(component.minutesSet, 'emit');
    const mockEvent = { target: { value: '08:30' } };
    
    component.setMinutes(mockEvent, 'startMonday');
    
    expect(emitSpy).toHaveBeenCalledWith({
      event: mockEvent,
      field: 'startMonday'
    });
  });

  it('should have correct shiftSuffix', () => {
    expect(component.shiftSuffix).toBe('');
    
    component.shiftSuffix = '2NdShift';
    expect(component.shiftSuffix).toBe('2NdShift');
  });

  it('should display all days of the week', () => {
    expect(component.days).toEqual([
      'Monday', 'Tuesday', 'Wednesday', 'Thursday', 'Friday', 'Saturday', 'Sunday'
    ]);
  });

  it('should accept data as input', () => {
    expect(component.data).toEqual(mockAssignedSiteData);
  });

  it('should accept shiftForm as input', () => {
    expect(component.shiftForm).toBeDefined();
    expect(component.shiftForm.get('monday')).toBeDefined();
  });
});

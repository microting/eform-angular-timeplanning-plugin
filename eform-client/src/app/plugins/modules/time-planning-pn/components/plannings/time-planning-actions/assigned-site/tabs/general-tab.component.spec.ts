import { ComponentFixture, TestBed } from '@angular/core/testing';
import { GeneralTabComponent } from './general-tab.component';
import { FormBuilder, ReactiveFormsModule } from '@angular/forms';
import { NO_ERRORS_SCHEMA } from '@angular/core';
import { TranslateModule } from '@ngx-translate/core';
import { AssignedSiteModel } from '../../../../../models';

describe('GeneralTabComponent', () => {
  let component: GeneralTabComponent;
  let fixture: ComponentFixture<GeneralTabComponent>;
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
      declarations: [GeneralTabComponent],
      imports: [ReactiveFormsModule, TranslateModule.forRoot()],
      schemas: [NO_ERRORS_SCHEMA],
      providers: [FormBuilder]
    }).compileComponents();

    fixture = TestBed.createComponent(GeneralTabComponent);
    component = fixture.componentInstance;
    fb = TestBed.inject(FormBuilder);
    
    component.data = mockAssignedSiteData;
    component.assignedSiteForm = fb.group({
      useGoogleSheetAsDefault: [false],
      useOnlyPlanHours: [false],
      autoBreakCalculationActive: [false],
      allowPersonalTimeRegistration: [true],
      allowEditOfRegistrations: [true],
      usePunchClock: [false],
      usePunchClockWithAllowRegisteringInHistory: [false],
      allowAcceptOfPlannedHours: [false],
      daysBackInTimeAllowedEditingEnabled: [false],
      thirdShiftActive: [false],
      fourthShiftActive: [false],
      fifthShiftActive: [false],
      resigned: [false],
      resignedAtDate: [new Date()],
    });
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  it('should accept isFirstUser as input', () => {
    component.isFirstUser = true;
    fixture.detectChanges();
    expect(component.isFirstUser).toBe(true);
  });

  it('should accept isAdmin as input', () => {
    component.isAdmin = true;
    fixture.detectChanges();
    expect(component.isAdmin).toBe(true);
  });

  it('should render checkboxes when isFirstUser is true', () => {
    component.isFirstUser = true;
    component.isAdmin = false;
    fixture.detectChanges();
    
    const compiled = fixture.nativeElement;
    const useGoogleSheetCheckbox = compiled.querySelector('#useGoogleSheetAsDefault');
    expect(useGoogleSheetCheckbox).toBeTruthy();
  });

  it('should render admin-only checkboxes when isAdmin is true', () => {
    component.isFirstUser = false;
    component.isAdmin = true;
    fixture.detectChanges();
    
    const compiled = fixture.nativeElement;
    const allowPersonalTimeCheckbox = compiled.querySelector('#allowPersonalTimeRegistration');
    expect(allowPersonalTimeCheckbox).toBeTruthy();
  });

  it('should not render first user checkboxes when isFirstUser is false', () => {
    component.isFirstUser = false;
    component.isAdmin = false;
    fixture.detectChanges();
    
    const compiled = fixture.nativeElement;
    const useGoogleSheetCheckbox = compiled.querySelector('#useGoogleSheetAsDefault');
    expect(useGoogleSheetCheckbox).toBeFalsy();
  });
});

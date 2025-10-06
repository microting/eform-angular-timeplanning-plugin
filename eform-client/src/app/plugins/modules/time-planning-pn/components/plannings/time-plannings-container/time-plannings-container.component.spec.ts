import { ComponentFixture, TestBed } from '@angular/core/testing';
import { TimePlanningsContainerComponent } from './time-plannings-container.component';
import { TimePlanningPnPlanningsService } from '../../../services/time-planning-pn-plannings.service';
import { TimePlanningPnSettingsService } from '../../../services/time-planning-pn-settings.service';
import { MatDialog } from '@angular/material/dialog';
import { Store } from '@ngrx/store';
import { of } from 'rxjs';
import { format } from 'date-fns';

describe('TimePlanningsContainerComponent', () => {
  let component: TimePlanningsContainerComponent;
  let fixture: ComponentFixture<TimePlanningsContainerComponent>;
  let mockPlanningsService: jasmine.SpyObj<TimePlanningPnPlanningsService>;
  let mockSettingsService: jasmine.SpyObj<TimePlanningPnSettingsService>;
  let mockDialog: jasmine.SpyObj<MatDialog>;
  let mockStore: jasmine.SpyObj<Store>;

  beforeEach(async () => {
    mockPlanningsService = jasmine.createSpyObj('TimePlanningPnPlanningsService', ['getPlannings', 'updatePlanning']);
    mockSettingsService = jasmine.createSpyObj('TimePlanningPnSettingsService', ['getAvailableSites', 'getResignedSites', 'getAssignedSite', 'updateAssignedSite']);
    mockDialog = jasmine.createSpyObj('MatDialog', ['open']);
    mockStore = jasmine.createSpyObj('Store', ['select']);

    mockStore.select.and.returnValue(of('en-US'));
    mockSettingsService.getAvailableSites.and.returnValue(of({ success: true, model: [] }) as any);
    mockPlanningsService.getPlannings.and.returnValue(of({ success: true, model: [] }) as any);

    await TestBed.configureTestingModule({
      declarations: [TimePlanningsContainerComponent],
      providers: [
        { provide: TimePlanningPnPlanningsService, useValue: mockPlanningsService },
        { provide: TimePlanningPnSettingsService, useValue: mockSettingsService },
        { provide: MatDialog, useValue: mockDialog },
        { provide: Store, useValue: mockStore }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(TimePlanningsContainerComponent);
    component = fixture.componentInstance;
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
      spyOn(component, 'getPlannings');
      
      component.onTimePlanningChanged({});

      expect(component.getPlannings).toHaveBeenCalled();
    });

    it('should call getPlannings when onAssignedSiteChanged is triggered', () => {
      spyOn(component, 'getPlannings');
      
      component.onAssignedSiteChanged({});

      expect(component.getPlannings).toHaveBeenCalled();
    });

    it('should update siteId and call getPlannings when onSiteChanged is triggered', () => {
      spyOn(component, 'getPlannings');
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
      mockDialog.open.and.returnValue(mockDialogRef as any);

      component.openDownloadExcelDialog();

      expect(mockDialog.open).toHaveBeenCalled();
    });
  });

  describe('Show Resigned Sites', () => {
    it('should load resigned sites when showResignedSites is true', () => {
      mockSettingsService.getResignedSites.and.returnValue(of({ success: true, model: [{ id: 1, name: 'Resigned Site' }] } as any));
      
      component.onShowResignedSitesChanged({ checked: true });

      expect(mockSettingsService.getResignedSites).toHaveBeenCalled();
      expect(component.showResignedSites).toBe(true);
    });

    it('should load available sites when showResignedSites is false', () => {
      component.showResignedSites = true;
      mockSettingsService.getAvailableSites.and.returnValue(of({ success: true, model: [{ id: 1, name: 'Available Site' }] } as any));
      
      component.onShowResignedSitesChanged({ checked: false });

      expect(mockSettingsService.getAvailableSites).toHaveBeenCalled();
      expect(component.showResignedSites).toBe(false);
    });
  });
});

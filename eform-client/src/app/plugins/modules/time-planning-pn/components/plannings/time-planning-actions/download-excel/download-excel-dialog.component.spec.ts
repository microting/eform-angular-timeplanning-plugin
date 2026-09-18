import { ComponentFixture, TestBed } from '@angular/core/testing';
import { DownloadExcelDialogComponent, DownloadExcelDialogData } from './download-excel-dialog.component';
import { MAT_DIALOG_DATA } from '@angular/material/dialog';
import { TimePlanningPnPlanningsService, TimePlanningPnWorkingHoursService } from '../../../../services';
import { ToastrService } from 'ngx-toastr';
import { of, throwError } from 'rxjs';
import { format } from 'date-fns';
import { NO_ERRORS_SCHEMA } from '@angular/core';
import { TranslateModule } from '@ngx-translate/core';
import { FormsModule } from '@angular/forms';
import { CommonModule } from '@angular/common';

describe('DownloadExcelDialogComponent', () => {
  let component: DownloadExcelDialogComponent;
  let fixture: ComponentFixture<DownloadExcelDialogComponent>;
  let mockWorkingHoursService: jest.Mocked<TimePlanningPnWorkingHoursService>;
  let mockPlanningsService: jest.Mocked<TimePlanningPnPlanningsService>;
  let mockToastrService: jest.Mocked<ToastrService>;
  let dialogData: DownloadExcelDialogData;

  /** Two active workers on tag 1, one on tag 2, and a resigned one that also carries tag 1. */
  const siteTags = [
    { siteId: 11, tagIds: [1], resigned: false },
    { siteId: 12, tagIds: [1, 2], resigned: false },
    { siteId: 13, tagIds: [], resigned: false },
    { siteId: 14, tagIds: [1], resigned: true },
  ];

  beforeEach(async () => {
    // Mock URL.createObjectURL and URL.revokeObjectURL for file-saver
    global.URL.createObjectURL = jest.fn(() => 'mock-url');
    global.URL.revokeObjectURL = jest.fn();
    
    // Mock HTMLAnchorElement.prototype.click to prevent navigation errors
    const mockClick = jest.fn();
    Object.defineProperty(HTMLAnchorElement.prototype, 'click', {
      configurable: true,
      value: mockClick,
    });
    
    mockWorkingHoursService = {
      downloadReport: jest.fn(),
      downloadReportAllWorkers: jest.fn(),
    } as any;
    mockPlanningsService = {
      getSiteTags: jest.fn().mockReturnValue(of({ success: true, model: siteTags })),
    } as any;
    mockToastrService = {
      error: jest.fn(),
      success: jest.fn(),
    } as any;

    dialogData = {
      availableSites: [{ siteId: 11, siteName: 'A' }, { siteId: 12, siteName: 'B' }] as any,
      availableTags: [{ id: 1, name: 'Tag 1' }, { id: 2, name: 'Tag 2' }],
      dateFrom: new Date(2024, 0, 15),
      dateTo: new Date(2024, 0, 21),
      selectedTagIds: [1],
      siteId: null,
    };

    await TestBed.configureTestingModule({
      declarations: [DownloadExcelDialogComponent],
      imports: [CommonModule, FormsModule, TranslateModule.forRoot()],
      schemas: [NO_ERRORS_SCHEMA],
      providers: [
        { provide: MAT_DIALOG_DATA, useValue: dialogData },
        { provide: TimePlanningPnWorkingHoursService, useValue: mockWorkingHoursService },
        { provide: TimePlanningPnPlanningsService, useValue: mockPlanningsService },
        { provide: ToastrService, useValue: mockToastrService }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(DownloadExcelDialogComponent);
    component = fixture.componentInstance;
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  describe('Inherited page filters', () => {
    it('should start from the period, tags and worker the page had', () => {
      expect(component.dateFrom).toEqual(dialogData.dateFrom);
      expect(component.dateTo).toEqual(dialogData.dateTo);
      expect(component.selectedTagIds).toEqual([1]);
      expect(component.availableTags).toBe(dialogData.availableTags);
    });

    it('should not write changes back to the page state', () => {
      component.onTagsChanged([2]);
      component.updateDateFrom({ value: new Date(2024, 1, 1) } as any);

      expect(dialogData.selectedTagIds).toEqual([1]);
      expect(dialogData.dateFrom).toEqual(new Date(2024, 0, 15));
    });
  });

  describe('Export scope count', () => {
    it('should count the workers carrying any of the selected tags, over an inclusive period', () => {
      component.ngOnInit();

      expect(component.workerCount).toBe(2); // sites 11 and 12
      expect(component.dayCount).toBe(7);
    });

    it('should count every non-resigned worker when no tag is selected', () => {
      component.ngOnInit();

      component.onTagsChanged([]);

      expect(component.workerCount).toBe(3);
    });

    it('should never count a resigned worker, because the export leaves them out', () => {
      component.ngOnInit();

      // Site 14 carries tag 1 and is the only other worker that would qualify.
      expect(component.workerCount).toBe(2);
      component.onTagsChanged([]);
      expect(component.workerCount).toBe(3);
    });

    it('should count a single worker as one, whatever the tags say', () => {
      component.ngOnInit();

      component.onSiteChanged(11);

      expect(component.workerCount).toBe(1);
    });

    it('should hide the count when the tag map cannot be fetched', () => {
      mockPlanningsService.getSiteTags.mockReturnValue(throwError(() => new Error('nope')));

      component.ngOnInit();

      expect(component.workerCount).toBeNull();
    });
  });

  describe('Site Selection', () => {
    it('should update siteId when onSiteChanged is called', () => {
      const testSiteId = 123;
      
      component.onSiteChanged(testSiteId);

      expect(component.siteId).toBe(testSiteId);
    });
  });

  describe('Date Updates', () => {
    it('should update dateFrom when updateDateFrom is called', () => {
      const testDate = new Date(2024, 0, 15);
      const event = { value: testDate } as any;

      component.updateDateFrom(event);

      expect(component.dateFrom).toBe(testDate);
    });

    it('should update dateTo when updateDateTo is called', () => {
      const testDate = new Date(2024, 0, 21);
      const event = { value: testDate } as any;

      component.updateDateTo(event);

      expect(component.dateTo).toBe(testDate);
    });
  });

  describe('Excel Report Download', () => {
    beforeEach(() => {
      component.dateFrom = new Date(2024, 0, 15);
      component.dateTo = new Date(2024, 0, 21);
      component.siteId = 123;
    });

    it('should call downloadReport with correct model', () => {
      const mockBlob = new Blob(['test'], { type: 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet' });
      mockWorkingHoursService.downloadReport.mockReturnValue(of(mockBlob));

      component.onDownloadExcelReport();

      expect(mockWorkingHoursService.downloadReport).toHaveBeenCalledWith({
        dateFrom: '2024-01-15',
        dateTo: '2024-01-21',
        siteId: 123
      });
    });

    it('should show error toast when download fails', (done) => {
      mockWorkingHoursService.downloadReport.mockReturnValue(
        throwError(() => new Error('Download failed'))
      );

      component.onDownloadExcelReport();

      // Wait a bit for async operations
      setTimeout(() => {
        expect(mockToastrService.error).toHaveBeenCalledWith('Error downloading report');
        done();
      }, 100);
    });
  });

  describe('Excel Report All Workers Download', () => {
    beforeEach(() => {
      component.dateFrom = new Date(2024, 0, 15);
      component.dateTo = new Date(2024, 0, 21);
    });

    it('should call downloadReportAllWorkers with correct model', () => {
      const mockBlob = new Blob(['test'], { type: 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet' });
      mockWorkingHoursService.downloadReportAllWorkers.mockReturnValue(of(mockBlob));

      component.onDownloadExcelReportAllWorkers();

      expect(mockWorkingHoursService.downloadReportAllWorkers).toHaveBeenCalledWith({
        dateFrom: '2024-01-15',
        dateTo: '2024-01-21',
        tagIds: [1]
      });
    });

    it('should leave tagIds out when no tag is selected', () => {
      const mockBlob = new Blob(['test'], { type: 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet' });
      mockWorkingHoursService.downloadReportAllWorkers.mockReturnValue(of(mockBlob));
      component.onTagsChanged([]);

      component.onDownloadExcelReportAllWorkers();

      const callArgs = mockWorkingHoursService.downloadReportAllWorkers.mock.calls[0][0];
      expect('tagIds' in callArgs).toBe(false);
    });

    it('should show error toast when download all workers fails', (done) => {
      mockWorkingHoursService.downloadReportAllWorkers.mockReturnValue(
        throwError(() => new Error('Download failed'))
      );

      component.onDownloadExcelReportAllWorkers();

      // Wait a bit for async operations
      setTimeout(() => {
        expect(mockToastrService.error).toHaveBeenCalledWith('Error downloading report');
        done();
      }, 100);
    });

    it('should not include siteId in all workers report model', () => {
      const mockBlob = new Blob(['test'], { type: 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet' });
      mockWorkingHoursService.downloadReportAllWorkers.mockReturnValue(of(mockBlob));
      component.siteId = 999; // Should not be included

      component.onDownloadExcelReportAllWorkers();

      const callArgs = mockWorkingHoursService.downloadReportAllWorkers.mock.calls[mockWorkingHoursService.downloadReportAllWorkers.mock.calls.length - 1][0];
      expect('siteId' in callArgs).toBe(false);
    });
  });
});

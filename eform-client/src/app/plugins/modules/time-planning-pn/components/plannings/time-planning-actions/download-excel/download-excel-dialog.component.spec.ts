import { ComponentFixture, TestBed } from '@angular/core/testing';
import { DownloadExcelDialogComponent } from './download-excel-dialog.component';
import { MAT_DIALOG_DATA } from '@angular/material/dialog';
import { TimePlanningPnWorkingHoursService } from '../../../../services';
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
  let mockToastrService: jest.Mocked<ToastrService>;

  beforeEach(async () => {
    // Mock URL.createObjectURL and URL.revokeObjectURL for file-saver
    global.URL.createObjectURL = jest.fn(() => 'mock-url');
    global.URL.revokeObjectURL = jest.fn();
    
    // Mock HTMLAnchorElement.prototype.click to prevent navigation errors
    HTMLAnchorElement.prototype.click = jest.fn();
    
    mockWorkingHoursService = {
      downloadReport: jest.fn(),
      downloadReportAllWorkers: jest.fn(),
    } as any;
    mockToastrService = {
      error: jest.fn(),
      success: jest.fn(),
    } as any;

    await TestBed.configureTestingModule({
      declarations: [DownloadExcelDialogComponent],
      imports: [CommonModule, FormsModule, TranslateModule.forRoot()],
      schemas: [NO_ERRORS_SCHEMA],
      providers: [
        { provide: MAT_DIALOG_DATA, useValue: [] },
        { provide: TimePlanningPnWorkingHoursService, useValue: mockWorkingHoursService },
        { provide: ToastrService, useValue: mockToastrService }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(DownloadExcelDialogComponent);
    component = fixture.componentInstance;
  });

  it('should create', () => {
    expect(component).toBeTruthy();
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
        dateTo: '2024-01-21'
      });
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

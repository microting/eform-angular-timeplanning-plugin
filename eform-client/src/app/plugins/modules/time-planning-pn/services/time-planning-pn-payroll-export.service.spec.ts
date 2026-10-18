import {describe, it, expect, beforeEach, afterEach, vi} from 'vitest';
import type {Mocked} from 'vitest';
import { TestBed } from '@angular/core/testing';
import { HttpClientTestingModule, HttpTestingController } from '@angular/common/http/testing';
import { TimePlanningPnPayrollExportService } from './time-planning-pn-payroll-export.service';
import { ApiBaseService } from 'src/app/common/services';
import { firstValueFrom, of } from 'rxjs';

describe('TimePlanningPnPayrollExportService', () => {
  let service: TimePlanningPnPayrollExportService;
  let mockApiBaseService: Mocked<ApiBaseService>;
  let httpTestingController: HttpTestingController;

  beforeEach(() => {
    mockApiBaseService = {
      get: vi.fn(),
      post: vi.fn(),
      put: vi.fn(),
    } as any;

    TestBed.configureTestingModule({
      imports: [HttpClientTestingModule],
      providers: [
        TimePlanningPnPayrollExportService,
        { provide: ApiBaseService, useValue: mockApiBaseService },
      ],
    });

    service = TestBed.inject(TimePlanningPnPayrollExportService);
    httpTestingController = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpTestingController.verify();
  });

  it('should be created', () => {
    expect(service).toBeTruthy();
  });

  describe('preview', () => {
    it('should call correct endpoint with start and end params', async () => {
      const mockResponse = { success: true, model: { rows: [], alreadyExportedAt: null } };
      mockApiBaseService.get.mockReturnValue(of(mockResponse as any));

      const result = await firstValueFrom(service.preview('2026-03-20', '2026-04-19'));
      expect(result).toEqual(mockResponse as any);

      expect(mockApiBaseService.get).toHaveBeenCalledWith(
        'api/time-planning-pn/payroll/preview?start=2026-03-20&end=2026-04-19'
      );
    });

    it('should pass different date ranges correctly', async () => {
      const mockResponse = { success: true, model: null };
      mockApiBaseService.get.mockReturnValue(of(mockResponse as any));

      const result = await firstValueFrom(service.preview('2026-01-01', '2026-01-31'));
      expect(result).toEqual(mockResponse as any);

      expect(mockApiBaseService.get).toHaveBeenCalledWith(
        'api/time-planning-pn/payroll/preview?start=2026-01-01&end=2026-01-31'
      );
    });
  });

  describe('exportPayroll', () => {
    it('should POST to correct endpoint with period body', () => {
      service.exportPayroll('2026-03-20', '2026-04-19').subscribe();

      const req = httpTestingController.expectOne('api/time-planning-pn/payroll/export');
      expect(req.request.method).toBe('POST');
      expect(req.request.body).toEqual({
        periodStart: '2026-03-20',
        periodEnd: '2026-04-19',
      });
      expect(req.request.responseType).toBe('blob');

      req.flush(new Blob(['csv-data'], { type: 'text/csv' }));
    });

    it('should request blob response type', () => {
      service.exportPayroll('2026-01-01', '2026-01-31').subscribe();

      const req = httpTestingController.expectOne('api/time-planning-pn/payroll/export');
      expect(req.request.responseType).toBe('blob');

      req.flush(new Blob());
    });
  });
});

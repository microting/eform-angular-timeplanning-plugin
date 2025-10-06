import { TestBed } from '@angular/core/testing';
import { TimePlanningPnPlanningsService } from './time-planning-pn-plannings.service';
import { ApiBaseService } from 'src/app/common/services';
import { of } from 'rxjs';

describe('TimePlanningPnPlanningsService', () => {
  let service: TimePlanningPnPlanningsService;
  let mockApiBaseService: jasmine.SpyObj<ApiBaseService>;

  beforeEach(() => {
    mockApiBaseService = jasmine.createSpyObj('ApiBaseService', ['post', 'put', 'get']);

    TestBed.configureTestingModule({
      providers: [
        TimePlanningPnPlanningsService,
        { provide: ApiBaseService, useValue: mockApiBaseService }
      ]
    });

    service = TestBed.inject(TimePlanningPnPlanningsService);
  });

  it('should be created', () => {
    expect(service).toBeTruthy();
  });

  describe('getPlannings', () => {
    it('should call apiBaseService.post with correct parameters', () => {
      const mockRequest = {
        dateFrom: '2024-01-01',
        dateTo: '2024-01-07',
        sort: 'Date',
        isSortDsc: true,
        siteId: 1,
        showResignedSites: false
      };
      const mockResponse = { success: true, model: [] };
      mockApiBaseService.post.and.returnValue(of(mockResponse as any));

      service.getPlannings(mockRequest).subscribe(result => {
        expect(result).toEqual(mockResponse as any);
      });

      expect(mockApiBaseService.post).toHaveBeenCalledWith(
        'api/time-planning-pn/plannings/index',
        mockRequest
      );
    });

    it('should handle empty response', () => {
      const mockRequest = {
        dateFrom: '2024-01-01',
        dateTo: '2024-01-07',
        sort: 'Date',
        isSortDsc: true,
        siteId: 0,
        showResignedSites: false
      };
      const mockResponse = { success: true, model: [] };
      mockApiBaseService.post.and.returnValue(of(mockResponse as any));

      service.getPlannings(mockRequest).subscribe(result => {
        expect(result.model).toEqual([]);
      });
    });
  });

  describe('updatePlanning', () => {
    it('should call apiBaseService.put with correct parameters', () => {
      const mockPlanningModel = {
        id: 123,
        planHours: 8,
        message: 1,
        planText: 'Test planning'
      } as any;
      const mockResponse = { success: true };
      mockApiBaseService.put.and.returnValue(of(mockResponse as any));

      service.updatePlanning(mockPlanningModel, 123).subscribe(result => {
        expect(result).toEqual(mockResponse as any);
      });

      expect(mockApiBaseService.put).toHaveBeenCalledWith(
        'api/time-planning-pn/plannings/123',
        mockPlanningModel
      );
    });

    it('should construct correct URL with id parameter', () => {
      const mockPlanningModel = { id: 456 } as any;
      const mockResponse = { success: true };
      mockApiBaseService.put.and.returnValue(of(mockResponse as any));

      service.updatePlanning(mockPlanningModel, 456).subscribe();

      expect(mockApiBaseService.put).toHaveBeenCalledWith(
        'api/time-planning-pn/plannings/456',
        mockPlanningModel
      );
    });
  });
});

import { TimePlanningPnAbsenceRequestsService } from './time-planning-pn-absence-requests.service';
import { ApiBaseService } from 'src/app/common/services';
import { of } from 'rxjs';
import { AbsenceRequestModel, AbsenceRequestDecisionModel } from '../models';

describe('TimePlanningPnAbsenceRequestsService', () => {
  let service: TimePlanningPnAbsenceRequestsService;
  let mockApiBaseService: jest.Mocked<ApiBaseService>;

  beforeEach(() => {
    mockApiBaseService = {
      post: jest.fn(),
      get: jest.fn(),
    } as any;

    service = new TimePlanningPnAbsenceRequestsService(mockApiBaseService);
  });

  it('should be created', () => {
    expect(service).toBeTruthy();
  });

  describe('getInbox', () => {
    it('should call apiBaseService.get with correct parameters', (done) => {
      const managerSdkSitId = 1;
      const mockResponse = { success: true, model: [] as AbsenceRequestModel[] };
      mockApiBaseService.get.mockReturnValue(of(mockResponse as any));

      service.getInbox(managerSdkSitId).subscribe(result => {
        expect(result).toEqual(mockResponse as any);
        done();
      });

      expect(mockApiBaseService.get).toHaveBeenCalledWith(
        'api/time-planning-pn/absence-requests/inbox',
        { managerSdkSitId }
      );
    });

    it('should handle empty response', (done) => {
      const managerSdkSitId = 1;
      const mockResponse = { success: true, model: [] as AbsenceRequestModel[] };
      mockApiBaseService.get.mockReturnValue(of(mockResponse as any));

      service.getInbox(managerSdkSitId).subscribe(result => {
        expect(result.model).toEqual([]);
        done();
      });
    });
  });

  describe('getMine', () => {
    it('should call apiBaseService.get with correct parameters', (done) => {
      const requestedBySdkSitId = 2;
      const mockResponse = { success: true, model: [] as AbsenceRequestModel[] };
      mockApiBaseService.get.mockReturnValue(of(mockResponse as any));

      service.getMine(requestedBySdkSitId).subscribe(result => {
        expect(result).toEqual(mockResponse as any);
        done();
      });

      expect(mockApiBaseService.get).toHaveBeenCalledWith(
        'api/time-planning-pn/absence-requests/mine',
        { requestedBySdkSitId }
      );
    });
  });

  describe('approve', () => {
    it('should call apiBaseService.post with correct parameters', (done) => {
      const requestId = 123;
      const decisionModel: AbsenceRequestDecisionModel = {
        managerSdkSitId: 1,
        decisionComment: 'Approved'
      };
      const mockResponse = { success: true };
      mockApiBaseService.post.mockReturnValue(of(mockResponse as any));

      service.approve(requestId, decisionModel).subscribe(result => {
        expect(result).toEqual(mockResponse as any);
        done();
      });

      expect(mockApiBaseService.post).toHaveBeenCalledWith(
        'api/time-planning-pn/absence-requests/123/approve',
        decisionModel
      );
    });

    it('should construct correct URL with id parameter', () => {
      const requestId = 456;
      const decisionModel: AbsenceRequestDecisionModel = {
        managerSdkSitId: 1
      };
      const mockResponse = { success: true };
      mockApiBaseService.post.mockReturnValue(of(mockResponse as any));

      service.approve(requestId, decisionModel).subscribe();

      expect(mockApiBaseService.post).toHaveBeenCalledWith(
        'api/time-planning-pn/absence-requests/456/approve',
        decisionModel
      );
    });
  });

  describe('reject', () => {
    it('should call apiBaseService.post with correct parameters', (done) => {
      const requestId = 789;
      const decisionModel: AbsenceRequestDecisionModel = {
        managerSdkSitId: 1,
        decisionComment: 'Rejected due to staffing'
      };
      const mockResponse = { success: true };
      mockApiBaseService.post.mockReturnValue(of(mockResponse as any));

      service.reject(requestId, decisionModel).subscribe(result => {
        expect(result).toEqual(mockResponse as any);
        done();
      });

      expect(mockApiBaseService.post).toHaveBeenCalledWith(
        'api/time-planning-pn/absence-requests/789/reject',
        decisionModel
      );
    });

    it('should handle rejection without comment', (done) => {
      const requestId = 111;
      const decisionModel: AbsenceRequestDecisionModel = {
        managerSdkSitId: 1
      };
      const mockResponse = { success: true };
      mockApiBaseService.post.mockReturnValue(of(mockResponse as any));

      service.reject(requestId, decisionModel).subscribe(result => {
        expect(result.success).toBe(true);
        done();
      });

      expect(mockApiBaseService.post).toHaveBeenCalledWith(
        'api/time-planning-pn/absence-requests/111/reject',
        decisionModel
      );
    });
  });
});

import { TimePlanningPnBreakPoliciesService } from './time-planning-pn-break-policies.service';
import { ApiBaseService } from 'src/app/common/services';
import { of } from 'rxjs';

describe('TimePlanningPnBreakPoliciesService', () => {
  let service: TimePlanningPnBreakPoliciesService;
  let mockApiBaseService: jest.Mocked<ApiBaseService>;

  beforeEach(() => {
    mockApiBaseService = {
      get: jest.fn(),
      post: jest.fn(),
      put: jest.fn(),
      delete: jest.fn(),
    } as any;

    service = new TimePlanningPnBreakPoliciesService(mockApiBaseService);
  });

  it('should be created', () => {
    expect(service).toBeTruthy();
  });

  describe('getBreakPolicies', () => {
    it('should call apiBaseService.get with correct parameters', (done) => {
      const mockRequest = {
        offset: 0,
        pageSize: 10,
      };
      const mockResponse = { success: true, model: { total: 0, breakPolicies: [] } };
      mockApiBaseService.get.mockReturnValue(of(mockResponse as any));

      service.getBreakPolicies(mockRequest).subscribe(result => {
        expect(result).toEqual(mockResponse as any);
        done();
      });

      expect(mockApiBaseService.get).toHaveBeenCalledWith(
        'api/time-planning-pn/break-policies',
        { offset: '0', pageSize: '10' }
      );
    });

    it('should handle pagination', (done) => {
      const mockRequest = {
        offset: 20,
        pageSize: 50,
      };
      const mockResponse = { success: true, model: { total: 100, breakPolicies: [] } };
      mockApiBaseService.get.mockReturnValue(of(mockResponse as any));

      service.getBreakPolicies(mockRequest).subscribe(result => {
        expect(result.model.total).toEqual(100);
        done();
      });

      expect(mockApiBaseService.get).toHaveBeenCalledWith(
        'api/time-planning-pn/break-policies',
        { offset: '20', pageSize: '50' }
      );
    });
  });

  describe('getBreakPolicy', () => {
    it('should call apiBaseService.get with correct id', (done) => {
      const mockId = 123;
      const mockResponse = { 
        success: true, 
        model: { id: 123, name: 'Test Policy', rules: [] }
      };
      mockApiBaseService.get.mockReturnValue(of(mockResponse as any));

      service.getBreakPolicy(mockId).subscribe(result => {
        expect(result).toEqual(mockResponse as any);
        done();
      });

      expect(mockApiBaseService.get).toHaveBeenCalledWith(
        'api/time-planning-pn/break-policies/123'
      );
    });
  });

  describe('createBreakPolicy', () => {
    it('should call apiBaseService.post with correct parameters', (done) => {
      const mockModel = {
        name: 'New Policy',
        rules: []
      };
      const mockResponse = { success: true };
      mockApiBaseService.post.mockReturnValue(of(mockResponse as any));

      service.createBreakPolicy(mockModel).subscribe(result => {
        expect(result).toEqual(mockResponse as any);
        done();
      });

      expect(mockApiBaseService.post).toHaveBeenCalledWith(
        'api/time-planning-pn/break-policies',
        mockModel
      );
    });
  });

  describe('updateBreakPolicy', () => {
    it('should call apiBaseService.put with correct parameters', (done) => {
      const mockModel = {
        id: 123,
        name: 'Updated Policy',
        rules: []
      };
      const mockResponse = { success: true };
      mockApiBaseService.put.mockReturnValue(of(mockResponse as any));

      service.updateBreakPolicy(mockModel).subscribe(result => {
        expect(result).toEqual(mockResponse as any);
        done();
      });

      expect(mockApiBaseService.put).toHaveBeenCalledWith(
        'api/time-planning-pn/break-policies/123',
        mockModel
      );
    });

    it('should construct correct URL with id parameter', () => {
      const mockModel = { id: 456, name: 'Test', rules: [] };
      const mockResponse = { success: true };
      mockApiBaseService.put.mockReturnValue(of(mockResponse as any));

      service.updateBreakPolicy(mockModel).subscribe();

      expect(mockApiBaseService.put).toHaveBeenCalledWith(
        'api/time-planning-pn/break-policies/456',
        mockModel
      );
    });
  });

  describe('deleteBreakPolicy', () => {
    it('should call apiBaseService.delete with correct id', (done) => {
      const mockId = 123;
      const mockResponse = { success: true };
      mockApiBaseService.delete.mockReturnValue(of(mockResponse as any));

      service.deleteBreakPolicy(mockId).subscribe(result => {
        expect(result).toEqual(mockResponse as any);
        done();
      });

      expect(mockApiBaseService.delete).toHaveBeenCalledWith(
        'api/time-planning-pn/break-policies/123'
      );
    });

    it('should handle different id values', () => {
      const mockResponse = { success: true };
      mockApiBaseService.delete.mockReturnValue(of(mockResponse as any));

      service.deleteBreakPolicy(789).subscribe();

      expect(mockApiBaseService.delete).toHaveBeenCalledWith(
        'api/time-planning-pn/break-policies/789'
      );
    });
  });
});

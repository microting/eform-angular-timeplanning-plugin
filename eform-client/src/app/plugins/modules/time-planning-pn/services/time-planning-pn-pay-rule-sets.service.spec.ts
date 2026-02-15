import { TimePlanningPnPayRuleSetsService } from './time-planning-pn-pay-rule-sets.service';
import { ApiBaseService } from 'src/app/common/services';
import { of } from 'rxjs';

describe('TimePlanningPnPayRuleSetsService', () => {
  let service: TimePlanningPnPayRuleSetsService;
  let mockApiBaseService: jest.Mocked<ApiBaseService>;

  beforeEach(() => {
    mockApiBaseService = {
      get: jest.fn(),
      post: jest.fn(),
      put: jest.fn(),
      delete: jest.fn(),
    } as any;

    service = new TimePlanningPnPayRuleSetsService(mockApiBaseService);
  });

  it('should be created', () => {
    expect(service).toBeTruthy();
  });

  describe('getPayRuleSets', () => {
    it('should call apiBaseService.get with correct parameters', (done) => {
      const mockRequest = { offset: 0, pageSize: 10 };
      const mockResponse = { success: true, model: { total: 0, payRuleSets: [] } };
      mockApiBaseService.get.mockReturnValue(of(mockResponse as any));

      service.getPayRuleSets(mockRequest).subscribe(result => {
        expect(result).toEqual(mockResponse as any);
        done();
      });

      expect(mockApiBaseService.get).toHaveBeenCalledWith(
        'api/time-planning-pn/pay-rule-sets',
        { offset: '0', pageSize: '10' }
      );
    });
  });

  describe('getPayRuleSet', () => {
    it('should call apiBaseService.get with correct id', (done) => {
      const mockResponse = { success: true, model: { id: 123, name: 'Test', payDayRules: [] } };
      mockApiBaseService.get.mockReturnValue(of(mockResponse as any));

      service.getPayRuleSet(123).subscribe(result => {
        expect(result).toEqual(mockResponse as any);
        done();
      });

      expect(mockApiBaseService.get).toHaveBeenCalledWith('api/time-planning-pn/pay-rule-sets/123');
    });
  });

  describe('createPayRuleSet', () => {
    it('should call apiBaseService.post with correct parameters', (done) => {
      const mockModel = { name: 'New RuleSet', payDayRules: [] };
      const mockResponse = { success: true };
      mockApiBaseService.post.mockReturnValue(of(mockResponse as any));

      service.createPayRuleSet(mockModel).subscribe(result => {
        expect(result).toEqual(mockResponse as any);
        done();
      });

      expect(mockApiBaseService.post).toHaveBeenCalledWith('api/time-planning-pn/pay-rule-sets', mockModel);
    });
  });

  describe('updatePayRuleSet', () => {
    it('should call apiBaseService.put with correct parameters', (done) => {
      const mockModel = { id: 123, name: 'Updated', payDayRules: [] };
      const mockResponse = { success: true };
      mockApiBaseService.put.mockReturnValue(of(mockResponse as any));

      service.updatePayRuleSet(mockModel).subscribe(result => {
        expect(result).toEqual(mockResponse as any);
        done();
      });

      expect(mockApiBaseService.put).toHaveBeenCalledWith('api/time-planning-pn/pay-rule-sets/123', mockModel);
    });
  });

  describe('deletePayRuleSet', () => {
    it('should call apiBaseService.delete with correct id', (done) => {
      const mockResponse = { success: true };
      mockApiBaseService.delete.mockReturnValue(of(mockResponse as any));

      service.deletePayRuleSet(123).subscribe(result => {
        expect(result).toEqual(mockResponse as any);
        done();
      });

      expect(mockApiBaseService.delete).toHaveBeenCalledWith('api/time-planning-pn/pay-rule-sets/123');
    });
  });
});

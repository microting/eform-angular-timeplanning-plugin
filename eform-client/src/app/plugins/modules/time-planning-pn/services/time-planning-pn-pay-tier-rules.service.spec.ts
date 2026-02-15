import { TimePlanningPnPayTierRulesService } from './time-planning-pn-pay-tier-rules.service';
import { ApiBaseService } from 'src/app/common/services';
import { of } from 'rxjs';

describe('TimePlanningPnPayTierRulesService', () => {
  let service: TimePlanningPnPayTierRulesService;
  let mockApiBaseService: jest.Mocked<ApiBaseService>;

  beforeEach(() => {
    mockApiBaseService = {
      get: jest.fn(),
      post: jest.fn(),
      put: jest.fn(),
      delete: jest.fn(),
    } as any;

    service = new TimePlanningPnPayTierRulesService(mockApiBaseService);
  });

  it('should be created', () => {
    expect(service).toBeTruthy();
  });

  describe('getPayTierRules', () => {
    it('should call apiBaseService.get with correct parameters', (done) => {
      const mockRequest = { offset: 0, pageSize: 10 };
      const mockResponse = { success: true, model: { total: 0, payTierRules: [] } };
      mockApiBaseService.get.mockReturnValue(of(mockResponse as any));

      service.getPayTierRules(mockRequest).subscribe(result => {
        expect(result).toEqual(mockResponse as any);
        done();
      });

      expect(mockApiBaseService.get).toHaveBeenCalledWith(
        'api/time-planning-pn/pay-tier-rules',
        { offset: '0', pageSize: '10' }
      );
    });

    it('should include optional payDayRuleId filter', (done) => {
      const mockRequest = { offset: 0, pageSize: 10, payDayRuleId: 3 };
      const mockResponse = { success: true, model: { total: 0, payTierRules: [] } };
      mockApiBaseService.get.mockReturnValue(of(mockResponse as any));

      service.getPayTierRules(mockRequest).subscribe(result => {
        done();
      });

      expect(mockApiBaseService.get).toHaveBeenCalledWith(
        'api/time-planning-pn/pay-tier-rules',
        { offset: '0', pageSize: '10', payDayRuleId: '3' }
      );
    });
  });

  describe('getPayTierRule', () => {
    it('should call apiBaseService.get with correct id', (done) => {
      const mockResponse = { success: true, model: { id: 123, payDayRuleId: 1, order: 1, upToSeconds: 3600, payCode: 'CODE1' } };
      mockApiBaseService.get.mockReturnValue(of(mockResponse as any));

      service.getPayTierRule(123).subscribe(result => {
        expect(result).toEqual(mockResponse as any);
        done();
      });

      expect(mockApiBaseService.get).toHaveBeenCalledWith('api/time-planning-pn/pay-tier-rules/123');
    });
  });

  describe('createPayTierRule', () => {
    it('should call apiBaseService.post with correct parameters', (done) => {
      const mockModel = { payDayRuleId: 1, order: 1, upToSeconds: 3600, payCode: 'CODE1' };
      const mockResponse = { success: true };
      mockApiBaseService.post.mockReturnValue(of(mockResponse as any));

      service.createPayTierRule(mockModel).subscribe(result => {
        expect(result).toEqual(mockResponse as any);
        done();
      });

      expect(mockApiBaseService.post).toHaveBeenCalledWith('api/time-planning-pn/pay-tier-rules', mockModel);
    });
  });

  describe('updatePayTierRule', () => {
    it('should call apiBaseService.put with correct parameters', (done) => {
      const mockModel = { id: 123, payDayRuleId: 1, order: 2, upToSeconds: 7200, payCode: 'CODE2' };
      const mockResponse = { success: true };
      mockApiBaseService.put.mockReturnValue(of(mockResponse as any));

      service.updatePayTierRule(mockModel).subscribe(result => {
        expect(result).toEqual(mockResponse as any);
        done();
      });

      expect(mockApiBaseService.put).toHaveBeenCalledWith('api/time-planning-pn/pay-tier-rules/123', mockModel);
    });
  });

  describe('deletePayTierRule', () => {
    it('should call apiBaseService.delete with correct id', (done) => {
      const mockResponse = { success: true };
      mockApiBaseService.delete.mockReturnValue(of(mockResponse as any));

      service.deletePayTierRule(123).subscribe(result => {
        expect(result).toEqual(mockResponse as any);
        done();
      });

      expect(mockApiBaseService.delete).toHaveBeenCalledWith('api/time-planning-pn/pay-tier-rules/123');
    });
  });
});

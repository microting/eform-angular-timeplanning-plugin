import { TimePlanningPnPayDayTypeRulesService } from './time-planning-pn-pay-day-type-rules.service';
import { ApiBaseService } from 'src/app/common/services';
import { of } from 'rxjs';

describe('TimePlanningPnPayDayTypeRulesService', () => {
  let service: TimePlanningPnPayDayTypeRulesService;
  let mockApiBaseService: jest.Mocked<ApiBaseService>;

  beforeEach(() => {
    mockApiBaseService = {
      get: jest.fn(),
      post: jest.fn(),
      put: jest.fn(),
      delete: jest.fn(),
    } as any;

    service = new TimePlanningPnPayDayTypeRulesService(mockApiBaseService);
  });

  it('should be created', () => {
    expect(service).toBeTruthy();
  });

  describe('getPayDayTypeRules', () => {
    it('should call apiBaseService.get with correct parameters', (done) => {
      const mockRequest = { offset: 0, pageSize: 10 };
      const mockResponse = { success: true, model: { total: 0, payDayTypeRules: [] } };
      mockApiBaseService.get.mockReturnValue(of(mockResponse as any));

      service.getPayDayTypeRules(mockRequest).subscribe(result => {
        expect(result).toEqual(mockResponse as any);
        done();
      });

      expect(mockApiBaseService.get).toHaveBeenCalledWith(
        'api/time-planning-pn/pay-day-type-rules',
        { offset: '0', pageSize: '10' }
      );
    });

    it('should include optional payRuleSetId filter', (done) => {
      const mockRequest = { offset: 0, pageSize: 10, payRuleSetId: 5 };
      const mockResponse = { success: true, model: { total: 0, payDayTypeRules: [] } };
      mockApiBaseService.get.mockReturnValue(of(mockResponse as any));

      service.getPayDayTypeRules(mockRequest).subscribe(result => {
        done();
      });

      expect(mockApiBaseService.get).toHaveBeenCalledWith(
        'api/time-planning-pn/pay-day-type-rules',
        { offset: '0', pageSize: '10', payRuleSetId: '5' }
      );
    });
  });

  describe('getPayDayTypeRule', () => {
    it('should call apiBaseService.get with correct id', (done) => {
      const mockResponse = { success: true, model: { id: 123, payRuleSetId: 1, dayType: 'Weekday' } };
      mockApiBaseService.get.mockReturnValue(of(mockResponse as any));

      service.getPayDayTypeRule(123).subscribe(result => {
        expect(result).toEqual(mockResponse as any);
        done();
      });

      expect(mockApiBaseService.get).toHaveBeenCalledWith('api/time-planning-pn/pay-day-type-rules/123');
    });
  });

  describe('createPayDayTypeRule', () => {
    it('should call apiBaseService.post with correct parameters', (done) => {
      const mockModel = { payRuleSetId: 1, dayType: 'Weekday' };
      const mockResponse = { success: true };
      mockApiBaseService.post.mockReturnValue(of(mockResponse as any));

      service.createPayDayTypeRule(mockModel).subscribe(result => {
        expect(result).toEqual(mockResponse as any);
        done();
      });

      expect(mockApiBaseService.post).toHaveBeenCalledWith('api/time-planning-pn/pay-day-type-rules', mockModel);
    });
  });

  describe('updatePayDayTypeRule', () => {
    it('should call apiBaseService.put with correct parameters', (done) => {
      const mockModel = { id: 123, payRuleSetId: 1, dayType: 'Weekend' };
      const mockResponse = { success: true };
      mockApiBaseService.put.mockReturnValue(of(mockResponse as any));

      service.updatePayDayTypeRule(mockModel).subscribe(result => {
        expect(result).toEqual(mockResponse as any);
        done();
      });

      expect(mockApiBaseService.put).toHaveBeenCalledWith('api/time-planning-pn/pay-day-type-rules/123', mockModel);
    });
  });

  describe('deletePayDayTypeRule', () => {
    it('should call apiBaseService.delete with correct id', (done) => {
      const mockResponse = { success: true };
      mockApiBaseService.delete.mockReturnValue(of(mockResponse as any));

      service.deletePayDayTypeRule(123).subscribe(result => {
        expect(result).toEqual(mockResponse as any);
        done();
      });

      expect(mockApiBaseService.delete).toHaveBeenCalledWith('api/time-planning-pn/pay-day-type-rules/123');
    });
  });
});

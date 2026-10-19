import {describe, it, expect, beforeEach, vi} from 'vitest';
import type {Mocked} from 'vitest';
import { TimePlanningPnPayTierRulesService } from './time-planning-pn-pay-tier-rules.service';
import { ApiBaseService } from 'src/app/common/services';
import { firstValueFrom, of } from 'rxjs';

describe('TimePlanningPnPayTierRulesService', () => {
  let service: TimePlanningPnPayTierRulesService;
  let mockApiBaseService: Mocked<ApiBaseService>;

  beforeEach(() => {
    mockApiBaseService = {
      get: vi.fn(),
      post: vi.fn(),
      put: vi.fn(),
      delete: vi.fn(),
    } as any;

    service = new TimePlanningPnPayTierRulesService(mockApiBaseService);
  });

  it('should be created', () => {
    expect(service).toBeTruthy();
  });

  describe('getPayTierRules', () => {
    it('should call apiBaseService.get with correct parameters', async () => {
      const mockRequest = { offset: 0, pageSize: 10 };
      const mockResponse = { success: true, model: { total: 0, payTierRules: [] } };
      mockApiBaseService.get.mockReturnValue(of(mockResponse as any));

      const result = await firstValueFrom(service.getPayTierRules(mockRequest));
      expect(result).toEqual(mockResponse as any);

      expect(mockApiBaseService.get).toHaveBeenCalledWith(
        'api/time-planning-pn/pay-tier-rules',
        { offset: '0', pageSize: '10' }
      );
    });

    it('should include optional payDayRuleId filter', async () => {
      const mockRequest = { offset: 0, pageSize: 10, payDayRuleId: 3 };
      const mockResponse = { success: true, model: { total: 0, payTierRules: [] } };
      mockApiBaseService.get.mockReturnValue(of(mockResponse as any));

      await firstValueFrom(service.getPayTierRules(mockRequest));

      expect(mockApiBaseService.get).toHaveBeenCalledWith(
        'api/time-planning-pn/pay-tier-rules',
        { offset: '0', pageSize: '10', payDayRuleId: '3' }
      );
    });
  });

  describe('getPayTierRule', () => {
    it('should call apiBaseService.get with correct id', async () => {
      const mockResponse = { success: true, model: { id: 123, payDayRuleId: 1, order: 1, upToSeconds: 3600, payCode: 'CODE1' } };
      mockApiBaseService.get.mockReturnValue(of(mockResponse as any));

      const result = await firstValueFrom(service.getPayTierRule(123));
      expect(result).toEqual(mockResponse as any);

      expect(mockApiBaseService.get).toHaveBeenCalledWith('api/time-planning-pn/pay-tier-rules/123');
    });
  });

  describe('createPayTierRule', () => {
    it('should call apiBaseService.post with correct parameters', async () => {
      const mockModel = { payDayRuleId: 1, order: 1, upToSeconds: 3600, payCode: 'CODE1' };
      const mockResponse = { success: true };
      mockApiBaseService.post.mockReturnValue(of(mockResponse as any));

      const result = await firstValueFrom(service.createPayTierRule(mockModel));
      expect(result).toEqual(mockResponse as any);

      expect(mockApiBaseService.post).toHaveBeenCalledWith('api/time-planning-pn/pay-tier-rules', mockModel);
    });
  });

  describe('updatePayTierRule', () => {
    it('should call apiBaseService.put with correct parameters', async () => {
      const mockModel = { id: 123, payDayRuleId: 1, order: 2, upToSeconds: 7200, payCode: 'CODE2' };
      const mockResponse = { success: true };
      mockApiBaseService.put.mockReturnValue(of(mockResponse as any));

      const result = await firstValueFrom(service.updatePayTierRule(mockModel));
      expect(result).toEqual(mockResponse as any);

      expect(mockApiBaseService.put).toHaveBeenCalledWith('api/time-planning-pn/pay-tier-rules/123', mockModel);
    });
  });

  describe('deletePayTierRule', () => {
    it('should call apiBaseService.delete with correct id', async () => {
      const mockResponse = { success: true };
      mockApiBaseService.delete.mockReturnValue(of(mockResponse as any));

      const result = await firstValueFrom(service.deletePayTierRule(123));
      expect(result).toEqual(mockResponse as any);

      expect(mockApiBaseService.delete).toHaveBeenCalledWith('api/time-planning-pn/pay-tier-rules/123');
    });
  });
});

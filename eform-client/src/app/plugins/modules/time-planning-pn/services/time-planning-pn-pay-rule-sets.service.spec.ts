import {describe, it, expect, beforeEach, vi} from 'vitest';
import type {Mocked} from 'vitest';
import { TimePlanningPnPayRuleSetsService } from './time-planning-pn-pay-rule-sets.service';
import { ApiBaseService } from 'src/app/common/services';
import { firstValueFrom, of } from 'rxjs';

describe('TimePlanningPnPayRuleSetsService', () => {
  let service: TimePlanningPnPayRuleSetsService;
  let mockApiBaseService: Mocked<ApiBaseService>;

  beforeEach(() => {
    mockApiBaseService = {
      get: vi.fn(),
      post: vi.fn(),
      put: vi.fn(),
      delete: vi.fn(),
    } as any;

    service = new TimePlanningPnPayRuleSetsService(mockApiBaseService);
  });

  it('should be created', () => {
    expect(service).toBeTruthy();
  });

  describe('getPayRuleSets', () => {
    it('should call apiBaseService.get with correct parameters', async () => {
      const mockRequest = { offset: 0, pageSize: 10 };
      const mockResponse = { success: true, model: { total: 0, payRuleSets: [] } };
      mockApiBaseService.get.mockReturnValue(of(mockResponse as any));

      const result = await firstValueFrom(service.getPayRuleSets(mockRequest));
      expect(result).toEqual(mockResponse as any);

      expect(mockApiBaseService.get).toHaveBeenCalledWith(
        'api/time-planning-pn/pay-rule-sets',
        { offset: '0', pageSize: '10' }
      );
    });
  });

  describe('getPayRuleSet', () => {
    it('should call apiBaseService.get with correct id', async () => {
      const mockResponse = { success: true, model: { id: 123, name: 'Test', payDayRules: [] } };
      mockApiBaseService.get.mockReturnValue(of(mockResponse as any));

      const result = await firstValueFrom(service.getPayRuleSet(123));
      expect(result).toEqual(mockResponse as any);

      expect(mockApiBaseService.get).toHaveBeenCalledWith('api/time-planning-pn/pay-rule-sets/123');
    });
  });

  describe('createPayRuleSet', () => {
    it('should call apiBaseService.post with correct parameters', async () => {
      const mockModel = { name: 'New RuleSet', payDayRules: [], payDayTypeRules: [] };
      const mockResponse = { success: true };
      mockApiBaseService.post.mockReturnValue(of(mockResponse as any));

      const result = await firstValueFrom(service.createPayRuleSet(mockModel));
      expect(result).toEqual(mockResponse as any);

      expect(mockApiBaseService.post).toHaveBeenCalledWith('api/time-planning-pn/pay-rule-sets', mockModel);
    });
  });

  describe('updatePayRuleSet', () => {
    it('should call apiBaseService.put with correct parameters', async () => {
      const mockModel = { name: 'Updated', payDayRules: [], payDayTypeRules: [] };
      const mockResponse = { success: true };
      mockApiBaseService.put.mockReturnValue(of(mockResponse as any));

      const result = await firstValueFrom(service.updatePayRuleSet(123, mockModel));
      expect(result).toEqual(mockResponse as any);

      expect(mockApiBaseService.put).toHaveBeenCalledWith('api/time-planning-pn/pay-rule-sets/123', mockModel);
    });
  });

  describe('deletePayRuleSet', () => {
    it('should call apiBaseService.delete with correct id', async () => {
      const mockResponse = { success: true };
      mockApiBaseService.delete.mockReturnValue(of(mockResponse as any));

      const result = await firstValueFrom(service.deletePayRuleSet(123));
      expect(result).toEqual(mockResponse as any);

      expect(mockApiBaseService.delete).toHaveBeenCalledWith('api/time-planning-pn/pay-rule-sets/123');
    });
  });
});

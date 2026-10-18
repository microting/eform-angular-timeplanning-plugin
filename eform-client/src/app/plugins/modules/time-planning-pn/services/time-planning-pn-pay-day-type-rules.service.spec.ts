import {describe, it, expect, beforeEach, vi} from 'vitest';
import type {Mocked} from 'vitest';
import { TimePlanningPnPayDayTypeRulesService } from './time-planning-pn-pay-day-type-rules.service';
import { ApiBaseService } from 'src/app/common/services';
import { firstValueFrom, of } from 'rxjs';

describe('TimePlanningPnPayDayTypeRulesService', () => {
  let service: TimePlanningPnPayDayTypeRulesService;
  let mockApiBaseService: Mocked<ApiBaseService>;

  beforeEach(() => {
    mockApiBaseService = {
      get: vi.fn(),
      post: vi.fn(),
      put: vi.fn(),
      delete: vi.fn(),
    } as any;

    service = new TimePlanningPnPayDayTypeRulesService(mockApiBaseService);
  });

  it('should be created', () => {
    expect(service).toBeTruthy();
  });

  describe('getPayDayTypeRules', () => {
    it('should call apiBaseService.get with correct parameters', async () => {
      const mockRequest = { offset: 0, pageSize: 10 };
      const mockResponse = { success: true, model: { total: 0, payDayTypeRules: [] } };
      mockApiBaseService.get.mockReturnValue(of(mockResponse as any));

      const result = await firstValueFrom(service.getPayDayTypeRules(mockRequest));
      expect(result).toEqual(mockResponse as any);

      expect(mockApiBaseService.get).toHaveBeenCalledWith(
        'api/time-planning-pn/pay-day-type-rules',
        { offset: '0', pageSize: '10' }
      );
    });

    it('should include optional payRuleSetId filter', async () => {
      const mockRequest = { offset: 0, pageSize: 10, payRuleSetId: 5 };
      const mockResponse = { success: true, model: { total: 0, payDayTypeRules: [] } };
      mockApiBaseService.get.mockReturnValue(of(mockResponse as any));

      await firstValueFrom(service.getPayDayTypeRules(mockRequest));

      expect(mockApiBaseService.get).toHaveBeenCalledWith(
        'api/time-planning-pn/pay-day-type-rules',
        { offset: '0', pageSize: '10', payRuleSetId: '5' }
      );
    });
  });

  describe('getPayDayTypeRule', () => {
    it('should call apiBaseService.get with correct id', async () => {
      const mockResponse = { success: true, model: { id: 123, payRuleSetId: 1, dayType: 'Weekday' } };
      mockApiBaseService.get.mockReturnValue(of(mockResponse as any));

      const result = await firstValueFrom(service.getPayDayTypeRule(123));
      expect(result).toEqual(mockResponse as any);

      expect(mockApiBaseService.get).toHaveBeenCalledWith('api/time-planning-pn/pay-day-type-rules/123');
    });
  });

  describe('createPayDayTypeRule', () => {
    it('should call apiBaseService.post with correct parameters', async () => {
      const mockModel = { payRuleSetId: 1, dayType: 'Weekday' };
      const mockResponse = { success: true };
      mockApiBaseService.post.mockReturnValue(of(mockResponse as any));

      const result = await firstValueFrom(service.createPayDayTypeRule(mockModel));
      expect(result).toEqual(mockResponse as any);

      expect(mockApiBaseService.post).toHaveBeenCalledWith('api/time-planning-pn/pay-day-type-rules', mockModel);
    });
  });

  describe('updatePayDayTypeRule', () => {
    it('should call apiBaseService.put with correct parameters', async () => {
      const mockModel = { id: 123, payRuleSetId: 1, dayType: 'Weekend' };
      const mockResponse = { success: true };
      mockApiBaseService.put.mockReturnValue(of(mockResponse as any));

      const result = await firstValueFrom(service.updatePayDayTypeRule(mockModel));
      expect(result).toEqual(mockResponse as any);

      expect(mockApiBaseService.put).toHaveBeenCalledWith('api/time-planning-pn/pay-day-type-rules/123', mockModel);
    });
  });

  describe('deletePayDayTypeRule', () => {
    it('should call apiBaseService.delete with correct id', async () => {
      const mockResponse = { success: true };
      mockApiBaseService.delete.mockReturnValue(of(mockResponse as any));

      const result = await firstValueFrom(service.deletePayDayTypeRule(123));
      expect(result).toEqual(mockResponse as any);

      expect(mockApiBaseService.delete).toHaveBeenCalledWith('api/time-planning-pn/pay-day-type-rules/123');
    });
  });
});

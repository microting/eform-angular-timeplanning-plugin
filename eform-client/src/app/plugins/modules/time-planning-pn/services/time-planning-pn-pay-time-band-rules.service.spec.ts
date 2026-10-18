import {describe, it, expect, beforeEach, vi} from 'vitest';
import type {Mocked} from 'vitest';
import { TimePlanningPnPayTimeBandRulesService } from './time-planning-pn-pay-time-band-rules.service';
import { ApiBaseService } from 'src/app/common/services';
import { of } from 'rxjs';

describe('TimePlanningPnPayTimeBandRulesService', () => {
  let service: TimePlanningPnPayTimeBandRulesService;
  let mockApiBaseService: Mocked<ApiBaseService>;

  beforeEach(() => {
    mockApiBaseService = {
      get: vi.fn(),
      post: vi.fn(),
      put: vi.fn(),
      delete: vi.fn(),
    } as any;

    service = new TimePlanningPnPayTimeBandRulesService(mockApiBaseService);
  });

  it('should be created', () => {
    expect(service).toBeTruthy();
  });

  describe('getPayTimeBandRules', () => {
    it('should call apiBaseService.get with correct parameters', () => new Promise<void>((done) => {
      const mockRequest = { offset: 0, pageSize: 10 };
      const mockResponse = { success: true, model: { total: 0, payTimeBandRules: [] } };
      mockApiBaseService.get.mockReturnValue(of(mockResponse as any));

      service.getPayTimeBandRules(mockRequest).subscribe(result => {
        expect(result).toEqual(mockResponse as any);
        done();
      });

      expect(mockApiBaseService.get).toHaveBeenCalledWith(
        'api/time-planning-pn/pay-time-band-rules',
        { offset: '0', pageSize: '10' }
      );
    }));

    it('should include optional payDayTypeRuleId filter', () => new Promise<void>((done) => {
      const mockRequest = { offset: 0, pageSize: 10, payDayTypeRuleId: 2 };
      const mockResponse = { success: true, model: { total: 0, payTimeBandRules: [] } };
      mockApiBaseService.get.mockReturnValue(of(mockResponse as any));

      service.getPayTimeBandRules(mockRequest).subscribe(result => {
        done();
      });

      expect(mockApiBaseService.get).toHaveBeenCalledWith(
        'api/time-planning-pn/pay-time-band-rules',
        { offset: '0', pageSize: '10', payDayTypeRuleId: '2' }
      );
    }));
  });

  describe('getPayTimeBandRule', () => {
    it('should call apiBaseService.get with correct id', () => new Promise<void>((done) => {
      const mockResponse = {
        success: true,
        model: { id: 123, payDayTypeRuleId: 1, startSecondOfDay: 0, endSecondOfDay: 43200, payCode: 'DAY' }
      };
      mockApiBaseService.get.mockReturnValue(of(mockResponse as any));

      service.getPayTimeBandRule(123).subscribe(result => {
        expect(result).toEqual(mockResponse as any);
        done();
      });

      expect(mockApiBaseService.get).toHaveBeenCalledWith('api/time-planning-pn/pay-time-band-rules/123');
    }));
  });

  describe('createPayTimeBandRule', () => {
    it('should call apiBaseService.post with correct parameters', () => new Promise<void>((done) => {
      const mockModel = { payDayTypeRuleId: 1, startSecondOfDay: 0, endSecondOfDay: 43200, payCode: 'DAY', priority: 1 };
      const mockResponse = { success: true };
      mockApiBaseService.post.mockReturnValue(of(mockResponse as any));

      service.createPayTimeBandRule(mockModel).subscribe(result => {
        expect(result).toEqual(mockResponse as any);
        done();
      });

      expect(mockApiBaseService.post).toHaveBeenCalledWith('api/time-planning-pn/pay-time-band-rules', mockModel);
    }));
  });

  describe('updatePayTimeBandRule', () => {
    it('should call apiBaseService.put with correct parameters', () => new Promise<void>((done) => {
      const mockModel = { id: 123, payDayTypeRuleId: 1, startSecondOfDay: 43200, endSecondOfDay: 86400, payCode: 'NIGHT', priority: 1 };
      const mockResponse = { success: true };
      mockApiBaseService.put.mockReturnValue(of(mockResponse as any));

      service.updatePayTimeBandRule(mockModel).subscribe(result => {
        expect(result).toEqual(mockResponse as any);
        done();
      });

      expect(mockApiBaseService.put).toHaveBeenCalledWith('api/time-planning-pn/pay-time-band-rules/123', mockModel);
    }));
  });

  describe('deletePayTimeBandRule', () => {
    it('should call apiBaseService.delete with correct id', () => new Promise<void>((done) => {
      const mockResponse = { success: true };
      mockApiBaseService.delete.mockReturnValue(of(mockResponse as any));

      service.deletePayTimeBandRule(123).subscribe(result => {
        expect(result).toEqual(mockResponse as any);
        done();
      });

      expect(mockApiBaseService.delete).toHaveBeenCalledWith('api/time-planning-pn/pay-time-band-rules/123');
    }));
  });
});

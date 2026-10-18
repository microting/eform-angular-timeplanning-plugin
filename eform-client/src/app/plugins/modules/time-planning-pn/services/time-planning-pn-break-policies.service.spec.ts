import {describe, it, expect, beforeEach, vi} from 'vitest';
import type {Mocked} from 'vitest';
import { TimePlanningPnBreakPoliciesService } from './time-planning-pn-break-policies.service';
import { ApiBaseService } from 'src/app/common/services';
import { firstValueFrom, of } from 'rxjs';

describe('TimePlanningPnBreakPoliciesService', () => {
  let service: TimePlanningPnBreakPoliciesService;
  let mockApiBaseService: Mocked<ApiBaseService>;

  beforeEach(() => {
    mockApiBaseService = {
      get: vi.fn(),
      post: vi.fn(),
      put: vi.fn(),
      delete: vi.fn(),
    } as any;

    service = new TimePlanningPnBreakPoliciesService(mockApiBaseService);
  });

  it('should be created', () => {
    expect(service).toBeTruthy();
  });

  describe('getBreakPolicies', () => {
    it('should call apiBaseService.get with correct parameters', async () => {
      const mockRequest = {
        offset: 0,
        pageSize: 10,
      };
      const mockResponse = { success: true, model: { total: 0, breakPolicies: [] } };
      mockApiBaseService.get.mockReturnValue(of(mockResponse as any));

      const result = await firstValueFrom(service.getBreakPolicies(mockRequest));
      expect(result).toEqual(mockResponse as any);

      expect(mockApiBaseService.get).toHaveBeenCalledWith(
        'api/time-planning-pn/break-policies',
        { offset: '0', pageSize: '10' }
      );
    });

    it('should handle pagination', async () => {
      const mockRequest = {
        offset: 20,
        pageSize: 50,
      };
      const mockResponse = { success: true, model: { total: 100, breakPolicies: [] } };
      mockApiBaseService.get.mockReturnValue(of(mockResponse as any));

      const result = await firstValueFrom(service.getBreakPolicies(mockRequest));
      expect(result.model.total).toEqual(100);

      expect(mockApiBaseService.get).toHaveBeenCalledWith(
        'api/time-planning-pn/break-policies',
        { offset: '20', pageSize: '50' }
      );
    });
  });

  describe('getBreakPolicy', () => {
    it('should call apiBaseService.get with correct id', async () => {
      const mockId = 123;
      const mockResponse = { 
        success: true, 
        model: { id: 123, name: 'Test Policy', rules: [] }
      };
      mockApiBaseService.get.mockReturnValue(of(mockResponse as any));

      const result = await firstValueFrom(service.getBreakPolicy(mockId));
      expect(result).toEqual(mockResponse as any);

      expect(mockApiBaseService.get).toHaveBeenCalledWith(
        'api/time-planning-pn/break-policies/123'
      );
    });
  });

  describe('createBreakPolicy', () => {
    it('should call apiBaseService.post with correct parameters', async () => {
      const mockModel = {
        name: 'New Policy',
        breakPolicyRules: []
      };
      const mockResponse = { success: true };
      mockApiBaseService.post.mockReturnValue(of(mockResponse as any));

      const result = await firstValueFrom(service.createBreakPolicy(mockModel));
      expect(result).toEqual(mockResponse as any);

      expect(mockApiBaseService.post).toHaveBeenCalledWith(
        'api/time-planning-pn/break-policies',
        mockModel
      );
    });
  });

  describe('updateBreakPolicy', () => {
    it('should call apiBaseService.put with correct parameters', async () => {
      const mockModel = {
        name: 'Updated Policy',
        breakPolicyRules: []
      };
      const mockResponse = { success: true };
      mockApiBaseService.put.mockReturnValue(of(mockResponse as any));

      const result = await firstValueFrom(service.updateBreakPolicy(123, mockModel));
      expect(result).toEqual(mockResponse as any);

      expect(mockApiBaseService.put).toHaveBeenCalledWith(
        'api/time-planning-pn/break-policies/123',
        mockModel
      );
    });

    it('should construct correct URL with id parameter', () => {
      const mockModel = { name: 'Test', breakPolicyRules: [] };
      const mockResponse = { success: true };
      mockApiBaseService.put.mockReturnValue(of(mockResponse as any));

      service.updateBreakPolicy(456, mockModel).subscribe();

      expect(mockApiBaseService.put).toHaveBeenCalledWith(
        'api/time-planning-pn/break-policies/456',
        mockModel
      );
    });
  });

  describe('deleteBreakPolicy', () => {
    it('should call apiBaseService.delete with correct id', async () => {
      const mockId = 123;
      const mockResponse = { success: true };
      mockApiBaseService.delete.mockReturnValue(of(mockResponse as any));

      const result = await firstValueFrom(service.deleteBreakPolicy(mockId));
      expect(result).toEqual(mockResponse as any);

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

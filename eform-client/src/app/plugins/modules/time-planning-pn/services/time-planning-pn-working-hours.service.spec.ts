import { TestBed } from '@angular/core/testing';
import { HttpClientTestingModule, HttpTestingController } from '@angular/common/http/testing';
import { TimePlanningPnWorkingHoursService } from './time-planning-pn-working-hours.service';
import { ApiBaseService } from 'src/app/common/services';
import { of } from 'rxjs';

describe('TimePlanningPnWorkingHoursService', () => {
  let service: TimePlanningPnWorkingHoursService;
  let mockApiBaseService: jest.Mocked<ApiBaseService>;
  let httpTestingController: HttpTestingController;

  beforeEach(() => {
    mockApiBaseService = {
      get: jest.fn(),
      post: jest.fn(),
      put: jest.fn(),
      getBlobData: jest.fn().mockReturnValue(of(new Blob())),
    } as any;

    TestBed.configureTestingModule({
      imports: [HttpClientTestingModule],
      providers: [
        TimePlanningPnWorkingHoursService,
        { provide: ApiBaseService, useValue: mockApiBaseService },
      ],
    });

    service = TestBed.inject(TimePlanningPnWorkingHoursService);
    httpTestingController = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpTestingController.verify();
  });

  describe('downloadReportAllWorkers', () => {
    // The whole point of this suite: a key that carries its values as "1,2" binds to an
    // empty list on the server, and the filtered export silently returns every worker.
    it('should repeat the tagIds key once per tag', () => {
      service.downloadReportAllWorkers({
        dateFrom: '2024-01-15',
        dateTo: '2024-01-21',
        tagIds: [1, 2],
      }).subscribe();

      expect(mockApiBaseService.getBlobData).toHaveBeenCalledWith(
        'api/time-planning-pn/working-hours/reports/file-all-workers?tagIds=1&tagIds=2',
        { dateFrom: '2024-01-15', dateTo: '2024-01-21' }
      );
    });

    it('should leave the url clean when no tag is selected', () => {
      service.downloadReportAllWorkers({
        dateFrom: '2024-01-15',
        dateTo: '2024-01-21',
      }).subscribe();

      expect(mockApiBaseService.getBlobData).toHaveBeenCalledWith(
        'api/time-planning-pn/working-hours/reports/file-all-workers',
        { dateFrom: '2024-01-15', dateTo: '2024-01-21' }
      );
    });
  });
});

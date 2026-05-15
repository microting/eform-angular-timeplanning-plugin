import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { OperationDataResult } from 'src/app/common/models';
import { ApiBaseService } from 'src/app/common/services';

@Injectable()
export class TimePlanningPnPayrollExportService {
  constructor(private apiBaseService: ApiBaseService, private http: HttpClient) {}

  preview(start: string, end: string): Observable<OperationDataResult<any>> {
    return this.apiBaseService.get(`api/time-planning-pn/payroll/preview?start=${start}&end=${end}`);
  }

  exportPayroll(periodStart: string, periodEnd: string) {
    return this.http.post(`api/time-planning-pn/payroll/export`,
      { periodStart, periodEnd },
      { responseType: 'blob', observe: 'response' });
  }
}

import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { OperationDataResult, OperationResult } from 'src/app/common/models';
import { ApiBaseService } from 'src/app/common/services';
import { GpsCoordinateModel } from '../models';

const TimePlanningPnGpsCoordinateMethods = {
  GpsCoordinates: 'api/time-planning-pn/gps-coordinates',
};

@Injectable({
  providedIn: 'root',
})
export class TimePlanningPnGpsCoordinatesService {
  private apiBaseService = inject(ApiBaseService);

  getByPlanRegistrationId(id: number): Observable<OperationDataResult<GpsCoordinateModel[]>> {
    return this.apiBaseService.get(
      TimePlanningPnGpsCoordinateMethods.GpsCoordinates + '?planRegistrationId=' + id
    );
  }
}

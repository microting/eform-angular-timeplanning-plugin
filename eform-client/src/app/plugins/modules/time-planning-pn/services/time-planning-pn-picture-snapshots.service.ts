import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { OperationDataResult, OperationResult } from 'src/app/common/models';
import { ApiBaseService } from 'src/app/common/services';
import { PictureSnapshotModel } from '../models';

const TimePlanningPnPictureSnapshotMethods = {
  PictureSnapshots: 'api/time-planning-pn/picture-snapshots',
};

@Injectable({
  providedIn: 'root',
})
export class TimePlanningPnPictureSnapshotsService {
  private apiBaseService = inject(ApiBaseService);

  getByPlanRegistrationId(id: number): Observable<OperationDataResult<PictureSnapshotModel[]>> {
    return this.apiBaseService.get(
      TimePlanningPnPictureSnapshotMethods.PictureSnapshots + '?planRegistrationId=' + id
    );
  }
}

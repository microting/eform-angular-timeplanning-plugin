import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import {
  CommonDictionaryModel,
  OperationDataResult,
  OperationResult,
  Paged,
} from 'src/app/common/models';
import { ApiBaseService } from 'src/app/common/services';

export let BackendConfigurationPnPropertiesMethods = {
  Properties: 'api/time-planning-pn/plannings'
};

@Injectable({
  providedIn: 'root',
})
export class TimePlanningPnPlanningsService {
  constructor(private apiBaseService: ApiBaseService) {}

  // getAllProperties(
  //   model: PropertiesRequestModel
  // ): Observable<OperationDataResult<Paged<PropertyModel>>> {
  //   return this.apiBaseService.post(
  //     BackendConfigurationPnPropertiesMethods.PropertiesIndex,
  //     model
  //   );
  // }
  //
  // updateProperty(model: PropertyUpdateModel): Observable<OperationResult> {
  //   return this.apiBaseService.put(
  //     BackendConfigurationPnPropertiesMethods.Properties,
  //     model
  //   );
  // }
}

import {Injectable} from '@angular/core';
import {ApiBaseService} from 'src/app/common/services';
import {Observable} from 'rxjs';
import {
  TimePlanningRegistrationDeviceModel
} from '../models/registration-devices/time-planning-registration-device.model';
import {OperationDataResult} from 'src/app/common/models';

export let TimePlanningPnRegistrationDevicesMethods = {
  Index: 'api/time-planning-pn/registration-device/index',
  Create: 'api/time-planning-pn/registration-device',
  Update: 'api/time-planning-pn/registration-device',
  Delete: 'api/time-planning-pn/registration-device',
  Get: 'api/time-planning-pn/registration-device'
};
@Injectable({
    providedIn: 'root'
})
export class TimePlanningPnRegistrationDevicesService {
  constructor(private apiBaseService: ApiBaseService) {}

  getRegistrationDevices(model: any): Observable<OperationDataResult<TimePlanningRegistrationDeviceModel>> {
    return this.apiBaseService.post(TimePlanningPnRegistrationDevicesMethods.Index, model);
  }

  createRegistrationDevice(model: any) {
    return this.apiBaseService.post(TimePlanningPnRegistrationDevicesMethods.Create, model);
  }

  updateRegistrationDevice(model: any) {
    return this.apiBaseService.put(TimePlanningPnRegistrationDevicesMethods.Update, model);
  }

  deleteRegistrationDevice(model: any) {
    return this.apiBaseService.delete(TimePlanningPnRegistrationDevicesMethods.Delete + '/' + model);
  }
}

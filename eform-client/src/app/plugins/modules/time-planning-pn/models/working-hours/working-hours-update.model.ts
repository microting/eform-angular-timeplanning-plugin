import {WorkingHourModel} from 'src/app/plugins/modules/time-planning-pn/models';

export class WorkingHoursUpdateModel {
  workerId: number;
  plannings: WorkingHourModel[] = [];
}


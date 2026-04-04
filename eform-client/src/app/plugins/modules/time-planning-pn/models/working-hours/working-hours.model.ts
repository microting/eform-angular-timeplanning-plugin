import {WorkingHourModel} from './working-hour.model';

export class WorkingHoursModel {
  siteId: number;
  plannings: WorkingHourModel[] = [];
}

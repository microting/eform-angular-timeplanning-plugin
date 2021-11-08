import { TimePlanningModel } from '../plannings';

export class WorkingHoursModel {
  siteId: number;
  plannings: TimePlanningModel[] = [];
}

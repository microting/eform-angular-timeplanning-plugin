import { TimePlanningModel } from './time-planning.model';

export class TimePlanningsUpdateModel {
  workerId: number;
  plannings: TimePlanningModel[] = [];
}


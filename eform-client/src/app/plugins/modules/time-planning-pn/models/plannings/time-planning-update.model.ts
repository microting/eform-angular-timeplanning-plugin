import { TimePlanningMessagesEnum } from '../../enums';

export class TimePlanningUpdateModel {
  workerId: number;
  date: string;
  planText: string;
  planHours: number;
  message: TimePlanningMessagesEnum;
}

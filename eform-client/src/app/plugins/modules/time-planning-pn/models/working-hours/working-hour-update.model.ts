import { TimePlanningMessagesEnum } from '../../enums';

export class WorkingHourUpdateModel {
  siteId: number;
  date: string;
  planText: string;
  planHours: number;
  message: TimePlanningMessagesEnum;
}

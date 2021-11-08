import { TimePlanningMessagesEnum } from '../../enums';

export class TimePlanningUpdateModel {
  siteId: number;
  date: string;
  planText: string;
  planHours: number;
  message: TimePlanningMessagesEnum;
}

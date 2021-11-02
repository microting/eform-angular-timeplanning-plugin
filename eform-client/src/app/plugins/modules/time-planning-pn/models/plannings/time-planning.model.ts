import { TimePlanningMessagesEnum } from '../../enums';

export class TimePlanningModel {
  weekDay: number;
  date: string;
  planText: string;
  planHours: number;
  message: TimePlanningMessagesEnum;
  shift1Start: number;
  shift1Pause: number;
  shift1Stop: number;
  shift2Start: number;
  shift2Pause: number;
  shift2Stop: number;
  nettoHours: number;
  flexHours: number;
  sumFlex: number;
  paidOutFlex: number;
  commentWorker: string;
  commentOffice: string;
  commentOfficeAll: string;
}

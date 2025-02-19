import {PlanningPrDayModel} from './planning-pr-day.model';

export class TimePlanningModel {

  siteId: number;
  siteName: string;
  avatarUrl: string;
  planningPrDayModels: PlanningPrDayModel[] = [];
  plannedHours: number;
  plannedMinutes: number;
  currentWorkedHours: number;
  currentWorkedMinutes: number;
  percentageCompleted: number;
}

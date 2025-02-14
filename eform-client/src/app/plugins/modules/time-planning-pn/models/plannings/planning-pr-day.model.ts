export class PlanningPrDayModel {
  siteName: string;
  siteId: number;
  weekDay: number;
  date: string;
  planText: string;
  planHours: number;
  actualHours: number;
  difference: number;
  pauseMinutes: number;
  message: number;
  workDayStarted: boolean;
  workDayEnded: boolean;
  planHoursMatched: boolean;
}

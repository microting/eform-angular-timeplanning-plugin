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
  planHoursMatched: boolean
  plannedStartOfShift1: number;
  plannedEndOfShift1: number;
  plannedBreakOfShift1: number;
  plannedStartOfShift2: number;
  plannedEndOfShift2: number;
  plannedBreakOfShift2: number;
  isDoubleShift: boolean;
  onVacation: boolean;
  sick: boolean;
  otherAllowedAbsence: boolean;
  absenceWithoutPermission: boolean;
}

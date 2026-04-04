import { TimePlanningMessagesEnum } from '../../enums';

export class TimePlanningUpdateModel {
  siteId: number;
  date: string;
  planText: string;
  planHours: number;
  message: TimePlanningMessagesEnum;
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

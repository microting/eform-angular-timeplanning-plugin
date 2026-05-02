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
  SoftwareVersion: string;
  deviceModel: string;
  deviceManufacturer: string;
  softwareVersionIsValid: boolean;
  /**
   * Phase 4: mirror of the row's assigned-site UseOneMinuteIntervals flag.
   * When true, the plannings table renders actual stamps at HH:mm:ss instead
   * of HH:mm. Default false preserves byte-identical legacy behavior for
   * rows whose site has the flag off.
   */
  useOneMinuteIntervals: boolean;
}

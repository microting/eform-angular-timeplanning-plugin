import {SharedTagModel} from 'src/app/common/models';
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
   * Mirror of the row's assigned-site UseOneMinuteIntervals flag. The flag now
   * controls only (a) the wire-precision of stored start/stop timestamps and
   * (b) the minutesGap input granularity in the workday-entity dialog (1-min
   * vs 5-min). Displayed timestamps in the plannings table always show
   * HH:mm regardless of the flag.
   */
  useOneMinuteIntervals: boolean;
  /** SDK site tags assigned to the row's site (rendered as chips in the Navn column). */
  tags: SharedTagModel[] = [];
  payRuleSetId: number | null;
  payRuleSetName: string;
  usePunchClock: boolean;
  allowAcceptOfPlannedHours: boolean;
  allowPersonalTimeRegistration: boolean;
  overMidnight: boolean;
  autoBreakCalculationActive: boolean;
  thirdShiftActive: boolean;
  fourthShiftActive: boolean;
  fifthShiftActive: boolean;
}

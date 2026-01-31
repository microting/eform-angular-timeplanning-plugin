import { AbsenceRequestDayModel } from './absence-request-day.model';

export class AbsenceRequestModel {
  id: number;
  requestedBySdkSitId: number;
  dateFrom: Date;
  dateTo: Date;
  status: string;
  requestedAtUtc: Date;
  decidedAtUtc?: Date;
  decidedBySdkSitId?: number;
  requestComment?: string;
  decisionComment?: string;
  days: AbsenceRequestDayModel[];
}

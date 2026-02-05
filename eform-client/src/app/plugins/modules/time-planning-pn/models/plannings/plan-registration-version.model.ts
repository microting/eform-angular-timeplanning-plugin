export class FieldChangeModel {
  fieldName: string;
  fromValue: string;
  toValue: string;
  fieldType: string; // "standard", "gps", "snapshot"
  latitude?: number;
  longitude?: number;
  pictureHash?: string;
  registrationType?: string;
}

export class PlanRegistrationVersionModel {
  version: number;
  updatedAt: Date;
  updatedByUserId?: number;
  updatedByUserName: string;
  changes: FieldChangeModel[] = [];
}

export class PlanRegistrationVersionHistoryModel {
  planRegistrationId: number;
  gpsEnabled: boolean;
  snapshotEnabled: boolean;
  versions: PlanRegistrationVersionModel[] = [];
}

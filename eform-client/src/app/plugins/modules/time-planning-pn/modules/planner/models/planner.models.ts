/**
 * The web planner, "Planlægning (beta)" (#5489). These mirror the C# models in
 * TimePlanning.Pn/Infrastructure/Models/Planner and Models/PlanPattern
 * one-to-one (camelCase on the wire).
 *
 * Dates: every date here is a calendar date. The server sends them as
 * "2026-11-09T00:00:00" and they are sent back as UTC midnight of that date
 * ("2026-11-09T00:00:00.000Z", see toTransportDate). They are typed as string
 * because that is what arrives; planner.utils.ts turns them into local Dates
 * for display only.
 */

// ------------------------------------------------------------------ shared

/** PlanPatternDayModel: one day of a pattern. */
export interface PlanPatternDayModel {
  /** Week of the rotation, 0 .. rotationWeeks - 1. */
  weekIndex: number;
  /** ISO day of week, 0 = Monday ... 6 = Sunday. */
  dayOfWeek: number;
  /** Canonical plan text, e.g. 7:00-15:00/0.5; null or "" = no plan that day. */
  planText: string | null;
}

/** CommonDictionaryModel from eFormApi.BasePn (the worker's tags). */
export interface PlannerTagModel {
  id: number;
  name: string;
  description?: string | null;
}

// -------------------------------------------------------------------- grid

/** PlannerGridModel: workers x days. */
export interface PlannerGridModel {
  from: string;
  to: string;
  dates: string[];
  dayTotals: PlannerDayTotalModel[];
  workers: PlannerWorkerModel[];
  /** The import wizard is offered only while this is false. */
  googleSheetRetired: boolean;
  /** Pending absence requests the caller may decide (the count on "Ferieønsker"). */
  pendingRequestCount: number;
}

export interface PlannerDayTotalModel {
  date: string;
  /** Workers with plan hours that day and no absence. */
  plannedHeadcount: number;
  /** Workers with a pending absence request covering the day. */
  pendingAbsenceRequests: number;
}

export interface PlannerWorkerModel {
  assignedSiteId: number;
  /** The SDK site MicrotingUid (PlanRegistration.SdkSitId, AbsenceRequest.RequestedBySdkSitId). */
  siteId: number;
  siteName: string;
  avatarUrl: string | null;
  tags: PlannerTagModel[];
  hasPattern: boolean;
  rotationWeeks: number | null;
  useGoogleSheetAsDefault: boolean;
  /** The caller is a current manager of this worker (may approve/reject/register absence). */
  canApproveAbsence: boolean;
  /** Null when the worker has no rule set. */
  weeklyNormHours: number | null;
  weeks: PlannerWeekSumModel[];
  cells: PlannerCellModel[];
}

export interface PlannerWeekSumModel {
  isoYear: number;
  isoWeek: number;
  /** The Monday of the week. */
  weekStart: string;
  plannedHours: number;
  normHours: number | null;
  differenceHours: number | null;
  /** The week of the worker's rotation this week falls on (1-based), or null without a pattern. */
  rotationWeek: number | null;
}

export interface PlannerCellModel {
  date: string;
  planRegistrationId: number | null;
  planText: string;
  planHours: number;
  /** The day's absence type (Message id), or null. */
  messageId: number | null;
  absenceRequestId: number | null;
  planChangedByAdmin: boolean;
  locked: boolean;
  isPaidDay: boolean;
  hasPendingAbsenceRequest: boolean;
}

// --------------------------------------------------------------- bulk edit

export interface PlannerCellRefModel {
  assignedSiteId: number;
  date: string;
}

/** PlannerEditModes. */
export const PlannerEditModes = {
  onlyTheseDays: 'onlyTheseDays',
  everyWeekdayFrom: 'everyWeekdayFrom',
} as const;
export type PlannerEditMode = typeof PlannerEditModes[keyof typeof PlannerEditModes];

/** POST planner/cells. */
export interface PlannerCellsUpdateModel {
  cells: PlannerCellRefModel[];
  /** 7-15/0,5, 6-10;14-18, Fri, or "" (Ryd). Ignored when messageId is set. */
  shortForm: string | null;
  /** An absence type (Message id). Only with mode onlyTheseDays. */
  messageId: number | null;
  mode: PlannerEditMode;
  /** Required for everyWeekdayFrom. */
  fromDate: string | null;
}

export interface PlannerCellsUpdateResultModel {
  updatedCount: number;
  lockedCells: PlannerCellRefModel[];
  errors: PlannerWorkerErrorModel[];
  /** Translated "n days locked – not changed", or null. */
  lockedMessage: string | null;
}

export interface PlannerWorkerErrorModel {
  assignedSiteId: number;
  key: string;
  /** Translated text. */
  message: string;
}

// ----------------------------------------------------------------- pattern

export interface PlannerPatternModel {
  /** Null when not saved (a prefill or a copy). */
  planPatternId: number | null;
  assignedSiteId: number;
  /** No pattern yet: prefilled from the weekday template (1 week). */
  isPrefill: boolean;
  rotationWeeks: number;
  /** The Monday that is week 1 of the rotation. */
  anchorDate: string;
  validFrom: string;
  days: PlanPatternDayModel[];
}

/** PUT planner/patterns/{assignedSiteId}. */
export interface PlannerPatternSaveModel {
  rotationWeeks: number;
  anchorDate: string;
  validFrom: string;
  /** Short forms are accepted; the server stores them canonical. */
  days: PlanPatternDayModel[];
  /** "Overskriv også mine rettede dage". */
  overwriteManualDays: boolean;
}

/** PlanPatternApplyResult. */
export interface PlanPatternApplyResult {
  planPatternId: number;
  rowsWritten: number;
  lockedDays: string[];
  /** A localization key for a saved-with-a-problem outcome, or null. */
  warningKey: string | null;
}

// ----------------------------------------------------------------- absence

/** POST planner/absence. */
export interface PlannerAbsenceCreateModel {
  assignedSiteId: number;
  from: string;
  to: string;
  messageId: number;
  comment: string | null;
}

/** POST planner/absence/preview: what registering this absence would do, day by day. */
export interface PlannerAbsencePreviewRequestModel {
  assignedSiteId: number;
  from: string;
  to: string;
  messageId: number;
}

/** Known preview outcomes; the server may add more, which the dialog shows as unchanged. */
export const PlannerAbsenceOutcomes = {
  apply: 'apply',
  dayOff: 'dayOff',
  locked: 'locked',
  /** Held by another pending or approved request: refuses the whole request. */
  overlap: 'overlap',
} as const;
export type PlannerAbsenceOutcome = typeof PlannerAbsenceOutcomes[keyof typeof PlannerAbsenceOutcomes];

export interface PlannerAbsencePreviewDayModel {
  date: string;
  planText: string | null;
  planHours: number;
  /** See PlannerAbsenceOutcomes. */
  outcome: string;
}

export interface PlannerAbsencePreviewModel {
  days: PlannerAbsencePreviewDayModel[];
  /** Days that would become absence. */
  workDays: number;
  /** The planned hours of those days. */
  plannedHours: number;
}

export interface PlannerAbsenceResultModel {
  absenceRequestId: number | null;
  appliedDates: string[];
  skippedDates: string[];
  /** On refusal: the locked days that blocked the whole request. */
  lockedDates: string[];
}

// ------------------------------------------------------------------ import

export interface PlannerImportPreviewModel {
  /** The first Monday read; also every recognised pattern's anchorDate. */
  windowStart: string;
  workers: PlannerImportWorkerModel[];
}

/** RotationInference reason codes, as the server sends them. */
export const PlannerImportReasons = {
  inconsistent: 'inconsistent',
  unparsable: 'unparsable',
  insufficientHistory: 'insufficientHistory',
  hoursOnly: 'hoursOnly',
} as const;

/** RotationInference confidence codes: every rotation day seen 3+ times (high) or twice (medium). */
export const PlannerImportConfidences = {
  high: 'high',
  medium: 'medium',
} as const;

export interface PlannerImportWorkerModel {
  assignedSiteId: number;
  siteId: number;
  siteName: string;
  /** False = "kunne ikke genkendes": no pattern is proposed. */
  recognised: boolean;
  /** When not recognised: see PlannerImportReasons. */
  reason: string | null;
  /** When recognised: see PlannerImportConfidences; null when not recognised. */
  confidence: string | null;
  rotationWeeks: number | null;
  anchorDate: string | null;
  days: PlanPatternDayModel[];
  /** Whole rotation cycles seen in the sheet (the fewest sightings of any rotation day); null when not known. */
  cyclesSeen: number | null;
  /** Weeks in the sheet with known days; null when not known. */
  weeksSeen: number | null;
}

/** POST planner/import/apply. */
export interface PlannerImportApplyModel {
  patterns: PlannerImportPatternModel[];
}

export interface PlannerImportPatternModel {
  assignedSiteId: number;
  rotationWeeks: number;
  anchorDate: string;
  /** Defaults to today on the server. */
  validFrom: string | null;
  days: PlanPatternDayModel[];
}

export interface PlannerImportApplyResultModel {
  workers: PlannerImportAppliedModel[];
}

export interface PlannerImportAppliedModel {
  assignedSiteId: number;
  planPatternId: number;
  rowsWritten: number;
  lockedDays: string[];
  /** Translated warning, or null. */
  warning: string | null;
}

// ---------------------------------------------------------------- settings

export interface PlannerSettingsModel {
  googleSheetRetired: boolean;
}

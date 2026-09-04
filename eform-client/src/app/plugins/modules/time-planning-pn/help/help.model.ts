export const HELP_IDS = [
  // tasks
  'task.registerVacation', 'task.registerSickness', 'task.registerDayOff',
  'task.correctRegisteredTime', 'task.addMissingRegistration', 'task.addExtraShift',
  'task.changePlannedHours', 'task.payOutFlex', 'task.exportForPayroll',
  'task.whoChangedThis', 'task.whereWasThisRegistered', 'task.filterToOneTeam',
  // toolbar controls
  'toolbar.showResigned', 'toolbar.navBackward', 'toolbar.navForward',
  'toolbar.workerFilter', 'toolbar.tagFilter', 'toolbar.dateRange',
  'toolbar.downloadExcel', 'toolbar.payrollExport', 'toolbar.reload',
  // grid controls
  'grid.nameColumn', 'grid.tagChips', 'grid.settingsStrip', 'grid.dayCellAnatomy',
  'grid.weeklyPlannedHours', 'grid.messageIcons', 'grid.sortName', 'grid.openDay',
  // day-cell dialog controls
  'dayCell.versionHistory', 'dayCell.plannedTimes', 'dayCell.actualTimes',
  'dayCell.shiftCount', 'dayCell.resetField', 'dayCell.resetPauseToRecorded',
  'dayCell.gps', 'dayCell.snapshot', 'dayCell.futureDisabled', 'dayCell.planHours',
  'dayCell.nettoOverride', 'dayCell.paidOutFlex', 'dayCell.flags',
  'dayCell.commentOffice', 'dayCell.save', 'dayCell.oneMinuteIntervals',
  // flex controls
  'flex.whatIsFlex', 'flex.sumFlex', 'flex.paidOutFlexRelation',
] as const;

export type HelpEntryId = typeof HELP_IDS[number];
export type HelpKind = 'control' | 'task';
export type HelpSection = 'task' | 'toolbar' | 'grid' | 'dayCell' | 'flex';
export type HelpTourName = 'page' | 'dialog';

export interface HelpEntry {
  id: HelpEntryId;
  kind: HelpKind;
  section: HelpSection;
  /** data-tp-help value on the element this entry describes. Controls only. */
  anchor?: string;
  tour?: HelpTourName;
  tourStep?: number;
  adminOnly?: boolean;
  /** Tasks only: the controls this task touches. */
  related?: HelpEntryId[];
}

export interface HelpProse {
  title: string;
  short: string;
  detail?: string;
  /** Tasks only, in order. */
  steps?: string[];
  /** Search synonyms, in this locale's language. */
  keywords: string[];
}

export type HelpProseMap = Record<HelpEntryId, HelpProse>;

/**
 * Labels for the help components' own chrome. These live here rather than in the
 * plugin's 25 shared locale files, which this work must not touch.
 */
export interface HelpUiStrings {
  help: string;
  searchHelp: string;
  clear: string;
  close: string;
  moreInHelp: string;
  replayTour: string;
  skip: string;
  next: string;
  noResults: string;
  sectionTask: string;
  sectionToolbar: string;
  sectionGrid: string;
  sectionDayCell: string;
  sectionFlex: string;
}

export type HelpUiKey = keyof HelpUiStrings;

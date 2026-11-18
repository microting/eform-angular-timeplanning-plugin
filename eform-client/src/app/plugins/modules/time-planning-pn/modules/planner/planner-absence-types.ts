import {TimePlanningMessagesEnum} from '../../enums';

/**
 * The order the planner offers the types in (the design's chips): the planned
 * kinds of absence first, then the rest in enum order.
 */
const LEADING_TYPES: number[] = [
  TimePlanningMessagesEnum.Vacation,
  TimePlanningMessagesEnum.TimeOff,
  TimePlanningMessagesEnum.VacationDayOff,
  TimePlanningMessagesEnum.DayOff,
  TimePlanningMessagesEnum.Course,
  TimePlanningMessagesEnum.LeaveOfAbsence,
  TimePlanningMessagesEnum.Maternity,
];

function typeRank(id: number): number {
  const leading = LEADING_TYPES.indexOf(id);
  return leading >= 0 ? leading : LEADING_TYPES.length + id;
}

/**
 * The absence types (Message ids) the planner offers, from the plugin's own
 * TimePlanningMessagesEnum; the label is the enum key, which the plugin already
 * translates. "Blank" is not an absence.
 */
export const PLANNER_ABSENCE_TYPES: { id: number; key: string }[] = Object.keys(TimePlanningMessagesEnum)
  .filter(key => isNaN(Number(key)) && key !== 'Blank')
  .map(key => ({id: TimePlanningMessagesEnum[key as keyof typeof TimePlanningMessagesEnum], key}))
  .sort((a, b) => typeRank(a.id) - typeRank(b.id));

/** The type the absence dialog starts on: vacation, the common case. */
export const DEFAULT_ABSENCE_TYPE = TimePlanningMessagesEnum.Vacation;

/** The translation key of a Message id, or null. */
export function absenceTypeKey(messageId: number | null | undefined): string | null {
  return messageId == null ? null : (TimePlanningMessagesEnum[messageId] ?? null);
}

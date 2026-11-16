import {TimePlanningMessagesEnum} from '../../enums';

/**
 * The absence types (Message ids) the planner offers, from the plugin's own
 * TimePlanningMessagesEnum; the label is the enum key, which the plugin already
 * translates. "Blank" is not an absence.
 */
export const PLANNER_ABSENCE_TYPES: { id: number; key: string }[] = Object.keys(TimePlanningMessagesEnum)
  .filter(key => isNaN(Number(key)) && key !== 'Blank')
  .map(key => ({id: TimePlanningMessagesEnum[key as keyof typeof TimePlanningMessagesEnum], key}));

/** The translation key of a Message id, or null. */
export function absenceTypeKey(messageId: number | null | undefined): string | null {
  return messageId == null ? null : (TimePlanningMessagesEnum[messageId] ?? null);
}

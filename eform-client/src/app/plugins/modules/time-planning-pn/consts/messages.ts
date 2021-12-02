import { TimePlanningMessagesEnum } from 'src/app/plugins/modules/time-planning-pn/enums';
import { TranslateService } from '@ngx-translate/core';

export function messages(
  translate: TranslateService
): { id: number; value: string }[] {
  return [
    {
      id: TimePlanningMessagesEnum.DayOff,
      value: translate.instant(
        TimePlanningMessagesEnum[TimePlanningMessagesEnum.DayOff]
      ),
    },
    {
      id: TimePlanningMessagesEnum.Vacation,
      value: translate.instant(
        TimePlanningMessagesEnum[TimePlanningMessagesEnum.Vacation]
      ),
    },
    {
      id: TimePlanningMessagesEnum.Sick,
      value: translate.instant(
        TimePlanningMessagesEnum[TimePlanningMessagesEnum.Sick]
      ),
    },
    {
      id: TimePlanningMessagesEnum.Course,
      value: translate.instant(
        TimePlanningMessagesEnum[TimePlanningMessagesEnum.Course]
      ),
    },
    {
      id: TimePlanningMessagesEnum.LeaveOfAbsence,
      value: translate.instant(
        TimePlanningMessagesEnum[TimePlanningMessagesEnum.LeaveOfAbsence]
      ),
    },
    {
      id: TimePlanningMessagesEnum.Care,
      value: translate.instant(
        TimePlanningMessagesEnum[TimePlanningMessagesEnum.Care]
      ),
    },
    {
      id: TimePlanningMessagesEnum.Children1stSick,
      value: translate.instant(
        TimePlanningMessagesEnum[TimePlanningMessagesEnum.Children1stSick]
      ),
    },
    {
      id: TimePlanningMessagesEnum.Children2stSick,
      value: translate.instant(
        TimePlanningMessagesEnum[TimePlanningMessagesEnum.Children2stSick]
      ),
    },
    {
      id: TimePlanningMessagesEnum.TimeOff,
      value: translate.instant(
        TimePlanningMessagesEnum[TimePlanningMessagesEnum.TimeOff]
      ),
    },
    {
      id: TimePlanningMessagesEnum[' '],
      value: TimePlanningMessagesEnum[TimePlanningMessagesEnum[' ']],
    },
  ];
}

import { MY_MOMENT_FORMATS } from 'src/app/common/helpers';

// See the Moment.js docs for the meaning of these formats:
// https://momentjs.com/docs/#/displaying/format/
// or https://danielykpan.github.io/date-time-picker
export const MY_MOMENT_FORMATS_FOR_WORKING_HOURS = {
  parseInput: MY_MOMENT_FORMATS.parseInput, // 'l LT',
  fullPickerInput: 'DD.MM.YYYY',
  datePickerInput: 'DD.MM.YYYY',
  timePickerInput: 'DD.MM.YYYY',
  monthYearLabel: MY_MOMENT_FORMATS.monthYearLabel, // 'MMM YYYY',
  dateA11yLabel: MY_MOMENT_FORMATS.dateA11yLabel, // 'LL',
  monthYearA11yLabel: MY_MOMENT_FORMATS.monthYearA11yLabel, // 'MMMM YYYY',
};

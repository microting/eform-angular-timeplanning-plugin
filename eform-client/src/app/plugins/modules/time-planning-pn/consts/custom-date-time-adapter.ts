import { MY_MOMENT_FORMATS } from 'src/app/common/helpers';

// See the Moment.js docs for the meaning of these formats:
// https://momentjs.com/docs/#/displaying/format/
// or https://danielykpan.github.io/date-time-picker
export const MY_MOMENT_FORMATS_FOR_TIME_PLANNING = {
  parseInput: MY_MOMENT_FORMATS.parseInput, // 'l LT',
  fullPickerInput: MY_MOMENT_FORMATS.fullPickerInput, // 'YYYY/MM/DD',
  datePickerInput: MY_MOMENT_FORMATS.datePickerInput, // 'YYYY/MM/DD',
  timePickerInput: MY_MOMENT_FORMATS.timePickerInput, // 'YYYY/MM/DD',
  monthYearLabel: MY_MOMENT_FORMATS.monthYearLabel, // 'MMM YYYY',
  dateA11yLabel: MY_MOMENT_FORMATS.dateA11yLabel, // 'LL',
  monthYearA11yLabel: MY_MOMENT_FORMATS.monthYearA11yLabel, // 'MMMM YYYY',
};

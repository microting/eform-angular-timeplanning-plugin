import {Component, Input, OnChanges} from '@angular/core';
import {FormsModule} from '@angular/forms';
import {MatFormField} from '@angular/material/form-field';
import {MatInput} from '@angular/material/input';
import {TranslatePipe} from '@ngx-translate/core';
import {PlanPatternDayModel} from '../../models';
import {isValidPatternDayText, WEEKDAY_KEYS} from '../../planner.utils';

/**
 * The rotation weeks x Mon–Sun grid of plan texts (#5489), shared by the
 * pattern editor and the import review. `days` must hold every
 * (week, weekday) of the rotation (fullPatternGrid); texts are edited in place.
 */
@Component({
  selector: 'app-planner-pattern-days',
  imports: [FormsModule, MatFormField, MatInput, TranslatePipe],
  templateUrl: './planner-pattern-days.component.html',
  styleUrls: ['./planner-pattern-days.component.scss'],
})
export class PlannerPatternDaysComponent implements OnChanges {
  @Input() rotationWeeks = 1;
  @Input() days: PlanPatternDayModel[] = [];
  /** Prefix of the inputs' ids/test ids, e.g. planner-pattern-day or planner-import-day-12. */
  @Input() idPrefix = 'planner-pattern-day';

  readonly weekdayKeys = WEEKDAY_KEYS;
  weeks: PlanPatternDayModel[][] = [];

  ngOnChanges(): void {
    this.weeks = [];
    for (let week = 0; week < this.rotationWeeks; week++) {
      this.weeks.push(this.days.filter(d => d.weekIndex === week).sort((a, b) => a.dayOfWeek - b.dayOfWeek));
    }
  }

  isValid(day: PlanPatternDayModel): boolean {
    return isValidPatternDayText(day.planText);
  }
}

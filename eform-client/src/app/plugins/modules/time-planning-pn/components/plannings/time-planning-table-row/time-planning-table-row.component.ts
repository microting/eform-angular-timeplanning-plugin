import {
  AfterViewInit,
  Component,
  EventEmitter,
  Input,
  OnInit,
  Output,
  ViewChild,
} from '@angular/core';
import { debounceTime, distinctUntilChanged, last } from 'rxjs/operators';
import { DaysOfWeekEnum } from 'src/app/common/const';
import { TimePlanningMessagesEnum } from '../../../enums';

@Component({
  // tslint:disable-next-line:component-selector
  selector: '[time-planning-table-row]',
  templateUrl: './time-planning-table-row.component.html',
  styleUrls: ['./time-planning-table-row.component.scss'],
})
export class TimePlanningTableRowComponent implements OnInit, AfterViewInit {
  @ViewChild('planHoursInput') planHoursInput;
  @ViewChild('planTextInput') planTextInput;
  @Input() weekDay: number;
  @Input() date: string;
  @Input() planText: string;
  @Input() planHours: number;
  @Input() message: number;
  @Input() index: number;
  @Output() planHoursChanged: EventEmitter<number> = new EventEmitter<number>();
  @Output() planTextChanged: EventEmitter<string> = new EventEmitter<string>();
  @Output() messageChanged: EventEmitter<number> = new EventEmitter<number>();

  get daysOfWeek() {
    return DaysOfWeekEnum;
  }

  get messages() {
    return TimePlanningMessagesEnum;
  }

  constructor() {}

  ngAfterViewInit() {
    this.planHoursInput.valueChanges
      .pipe(debounceTime(500), distinctUntilChanged())
      .subscribe(() => (value) => {
        this.planHoursChanged.emit(value);
      });

    this.planTextInput.valueChanges
      .pipe(debounceTime(500), distinctUntilChanged())
      .subscribe(() => (value) => {
        this.planTextChanged.emit(value);
      });
  }

  ngOnInit(): void {}

  changeMessage($event: any) {
    this.messageChanged.emit($event);
  }
}

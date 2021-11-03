import {
  AfterViewInit,
  Component,
  EventEmitter,
  Input, OnDestroy,
  OnInit,
  Output,
  ViewChild,
} from '@angular/core';
import {AutoUnsubscribe} from 'ngx-auto-unsubscribe';
import { Subject } from 'rxjs';
import { debounceTime, distinctUntilChanged, last } from 'rxjs/operators';
import { DaysOfWeekEnum } from 'src/app/common/const';
import { TimePlanningMessagesEnum } from '../../../enums';

@AutoUnsubscribe()
@Component({
  // tslint:disable-next-line:component-selector
  selector: '[time-planning-table-row]',
  templateUrl: './time-planning-table-row.component.html',
  styleUrls: ['./time-planning-table-row.component.scss'],
})
export class TimePlanningTableRowComponent implements OnInit, AfterViewInit, OnDestroy {
  @Input() weekDay: number;
  @Input() date: string;
  @Input() planText: string;
  @Input() planHours: number;
  @Input() message: number;
  @Input() index: number;
  @Output() planHoursChanged: EventEmitter<number> = new EventEmitter<number>();
  @Output() planTextChanged: EventEmitter<string> = new EventEmitter<string>();
  @Output() messageChanged: EventEmitter<number> = new EventEmitter<number>();

  planHours$ = new Subject<number>();
  planText$ = new Subject<string>();

  get daysOfWeek() {
    return DaysOfWeekEnum;
  }

  get messages() {
    return TimePlanningMessagesEnum;
  }

  constructor() {}

  ngAfterViewInit() {}

  ngOnInit(): void {
    this.planHours$.pipe(
      debounceTime(1000),
      distinctUntilChanged()
    ).subscribe((value: number) => {
      this.planHoursChanged.emit(value);
    });

    this.planText$.pipe(
      debounceTime(1000),
      distinctUntilChanged()
    ).subscribe((value: string) => {
      this.planTextChanged.emit(value);
    });
  }

  changeMessage($event: any) {
    this.messageChanged.emit($event);
  }

  onPlanHoursChange($event: any) {
    this.planHours$.next($event);
  }

  onPlanTextChange($event: any) {
    this.planText$.next($event);
  }

  ngOnDestroy(): void {
  }
}

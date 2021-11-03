import {
  AfterViewInit,
  Component,
  EventEmitter,
  Input,
  OnInit,
  Output,
} from '@angular/core';
import { Subject } from 'rxjs';
import { debounceTime, distinctUntilChanged, last } from 'rxjs/operators';
import { TimePlanningMessagesEnum } from '../../../enums';

@Component({
  // tslint:disable-next-line:component-selector
  selector: '[time-flexes-table-row]',
  templateUrl: './time-flexes-table-row.component.html',
  styleUrls: ['./time-flexes-table-row.component.scss'],
})
export class TimeFlexesTableRowComponent implements OnInit, AfterViewInit {
  @Input() date: string;
  @Input() workerName: string;
  @Input() paidOutFlex: number;
  @Input() sumFlex: number;
  @Input() index: number;
  @Input() commentWorker: string;
  @Input() commentOffice: string;
  @Input() commentOfficeAll: string;
  @Output()
  paidOutFlexChanged: EventEmitter<number> = new EventEmitter<number>();
  @Output() planTextChanged: EventEmitter<string> = new EventEmitter<string>();
  @Output() messageChanged: EventEmitter<number> = new EventEmitter<number>();

  paidOutFlex$ = new Subject<number>();

  get messages() {
    return TimePlanningMessagesEnum;
  }

  constructor() {}

  ngAfterViewInit() {}

  ngOnInit(): void {
    this.paidOutFlex$
      .pipe(debounceTime(1000), distinctUntilChanged())
      .subscribe((value: number) => {
        this.paidOutFlexChanged.emit(value);
      });
  }

  onPaidOutFlexChange($event: any) {
    this.paidOutFlex$.next($event);
  }
}

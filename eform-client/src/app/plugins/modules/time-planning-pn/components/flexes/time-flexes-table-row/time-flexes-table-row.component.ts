import {
  AfterViewInit,
  Component,
  EventEmitter,
  Input,
  OnInit,
  Output,
} from '@angular/core';
import { Subject } from 'rxjs';
import { debounceTime, distinctUntilChanged } from 'rxjs/operators';
import { TimePlanningMessagesEnum } from '../../../enums';
import { TimeFlexesModel } from 'src/app/plugins/modules/time-planning-pn/models';

@Component({
  // tslint:disable-next-line:component-selector
  selector: '[time-flexes-table-row]',
  templateUrl: './time-flexes-table-row.component.html',
  styleUrls: ['./time-flexes-table-row.component.scss'],
})
export class TimeFlexesTableRowComponent implements OnInit, AfterViewInit {
  @Input() flexPlanning: TimeFlexesModel = new TimeFlexesModel();
  @Input() index: number;
  @Output()
  paidOutFlexChanged: EventEmitter<number> = new EventEmitter<number>();
  @Output() planTextChanged: EventEmitter<string> = new EventEmitter<string>();
  @Output()
  openEditCommentOffice: EventEmitter<TimeFlexesModel> = new EventEmitter<TimeFlexesModel>();
  @Output()
  openEditCommentOfficeAll: EventEmitter<TimeFlexesModel> = new EventEmitter<TimeFlexesModel>();

  paidOutFlex$ = new Subject<number>();

  get messages() {
    return TimePlanningMessagesEnum;
  }

  get sumFlex() {
    return (this.flexPlanning.sumFlex - this.flexPlanning.paidOutFlex).toFixed(
      2
    );
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

  onCommentOfficeClick() {
    this.openEditCommentOffice.emit(this.flexPlanning);
  }

  onCommentOfficeAllClick() {
    this.openEditCommentOfficeAll.emit(this.flexPlanning);
  }
}

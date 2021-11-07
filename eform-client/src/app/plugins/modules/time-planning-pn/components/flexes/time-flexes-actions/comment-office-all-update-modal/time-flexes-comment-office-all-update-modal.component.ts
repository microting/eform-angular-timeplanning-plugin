import {
  Component,
  EventEmitter,
  OnDestroy,
  OnInit,
  Output,
  ViewChild,
} from '@angular/core';
import { AutoUnsubscribe } from 'ngx-auto-unsubscribe';
import { TimeFlexesModel } from '../../../../models';

@AutoUnsubscribe()
@Component({
  selector: 'app-time-flexes-comment-office-all-update-modal',
  templateUrl: './time-flexes-comment-office-all-update-modal.component.html',
  styleUrls: ['./time-flexes-comment-office-all-update-modal.component.scss'],
})
export class TimeFlexesCommentOfficeAllUpdateModalComponent
  implements OnInit, OnDestroy {
  @ViewChild('frame', { static: false }) frame;
  @Output()
  commentOfficeAllUpdate: EventEmitter<TimeFlexesModel> = new EventEmitter<TimeFlexesModel>();
  timeFlexes: TimeFlexesModel = new TimeFlexesModel();

  constructor() {}

  ngOnInit() {}

  show(timeFlexes: TimeFlexesModel): void {
    this.timeFlexes = { ...timeFlexes };
    this.frame.show();
  }

  onUpdateFlexPlanning() {
    this.commentOfficeAllUpdate.emit({ ...this.timeFlexes });
    this.hide();
  }

  hide() {
    this.frame.hide();
    this.timeFlexes = new TimeFlexesModel();
  }

  ngOnDestroy(): void {}
}

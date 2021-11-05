import {
  Component,
  EventEmitter,
  OnDestroy,
  OnInit,
  Output,
  ViewChild,
} from '@angular/core';
import { AutoUnsubscribe } from 'ngx-auto-unsubscribe';
import { Subscription } from 'rxjs';
import { TimeFlexesModel } from '../../../../models';

@AutoUnsubscribe()
@Component({
  selector: 'app-time-flexes-comment-office-update-modal',
  templateUrl: './time-flexes-comment-office-update-modal.component.html',
  styleUrls: ['./time-flexes-comment-office-update-modal.component.scss'],
})
export class TimeFlexesCommentOfficeUpdateModalComponent
  implements OnInit, OnDestroy {
  @ViewChild('frame', { static: false }) frame;
  @Output()
  commentOfficeUpdate: EventEmitter<TimeFlexesModel> = new EventEmitter<TimeFlexesModel>();
  timeFlexes: TimeFlexesModel = new TimeFlexesModel();

  updateTimePlanning$: Subscription;

  constructor() {}

  ngOnInit() {}

  show(timeFlexes: TimeFlexesModel): void {
    this.timeFlexes = { ...timeFlexes };
    this.frame.show();
  }

  onUpdateFlexPlanning() {
    this.commentOfficeUpdate.emit({ ...this.timeFlexes });
    this.hide();
    // this.updateTimePlanning$ = this.planningsService
    //   .updateFlexes({ ...this.timeFlexes })
    //   .subscribe((data) => {
    //     if (data && data.success) {
    //       this.commentOfficeUpdate.emit();
    //       this.hide();
    //     }
    //   });
  }

  hide() {
    this.frame.hide();
    this.timeFlexes = new TimeFlexesModel();
  }

  ngOnDestroy(): void {}
}

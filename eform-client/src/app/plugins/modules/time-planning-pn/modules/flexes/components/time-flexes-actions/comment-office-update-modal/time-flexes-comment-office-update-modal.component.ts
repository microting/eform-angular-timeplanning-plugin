import {
  Component,
  Inject,
  OnDestroy,
  OnInit,
} from '@angular/core';
import {AutoUnsubscribe} from 'ngx-auto-unsubscribe';
import {TimeFlexesModel} from '../../../../../models';
import {MAT_DIALOG_DATA, MatDialogRef} from '@angular/material/dialog';

@AutoUnsubscribe()
@Component({
    selector: 'app-time-flexes-comment-office-update-modal',
    templateUrl: './time-flexes-comment-office-update-modal.component.html',
    styleUrls: ['./time-flexes-comment-office-update-modal.component.scss'],
    standalone: false
})
export class TimeFlexesCommentOfficeUpdateModalComponent implements OnInit, OnDestroy {
  timeFlexes: TimeFlexesModel = new TimeFlexesModel();

  constructor(
    public dialogRef: MatDialogRef<TimeFlexesCommentOfficeUpdateModalComponent>,
    @Inject(MAT_DIALOG_DATA) timeFlexes: TimeFlexesModel,
  ) {
    this.timeFlexes = {...timeFlexes};
  }

  ngOnInit() {
  }

  onUpdateFlexPlanning() {
    this.hide(true);
  }

  hide(result = false) {
    this.dialogRef.close({result: result, model: {...this.timeFlexes}});
    this.timeFlexes = new TimeFlexesModel();
  }

  ngOnDestroy(): void {
  }
}

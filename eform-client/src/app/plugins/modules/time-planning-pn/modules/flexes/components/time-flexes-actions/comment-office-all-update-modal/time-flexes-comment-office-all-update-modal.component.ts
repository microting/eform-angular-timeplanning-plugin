import {
  Component,
  OnDestroy,
  OnInit,
  inject
} from '@angular/core';
import {AutoUnsubscribe} from 'ngx-auto-unsubscribe';
import {TimeFlexesModel} from '../../../../../models';
import {MAT_DIALOG_DATA, MatDialogRef} from '@angular/material/dialog';

@AutoUnsubscribe()
@Component({
    selector: 'app-time-flexes-comment-office-all-update-modal',
    templateUrl: './time-flexes-comment-office-all-update-modal.component.html',
    styleUrls: ['./time-flexes-comment-office-all-update-modal.component.scss',
],
    standalone: false
})
export class TimeFlexesCommentOfficeAllUpdateModalComponent
  implements OnInit, OnDestroy {
  public dialogRef = inject(MatDialogRef<TimeFlexesCommentOfficeAllUpdateModalComponent>);
  private injectedTimeFlexes = inject<TimeFlexesModel>(MAT_DIALOG_DATA);

  timeFlexes: TimeFlexesModel = new TimeFlexesModel();

  

  ngOnInit() {
    this.timeFlexes = {...this.injectedTimeFlexes};
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

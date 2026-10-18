import {
  Component,
  OnDestroy,
  OnInit,
  inject
} from '@angular/core';
import {AutoUnsubscribe} from 'ngx-auto-unsubscribe';
import {TimeFlexesModel} from '../../../../../models';
import {MAT_DIALOG_DATA, MatDialogRef, MatDialogTitle, MatDialogContent, MatDialogActions} from '@angular/material/dialog';
import {DatePipe} from '@angular/common';
import {FormsModule} from '@angular/forms';
import {TranslatePipe} from '@ngx-translate/core';
import {MatFormField, MatLabel} from '@angular/material/form-field';
import {MatInput} from '@angular/material/input';

@AutoUnsubscribe()
@Component({
    selector: 'app-time-flexes-comment-office-all-update-modal',
    imports: [DatePipe, FormsModule, TranslatePipe, MatFormField, MatLabel, MatInput, MatDialogTitle, MatDialogContent, MatDialogActions],
    templateUrl: './time-flexes-comment-office-all-update-modal.component.html',
    styleUrls: ['./time-flexes-comment-office-all-update-modal.component.scss',
],
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

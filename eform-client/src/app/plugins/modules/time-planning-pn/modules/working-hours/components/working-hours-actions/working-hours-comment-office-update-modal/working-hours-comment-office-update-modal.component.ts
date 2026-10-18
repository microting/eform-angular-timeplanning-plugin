import {Component, OnDestroy, OnInit,
  inject
} from '@angular/core';
import {AutoUnsubscribe} from 'ngx-auto-unsubscribe';
import {FormGroup, FormsModule} from '@angular/forms';
import {MAT_DIALOG_DATA, MatDialogRef, MatDialogTitle, MatDialogContent, MatDialogActions} from '@angular/material/dialog';
import {DatePipe} from '@angular/common';
import {TranslatePipe} from '@ngx-translate/core';
import {MatFormField, MatLabel} from '@angular/material/form-field';
import {MatInput} from '@angular/material/input';

@AutoUnsubscribe()
@Component({
    selector: 'app-working-hours-comment-office-update-modal',
    imports: [DatePipe, FormsModule, TranslatePipe, MatFormField, MatLabel, MatInput, MatDialogTitle, MatDialogContent, MatDialogActions],
    templateUrl: './working-hours-comment-office-update-modal.component.html',
    styleUrls: ['./working-hours-comment-office-update-modal.component.scss'],
})
export class WorkingHoursCommentOfficeUpdateModalComponent
  implements OnInit, OnDestroy {
  public dialogRef = inject(MatDialogRef<WorkingHoursCommentOfficeUpdateModalComponent>);
  public workingHoursForm = inject<FormGroup>(MAT_DIALOG_DATA);

  commentOffice: string;

  

  ngOnInit() {
    this.commentOffice = this.workingHoursForm.get('commentOffice').value;
  }

  get date(): Date {
    return this.workingHoursForm.get('date').value;
  }

  get workerName(): string {
    return this.workingHoursForm.get('workerName').value;
  }

  save() {
    this.workingHoursForm.get('commentOffice').setValue(this.commentOffice);
    this.hide();
  }

  hide() {
    this.dialogRef.close();
  }

  ngOnDestroy(): void {
  }
}

import {Component, OnDestroy, OnInit,
  inject
} from '@angular/core';
import {AutoUnsubscribe} from 'ngx-auto-unsubscribe';
import {FormGroup} from '@angular/forms';
import {MAT_DIALOG_DATA, MatDialogRef} from '@angular/material/dialog';

@AutoUnsubscribe()
@Component({
    selector: 'app-working-hours-comment-office-update-modal',
    templateUrl: './working-hours-comment-office-update-modal.component.html',
    styleUrls: ['./working-hours-comment-office-update-modal.component.scss'],
    standalone: false
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

import {Component, Inject, Input, OnDestroy, OnInit, ViewChild} from '@angular/core';
import {AutoUnsubscribe} from 'ngx-auto-unsubscribe';
import {FormGroup} from '@angular/forms';
import {MAT_DIALOG_DATA, MatDialogRef} from '@angular/material/dialog';

@AutoUnsubscribe()
@Component({
  selector: 'app-working-hours-comment-office-all-update-modal',
  templateUrl: './working-hours-comment-office-all-update-modal.component.html',
  styleUrls: ['./working-hours-comment-office-all-update-modal.component.scss'],
})
export class WorkingHoursCommentOfficeAllUpdateModalComponent
  implements OnInit, OnDestroy {
  commentOfficeAll: string;

  constructor(
    public dialogRef: MatDialogRef<WorkingHoursCommentOfficeAllUpdateModalComponent>,
    @Inject(MAT_DIALOG_DATA) public workingHoursForm: FormGroup,
  ) {
    this.commentOfficeAll = this.workingHoursForm.get('commentOfficeAll').value;
  }

  ngOnInit() {
  }

  get date(): Date {
    return this.workingHoursForm.get('date').value;
  }

  get workerName(): string {
    return this.workingHoursForm.get('workerName').value;
  }

  save() {
    this.workingHoursForm
      .get('commentOfficeAll')
      .setValue(this.commentOfficeAll);
    this.hide();
  }

  hide() {
    this.dialogRef.close();
  }

  ngOnDestroy(): void {
  }
}

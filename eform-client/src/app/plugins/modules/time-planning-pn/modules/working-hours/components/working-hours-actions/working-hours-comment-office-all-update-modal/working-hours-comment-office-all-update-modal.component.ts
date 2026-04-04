import {Component, OnDestroy, OnInit,
  inject
} from '@angular/core';
import {AutoUnsubscribe} from 'ngx-auto-unsubscribe';
import {FormGroup} from '@angular/forms';
import {MAT_DIALOG_DATA, MatDialogRef} from '@angular/material/dialog';

@AutoUnsubscribe()
@Component({
    selector: 'app-working-hours-comment-office-all-update-modal',
    templateUrl: './working-hours-comment-office-all-update-modal.component.html',
    styleUrls: ['./working-hours-comment-office-all-update-modal.component.scss'],
    standalone: false
})
export class WorkingHoursCommentOfficeAllUpdateModalComponent
  implements OnInit, OnDestroy {
  public dialogRef = inject(MatDialogRef<WorkingHoursCommentOfficeAllUpdateModalComponent>);
  public workingHoursForm = inject<FormGroup>(MAT_DIALOG_DATA);

  commentOfficeAll: string;

  

  ngOnInit() {
    this.commentOfficeAll = this.workingHoursForm.get('commentOfficeAll').value;
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

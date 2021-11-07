import { Component, Input, OnDestroy, OnInit, ViewChild } from '@angular/core';
import { AutoUnsubscribe } from 'ngx-auto-unsubscribe';
import { FormGroup } from '@angular/forms';

@AutoUnsubscribe()
@Component({
  selector: 'app-working-hours-comment-office-all-update-modal',
  templateUrl: './working-hours-comment-office-all-update-modal.component.html',
  styleUrls: ['./working-hours-comment-office-all-update-modal.component.scss'],
})
export class WorkingHoursCommentOfficeAllUpdateModalComponent
  implements OnInit, OnDestroy {
  @ViewChild('frame', { static: false }) frame;
  @Input() workingHoursForm: FormGroup;
  commentOfficeAll: string;

  constructor() {}

  ngOnInit() {}

  get date(): Date {
    return this.workingHoursForm.get('date').value;
  }

  get workerName(): string {
    return this.workingHoursForm.get('workerName').value;
  }

  show(): void {
    this.commentOfficeAll = this.workingHoursForm.get('commentOfficeAll').value;
    this.frame.show();
  }

  save() {
    this.workingHoursForm
      .get('commentOfficeAll')
      .setValue(this.commentOfficeAll);
    this.hide();
  }

  hide() {
    this.frame.hide();
  }

  ngOnDestroy(): void {}
}

import { Component, Input, OnDestroy, OnInit, ViewChild } from '@angular/core';
import { AutoUnsubscribe } from 'ngx-auto-unsubscribe';
import { FormGroup } from '@angular/forms';

@AutoUnsubscribe()
@Component({
  selector: 'app-working-hours-comment-office-update-modal',
  templateUrl: './working-hours-comment-office-update-modal.component.html',
  styleUrls: ['./working-hours-comment-office-update-modal.component.scss'],
})
export class WorkingHoursCommentOfficeUpdateModalComponent
  implements OnInit, OnDestroy {
  @ViewChild('frame', { static: false }) frame;
  @Input() workingHoursForm: FormGroup;
  commentOffice: string;

  constructor() {}

  ngOnInit() {}

  get date(): Date {
    return this.workingHoursForm.get('date').value;
  }

  get workerName(): string {
    return this.workingHoursForm.get('workerName').value;
  }

  show(): void {
    this.commentOffice = this.workingHoursForm.get('commentOffice').value;
    this.frame.show();
  }

  save() {
    this.workingHoursForm.get('commentOffice').setValue(this.commentOffice);
    this.hide();
  }

  hide() {
    this.frame.hide();
  }

  ngOnDestroy(): void {}
}

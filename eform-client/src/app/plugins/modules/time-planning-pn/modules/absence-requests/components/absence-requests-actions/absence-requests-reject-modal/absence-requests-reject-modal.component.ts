import { Component, OnInit, inject } from '@angular/core';
import { MAT_DIALOG_DATA, MatDialogRef } from '@angular/material/dialog';
import { TimePlanningPnAbsenceRequestsService } from 'src/app/plugins/modules/time-planning-pn/services';
import { AbsenceRequestModel, AbsenceRequestDecisionModel } from 'src/app/plugins/modules/time-planning-pn/models';

@Component({
  selector: 'app-absence-requests-reject-modal',
  templateUrl: './absence-requests-reject-modal.component.html',
  standalone: false
})
export class AbsenceRequestsRejectModalComponent implements OnInit {
  private absenceRequestsService = inject(TimePlanningPnAbsenceRequestsService);
  public dialogRef = inject(MatDialogRef<AbsenceRequestsRejectModalComponent>);
  private model = inject<{
    selectedAbsenceRequest: AbsenceRequestModel
  }>(MAT_DIALOG_DATA);

  selectedAbsenceRequest: AbsenceRequestModel;
  decisionComment: string = '';
  managerSdkSitId: number = 1; // TODO: Get from user context

  ngOnInit() {
    this.selectedAbsenceRequest = { ...this.model.selectedAbsenceRequest };
  }

  hide(result = false) {
    this.dialogRef.close(result);
  }

  reject() {
    const decision: AbsenceRequestDecisionModel = {
      managerSdkSitId: this.managerSdkSitId,
      decisionComment: this.decisionComment || undefined
    };

    this.absenceRequestsService
      .reject(this.selectedAbsenceRequest.id, decision)
      .subscribe((data) => {
        if (data && data.success) {
          this.hide(true);
        }
      });
  }
}

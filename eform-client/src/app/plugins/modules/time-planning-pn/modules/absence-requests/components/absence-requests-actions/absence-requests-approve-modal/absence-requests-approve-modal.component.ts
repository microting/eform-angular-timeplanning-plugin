import { Component, OnInit, inject } from '@angular/core';
import { MAT_DIALOG_DATA, MatDialogRef } from '@angular/material/dialog';
import { TimePlanningPnAbsenceRequestsService } from 'src/app/plugins/modules/time-planning-pn/services';
import { AbsenceRequestModel, AbsenceRequestDecisionModel } from 'src/app/plugins/modules/time-planning-pn/models';

@Component({
  selector: 'app-absence-requests-approve-modal',
  templateUrl: './absence-requests-approve-modal.component.html',
  standalone: false
})
export class AbsenceRequestsApproveModalComponent implements OnInit {
  private absenceRequestsService = inject(TimePlanningPnAbsenceRequestsService);
  public dialogRef = inject(MatDialogRef<AbsenceRequestsApproveModalComponent>);
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

  approve() {
    const decision: AbsenceRequestDecisionModel = {
      managerSdkSitId: this.managerSdkSitId,
      decisionComment: this.decisionComment || undefined
    };

    this.absenceRequestsService
      .approve(this.selectedAbsenceRequest.id, decision)
      .subscribe((data) => {
        if (data && data.success) {
          this.hide(true);
        }
      });
  }
}

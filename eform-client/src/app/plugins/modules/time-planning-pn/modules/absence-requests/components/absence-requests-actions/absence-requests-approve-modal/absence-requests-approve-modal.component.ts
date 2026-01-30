import { Component, OnInit, inject } from '@angular/core';
import { MAT_DIALOG_DATA, MatDialogRef } from '@angular/material/dialog';
import { TimePlanningPnAbsenceRequestsService } from 'src/app/plugins/modules/time-planning-pn/services';
import { AbsenceRequestModel, AbsenceRequestDecisionModel } from 'src/app/plugins/modules/time-planning-pn/models';
import { ToastrService } from 'ngx-toastr';
import { TranslateService } from '@ngx-translate/core';

@Component({
  selector: 'app-absence-requests-approve-modal',
  templateUrl: './absence-requests-approve-modal.component.html',
  standalone: false
})
export class AbsenceRequestsApproveModalComponent implements OnInit {
  private absenceRequestsService = inject(TimePlanningPnAbsenceRequestsService);
  public dialogRef = inject(MatDialogRef<AbsenceRequestsApproveModalComponent>);
  private toastrService = inject(ToastrService);
  private translateService = inject(TranslateService);
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
      .subscribe({
        next: (data) => {
          if (data && data.success) {
            this.toastrService.success(
              this.translateService.instant('Absence request approved successfully')
            );
            this.hide(true);
          }
        },
        error: () => {
          this.toastrService.error(
            this.translateService.instant('Error approving absence request')
          );
        }
      });
  }
}

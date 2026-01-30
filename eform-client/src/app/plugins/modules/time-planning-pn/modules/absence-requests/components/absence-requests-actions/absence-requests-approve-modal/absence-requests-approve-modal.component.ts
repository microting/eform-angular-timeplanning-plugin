import { Component, OnInit, inject } from '@angular/core';
import { MAT_DIALOG_DATA, MatDialogRef } from '@angular/material/dialog';
import { TimePlanningPnAbsenceRequestsService } from 'src/app/plugins/modules/time-planning-pn/services';
import { AbsenceRequestModel, AbsenceRequestDecisionModel } from 'src/app/plugins/modules/time-planning-pn/models';
import { ToastrService } from 'ngx-toastr';
import { TranslateService } from '@ngx-translate/core';
import { Store } from '@ngrx/store';
import { selectCurrentUserId } from 'src/app/state';
import { take } from 'rxjs';

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
  private store = inject(Store);
  private model = inject<{
    selectedAbsenceRequest: AbsenceRequestModel
  }>(MAT_DIALOG_DATA);

  selectedAbsenceRequest: AbsenceRequestModel;
  decisionComment: string = '';
  currentUserId: number | null = null;

  ngOnInit() {
    this.selectedAbsenceRequest = { ...this.model.selectedAbsenceRequest };
    // Get current user ID (take only once)
    this.store.select(selectCurrentUserId).pipe(take(1)).subscribe((userId) => {
      this.currentUserId = userId;
    });
  }

  hide(result = false) {
    this.dialogRef.close(result);
  }

  approve() {
    if (!this.currentUserId) {
      this.toastrService.error(
        this.translateService.instant('User not authenticated')
      );
      return;
    }

    const decision: AbsenceRequestDecisionModel = {
      managerSdkSitId: this.currentUserId,
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

import { Component, OnInit, inject } from '@angular/core';
import {MAT_DIALOG_DATA, MatDialogRef, MatDialogTitle, MatDialogContent, MatDialogActions} from '@angular/material/dialog';
import { TimePlanningPnAbsenceRequestsService } from 'src/app/plugins/modules/time-planning-pn/services';
import { AbsenceRequestModel, AbsenceRequestDecisionModel } from 'src/app/plugins/modules/time-planning-pn/models';
import { ToastrService } from 'ngx-toastr';
import {TranslateService, TranslatePipe} from '@ngx-translate/core';
import { Store } from '@ngrx/store';
import { selectAuthUser } from 'src/app/state';
import { take } from 'rxjs';
import {NgIf, DatePipe} from '@angular/common';
import {FormsModule} from '@angular/forms';
import {MatFormField, MatLabel} from '@angular/material/form-field';
import {MatInput} from '@angular/material/input';

@Component({
  selector: 'app-absence-requests-reject-modal',
  imports: [NgIf, DatePipe, FormsModule, TranslatePipe, MatFormField, MatLabel, MatInput, MatDialogTitle, MatDialogContent, MatDialogActions],
  templateUrl: './absence-requests-reject-modal.component.html',
})
export class AbsenceRequestsRejectModalComponent implements OnInit {
  private absenceRequestsService = inject(TimePlanningPnAbsenceRequestsService);
  public dialogRef = inject(MatDialogRef<AbsenceRequestsRejectModalComponent>);
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
    this.store.select(selectAuthUser).pipe(take(1)).subscribe((user) => {
      this.currentUserId = user?.id || null;
    });
  }

  hide(result = false) {
    this.dialogRef.close(result);
  }

  reject() {
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
      .reject(this.selectedAbsenceRequest.id, decision)
      .subscribe({
        next: (data) => {
          if (data && data.success) {
            this.toastrService.success(
              this.translateService.instant('Absence request rejected successfully')
            );
            this.hide(true);
          }
        },
        error: () => {
          this.toastrService.error(
            this.translateService.instant('Error rejecting absence request')
          );
        }
      });
  }
}

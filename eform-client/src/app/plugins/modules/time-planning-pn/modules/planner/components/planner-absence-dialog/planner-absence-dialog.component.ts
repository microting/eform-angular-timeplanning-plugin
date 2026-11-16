import {Component, inject} from '@angular/core';
import {FormsModule} from '@angular/forms';
import {MAT_DIALOG_DATA, MatDialogActions, MatDialogContent, MatDialogRef, MatDialogTitle} from '@angular/material/dialog';
import {MatDatepickerToggle, MatDateRangeInput, MatDateRangePicker, MatEndDate, MatStartDate} from '@angular/material/datepicker';
import {MatFormField, MatHint, MatLabel, MatSuffix} from '@angular/material/form-field';
import {MatInput} from '@angular/material/input';
import {MatOption, MatSelect} from '@angular/material/select';
import {TranslatePipe, TranslateService} from '@ngx-translate/core';
import {ToastrService} from 'ngx-toastr';
import {PlannerWorkerModel} from '../../models';
import {TimePlanningPnPlannerService} from '../../services';
import {formatPlannerDate, parseCalendarDate, toTransportDate} from '../../planner.utils';
import {PLANNER_ABSENCE_TYPES} from '../../planner-absence-types';

export interface PlannerAbsenceDialogData {
  workers: PlannerWorkerModel[];
  assignedSiteId?: number | null;
  from?: Date | null;
  to?: Date | null;
}

/**
 * Screen 4: a manager registers an absence over a period (#5489). The server
 * creates an approved absence request in one transaction; a locked day refuses
 * the whole request, and the locked dates are listed here.
 */
@Component({
  selector: 'app-planner-absence-dialog',
  imports: [
    FormsModule, TranslatePipe, MatDialogTitle, MatDialogContent, MatDialogActions, MatDateRangeInput,
    MatDateRangePicker, MatDatepickerToggle, MatStartDate, MatEndDate, MatFormField, MatLabel, MatHint, MatSuffix,
    MatInput, MatSelect, MatOption,
  ],
  templateUrl: './planner-absence-dialog.component.html',
  styleUrls: ['./planner-absence-dialog.component.scss'],
})
export class PlannerAbsenceDialogComponent {
  private plannerService = inject(TimePlanningPnPlannerService);
  private toastrService = inject(ToastrService);
  private translateService = inject(TranslateService);
  public dialogRef = inject(MatDialogRef<PlannerAbsenceDialogComponent>);
  public data = inject<PlannerAbsenceDialogData>(MAT_DIALOG_DATA);

  readonly absenceTypes = PLANNER_ABSENCE_TYPES;

  assignedSiteId: number | null = this.data.assignedSiteId ?? null;
  from: Date | null = this.data.from ?? null;
  to: Date | null = this.data.to ?? null;
  messageId: number | null = null;
  comment = '';
  busy = false;
  lockedDates: Date[] = [];

  get selectedWorker(): PlannerWorkerModel | undefined {
    return this.data.workers.find(w => w.assignedSiteId === this.assignedSiteId);
  }

  get canSubmit(): boolean {
    return !this.busy && !!this.selectedWorker?.canApproveAbsence && !!this.from && !!this.to && this.messageId != null;
  }

  submit(): void {
    if (!this.canSubmit) {
      return;
    }
    this.busy = true;
    this.lockedDates = [];
    this.plannerService.createAbsence({
      assignedSiteId: this.assignedSiteId,
      from: toTransportDate(this.from),
      to: toTransportDate(this.to),
      messageId: this.messageId,
      comment: this.comment.trim() || null,
    }).subscribe({
      next: (result) => {
        this.busy = false;
        if (result && result.success && result.model) {
          this.toastrService.success(this.translateService.instant('plannerAbsenceRegistered', {
            applied: result.model.appliedDates.length,
            skipped: result.model.skippedDates.length,
          }));
          this.dialogRef.close(true);
          return;
        }
        // The refusal itself was toasted by ApiBaseService; the locked days are listed here.
        this.lockedDates = (result?.model?.lockedDates ?? []).map(parseCalendarDate);
      },
      error: () => {
        this.busy = false;
        this.toastrService.error(this.translateService.instant('plannerErrorOccurred'));
      },
    });
  }

  /**
   * Not while the POST is in flight: the server would still register the
   * absence, and the grid only reloads on a true close. Escape and the
   * backdrop are already off (dialogConfigHelper sets disableClose).
   */
  cancel(): void {
    if (this.busy) {
      return;
    }
    this.dialogRef.close(false);
  }

  formatDate(date: Date): string {
    return formatPlannerDate(this.translateService, date);
  }
}

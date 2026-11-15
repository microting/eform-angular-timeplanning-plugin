import {Component, OnInit, inject} from '@angular/core';
import {FormsModule} from '@angular/forms';
import {MAT_DIALOG_DATA, MatDialogActions, MatDialogContent, MatDialogRef, MatDialogTitle} from '@angular/material/dialog';
import {MatButtonToggle, MatButtonToggleGroup} from '@angular/material/button-toggle';
import {MatCheckbox} from '@angular/material/checkbox';
import {MatDatepicker, MatDatepickerInput, MatDatepickerToggle} from '@angular/material/datepicker';
import {MatFormField, MatLabel, MatSuffix} from '@angular/material/form-field';
import {MatIcon} from '@angular/material/icon';
import {MatInput} from '@angular/material/input';
import {MatOption, MatSelect} from '@angular/material/select';
import {MatProgressSpinner} from '@angular/material/progress-spinner';
import {TranslatePipe, TranslateService} from '@ngx-translate/core';
import {ToastrService} from 'ngx-toastr';
import {PlanPatternDayModel, PlannerPatternModel, PlannerWorkerModel} from '../../models';
import {TimePlanningPnPlannerService} from '../../services';
import {
  formatPlannerDate, formatPlannerHours, fullPatternGrid, isValidPattern, isValidPatternDayText, mondaysOnly,
  parseCalendarDate, ROTATION_WEEK_OPTIONS, toTransportDate, trimmedPatternDays,
} from '../../planner.utils';
import {PlannerPatternDaysComponent} from '../planner-pattern-days/planner-pattern-days.component';

export interface PlannerPatternDialogData {
  worker: PlannerWorkerModel;
  /** Other workers in the grid that have a pattern ("Kopiér fra kollega"). */
  colleagues: PlannerWorkerModel[];
}

/** "Samme uge hver uge" / "Rotation" / "Ingen fast plan". */
export type PlannerPatternForm = 'same' | 'rotation' | 'none';

/**
 * Screen 3, the pattern editor (#5489). Closes with true when something was
 * saved or deleted, so the grid reloads.
 */
@Component({
  selector: 'app-planner-pattern-dialog',
  imports: [
    FormsModule, TranslatePipe, MatDialogTitle, MatDialogContent, MatDialogActions, MatButtonToggleGroup,
    MatButtonToggle, MatCheckbox, MatDatepicker, MatDatepickerInput, MatDatepickerToggle, MatFormField, MatLabel,
    MatSuffix, MatIcon, MatInput, MatSelect, MatOption, MatProgressSpinner, PlannerPatternDaysComponent,
  ],
  templateUrl: './planner-pattern-dialog.component.html',
  styleUrls: ['./planner-pattern-dialog.component.scss'],
})
export class PlannerPatternDialogComponent implements OnInit {
  private plannerService = inject(TimePlanningPnPlannerService);
  private toastrService = inject(ToastrService);
  private translateService = inject(TranslateService);
  public dialogRef = inject(MatDialogRef<PlannerPatternDialogComponent>);
  public data = inject<PlannerPatternDialogData>(MAT_DIALOG_DATA);

  readonly rotationOptions = ROTATION_WEEK_OPTIONS.filter(n => n > 1);

  loading = true;
  busy = false;
  /** 'edit', then 'confirm' (manual days) or 'confirmDelete'. */
  step: 'edit' | 'confirm' | 'confirmDelete' = 'edit';

  form: PlannerPatternForm = 'same';
  rotationWeeks = 1;
  anchorDate: Date;
  validFrom: Date;
  days: PlanPatternDayModel[] = fullPatternGrid(1, []);
  isPrefill = false;
  copySourceId: number | null = null;

  manualDays: Date[] = [];
  overwriteManualDays = false;

  readonly mondaysOnly = mondaysOnly;

  ngOnInit(): void {
    this.plannerService.getPattern(this.data.worker.assignedSiteId).subscribe({
      next: (result) => {
        this.loading = false;
        if (result && result.success && result.model) {
          this.apply(result.model);
        } else {
          this.dialogRef.close(false);
        }
      },
      error: () => {
        this.loading = false;
        this.toastrService.error(this.translateService.instant('plannerLoadFailed'));
        this.dialogRef.close(false);
      },
    });
  }

  private apply(model: PlannerPatternModel): void {
    this.isPrefill = model.isPrefill;
    this.rotationWeeks = model.rotationWeeks;
    this.form = model.rotationWeeks > 1 ? 'rotation' : 'same';
    this.anchorDate = parseCalendarDate(model.anchorDate);
    this.validFrom = parseCalendarDate(model.validFrom);
    this.days = fullPatternGrid(model.rotationWeeks, model.days);
  }

  get hasSavedPattern(): boolean {
    return this.data.worker.hasPattern;
  }

  onFormChange(form: PlannerPatternForm): void {
    this.form = form;
    if (form === 'same') {
      this.setRotationWeeks(1);
    } else if (form === 'rotation' && this.rotationWeeks < 2) {
      this.setRotationWeeks(2);
    }
  }

  setRotationWeeks(weeks: number): void {
    this.rotationWeeks = weeks;
    this.days = fullPatternGrid(weeks, this.days);
  }

  copyFromColleague(): void {
    if (this.copySourceId == null) {
      return;
    }
    this.busy = true;
    this.plannerService.copyPattern(this.data.worker.assignedSiteId, this.copySourceId).subscribe({
      next: (result) => {
        this.busy = false;
        if (result && result.success && result.model) {
          this.apply(result.model);
          this.toastrService.info(this.translateService.instant('plannerPatternCopied'));
        }
      },
      error: () => {
        this.busy = false;
        this.toastrService.error(this.translateService.instant('plannerErrorOccurred'));
      },
    });
  }

  get daysValid(): boolean {
    return this.days.every(d => isValidPatternDayText(d.planText));
  }

  get canSave(): boolean {
    if (this.form === 'none') {
      return true;
    }
    return !this.busy && isValidPattern(this.anchorDate, this.days) && !!this.validFrom;
  }

  /** "Gem": deletes for "Ingen fast plan", else checks for manual days first. */
  save(): void {
    if (this.form === 'none') {
      if (this.hasSavedPattern) {
        this.step = 'confirmDelete';
      } else {
        this.dialogRef.close(false);
      }
      return;
    }
    if (!this.canSave) {
      return;
    }
    this.busy = true;
    this.plannerService.getManualDays(this.data.worker.assignedSiteId, toTransportDate(this.validFrom)).subscribe({
      next: (result) => {
        this.busy = false;
        if (!result || !result.success) {
          return;
        }
        this.manualDays = (result.model ?? []).map(parseCalendarDate);
        if (this.manualDays.length > 0) {
          this.overwriteManualDays = false;
          this.step = 'confirm';
        } else {
          this.persist(false);
        }
      },
      error: () => {
        this.busy = false;
        this.toastrService.error(this.translateService.instant('plannerErrorOccurred'));
      },
    });
  }

  persist(overwriteManualDays: boolean): void {
    this.busy = true;
    this.plannerService.savePattern(this.data.worker.assignedSiteId, {
      rotationWeeks: this.rotationWeeks,
      anchorDate: toTransportDate(this.anchorDate),
      validFrom: toTransportDate(this.validFrom),
      days: trimmedPatternDays(this.days),
      overwriteManualDays,
    }).subscribe({
      next: (result) => {
        this.busy = false;
        if (!result || !result.success) {
          // NoToast call: the refusal is worded by the server, shown here.
          this.toastrService.error(result?.message || this.translateService.instant('plannerErrorOccurred'));
          return;
        }
        const applied = result.model;
        if (applied?.warningKey && result.message) {
          this.toastrService.warning(result.message);
        }
        if (applied?.lockedDays?.length) {
          this.toastrService.warning(
            this.translateService.instant('plannerDaysLockedNotChanged', {count: applied.lockedDays.length}));
        }
        this.toastrService.success(
          this.translateService.instant('plannerPatternSaved', {count: applied?.rowsWritten ?? 0}));
        this.dialogRef.close(true);
      },
      error: () => {
        this.busy = false;
        this.toastrService.error(this.translateService.instant('plannerErrorOccurred'));
      },
    });
  }

  deletePattern(): void {
    this.busy = true;
    this.plannerService.deletePattern(this.data.worker.assignedSiteId).subscribe({
      next: (result) => {
        this.busy = false;
        if (result && result.success) {
          this.toastrService.success(this.translateService.instant('plannerPatternDeleted'));
          this.dialogRef.close(true);
        } else {
          // NoToast call: the refusal is worded by the server, shown here.
          this.toastrService.error(result?.message || this.translateService.instant('plannerErrorOccurred'));
        }
      },
      error: () => {
        this.busy = false;
        this.toastrService.error(this.translateService.instant('plannerErrorOccurred'));
      },
    });
  }

  back(): void {
    this.step = 'edit';
  }

  cancel(): void {
    this.dialogRef.close(false);
  }

  formatDate(date: Date): string {
    return formatPlannerDate(this.translateService, date);
  }

  get normHours(): string {
    return formatPlannerHours(this.translateService, this.data.worker.weeklyNormHours);
  }
}

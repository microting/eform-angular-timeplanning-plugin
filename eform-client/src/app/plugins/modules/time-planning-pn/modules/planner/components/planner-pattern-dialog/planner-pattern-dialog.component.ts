import {Component, OnInit, inject} from '@angular/core';
import {FormsModule} from '@angular/forms';
import {MAT_DIALOG_DATA, MatDialogActions, MatDialogContent, MatDialogRef, MatDialogTitle} from '@angular/material/dialog';
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
  onNoReply, parseCalendarDate, ROTATION_WEEK_OPTIONS, segmentedKeydown, toTransportDate, trimmedPatternDays,
  workerInitials,
} from '../../planner.utils';
import {PlannerPatternDaysComponent} from '../planner-pattern-days/planner-pattern-days.component';

export interface PlannerPatternDialogData {
  /** The worker whose pattern is edited; null opens on a worker picker ("Ugemønstre" in the header). */
  worker: PlannerWorkerModel | null;
  /** Other workers in the grid that have a pattern ("Kopiér fra kollega"). Derived from `workers` when picking. */
  colleagues?: PlannerWorkerModel[];
  /** The workers to pick from when `worker` is null. */
  workers?: PlannerWorkerModel[];
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
    FormsModule, TranslatePipe, MatDialogTitle, MatDialogContent, MatDialogActions, MatCheckbox, MatDatepicker,
    MatDatepickerInput, MatDatepickerToggle, MatFormField, MatLabel, MatSuffix, MatIcon, MatInput, MatSelect, MatOption,
    MatProgressSpinner, PlannerPatternDaysComponent,
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
  readonly forms: { value: PlannerPatternForm; key: string }[] = [
    {value: 'same', key: 'plannerFormSameWeek'},
    {value: 'rotation', key: 'plannerFormRotation'},
    {value: 'none', key: 'plannerFormNoPlan'},
  ];
  readonly initials = workerInitials;

  /** Null until a worker is picked (opened from "Ugemønstre"). */
  worker: PlannerWorkerModel | null = this.data.worker;
  loading = true;
  busy = false;
  /** 'pick' (no worker yet), 'edit', then 'confirm' (manual days) or 'confirmDelete'. */
  step: 'pick' | 'edit' | 'confirm' | 'confirmDelete' = this.data.worker ? 'edit' : 'pick';

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
    if (this.worker) {
      this.loadPattern();
    } else {
      this.loading = false;
    }
  }

  /** Workers offered by the picker. */
  get pickableWorkers(): PlannerWorkerModel[] {
    return this.data.workers ?? [];
  }

  /** "Kopiér fra kollega": the other workers with a pattern. */
  get colleagues(): PlannerWorkerModel[] {
    if (this.data.colleagues && this.worker === this.data.worker) {
      return this.data.colleagues;
    }
    return this.pickableWorkers.filter(w => w.hasPattern && w.assignedSiteId !== this.worker?.assignedSiteId);
  }

  /** The picker: the pattern of the chosen worker is read and the editor opens. */
  pickWorker(assignedSiteId: number): void {
    const worker = this.pickableWorkers.find(w => w.assignedSiteId === assignedSiteId);
    if (!worker) {
      return;
    }
    this.worker = worker;
    this.step = 'edit';
    this.loading = true;
    this.loadPattern();
  }

  private loadPattern(): void {
    // No reply to the read: nothing to edit, so close as the refusal does.
    this.plannerService.getPattern(this.worker.assignedSiteId).pipe(onNoReply(() => {
      this.loading = false;
      this.dialogRef.close(false);
    })).subscribe({
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
    return !!this.worker?.hasPattern;
  }

  onFormKeydown(event: KeyboardEvent, index: number): void {
    const next = segmentedKeydown(event, index, this.forms.length);
    if (next !== null && this.forms[next].value !== this.form) {
      this.onFormChange(this.forms[next].value);
    }
  }

  onRotationKeydown(event: KeyboardEvent, index: number): void {
    const next = segmentedKeydown(event, index, this.rotationOptions.length);
    if (next !== null && this.rotationOptions[next] !== this.rotationWeeks) {
      this.setRotationWeeks(this.rotationOptions[next]);
    }
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
    this.plannerService.copyPattern(this.worker.assignedSiteId, this.copySourceId)
      .pipe(onNoReply(() => { this.busy = false; })).subscribe({
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
    // The flag is per request: when the lookup emits it starts the save, and
    // the lookup's own completion then leaves the save's busy state alone.
    this.plannerService.getManualDays(this.worker.assignedSiteId, toTransportDate(this.validFrom))
      .pipe(onNoReply(() => { this.busy = false; })).subscribe({
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
    this.plannerService.savePattern(this.worker.assignedSiteId, {
      rotationWeeks: this.rotationWeeks,
      anchorDate: toTransportDate(this.anchorDate),
      validFrom: toTransportDate(this.validFrom),
      days: trimmedPatternDays(this.days),
      overwriteManualDays,
    }).pipe(onNoReply(() => this.outcomeUnknown())).subscribe({
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
    this.plannerService.deletePattern(this.worker.assignedSiteId)
      .pipe(onNoReply(() => this.outcomeUnknown())).subscribe({
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

  /**
   * A write that completed without a reply (the HTTP interceptor's EMPTY): it
   * may or may not have been applied, so close as "changed" and let the grid
   * reload and show the true state.
   */
  private outcomeUnknown(): void {
    this.busy = false;
    this.toastrService.warning(this.translateService.instant('plannerOutcomeUnknown'));
    this.dialogRef.close(true);
  }

  back(): void {
    if (this.busy) {
      return;
    }
    this.step = 'edit';
  }

  /**
   * Not while a save, delete or lookup is in flight: a PUT the server still
   * applies would otherwise close as "nothing changed" and the grid would not
   * reload. Escape and the backdrop are already off (dialogConfigHelper sets
   * disableClose).
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

  get normHours(): string {
    return formatPlannerHours(this.translateService, this.worker?.weeklyNormHours);
  }
}

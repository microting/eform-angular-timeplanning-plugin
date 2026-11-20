import {Component, OnInit, inject} from '@angular/core';
import {FormsModule} from '@angular/forms';
import {MatDialogActions, MatDialogContent, MatDialogRef, MatDialogTitle} from '@angular/material/dialog';
import {MatCheckbox} from '@angular/material/checkbox';
import {MatDatepicker, MatDatepickerInput, MatDatepickerToggle} from '@angular/material/datepicker';
import {MatFormField, MatSuffix} from '@angular/material/form-field';
import {MatIcon} from '@angular/material/icon';
import {MatIconButton} from '@angular/material/button';
import {MatInput} from '@angular/material/input';
import {MatOption, MatSelect} from '@angular/material/select';
import {MatProgressSpinner} from '@angular/material/progress-spinner';
import {MatTooltip} from '@angular/material/tooltip';
import {TranslatePipe, TranslateService} from '@ngx-translate/core';
import {ToastrService} from 'ngx-toastr';
import {
  PlanPatternDayModel, PlannerImportAppliedModel, PlannerImportConfidences, PlannerImportReasons, PlannerImportWorkerModel,
} from '../../models';
import {TimePlanningPnPlannerService} from '../../services';
import {
  formatPlannerDate, fullPatternGrid, isEmptyPattern, isValidPattern, mondaysOnly, onNoReply, parseCalendarDate,
  ROTATION_WEEK_OPTIONS, toTransportDate, trimmedPatternDays, WEEKDAY_KEYS,
} from '../../planner.utils';
import {PlannerPatternDaysComponent} from '../planner-pattern-days/planner-pattern-days.component';

/** One worker in the review step. */
export interface PlannerImportRow {
  worker: PlannerImportWorkerModel;
  include: boolean;
  rotationWeeks: number;
  anchorDate: Date | null;
  days: PlanPatternDayModel[];
  expanded: boolean;
  /**
   * Recognised, but no day holds a shift: the history is all days off, which
   * the canvas calls "Ingen plan (timelønnet)". Not ticked by default, so the
   * import never writes an all-off pattern unless asked to.
   */
  noPlan: boolean;
}

/** The translation key for a RotationInference reason code; every code the server sends has one. */
export function importReasonKey(reason: string | null): string {
  switch (reason) {
    case PlannerImportReasons.inconsistent: return 'plannerImportReasonInconsistent';
    case PlannerImportReasons.unparsable: return 'plannerImportReasonUnparsable';
    case PlannerImportReasons.insufficientHistory: return 'plannerImportReasonInsufficientHistory';
    case PlannerImportReasons.hoursOnly: return 'plannerImportReasonHoursOnly';
    default: return 'plannerImportReasonUnknown';
  }
}

/** The "Sikkerhed" chip of a recognised worker: its label key and colour class, or null for no chip. */
export function importConfidenceChip(confidence: string | null): { key: string; chipClass: string } | null {
  switch (confidence) {
    case PlannerImportConfidences.high: return {key: 'plannerConfidenceHigh', chipClass: 'planner-chip-success'};
    case PlannerImportConfidences.medium: return {key: 'plannerConfidenceMedium', chipClass: 'planner-chip-warning'};
    default: return null;
  }
}

/**
 * Screen 6, the one-off import from the Google Sheet (#5489): preview, then
 * review and edit, then apply. Applying retires the sheet in the same
 * transaction, so the grid reloads afterwards (closes with true).
 */
@Component({
  selector: 'app-planner-import-dialog',
  imports: [
    FormsModule, TranslatePipe, MatDialogTitle, MatDialogContent, MatDialogActions, MatCheckbox, MatDatepicker,
    MatDatepickerInput, MatDatepickerToggle, MatFormField, MatSuffix, MatIcon, MatIconButton, MatInput,
    MatSelect, MatOption, MatProgressSpinner, MatTooltip, PlannerPatternDaysComponent,
  ],
  templateUrl: './planner-import-dialog.component.html',
  styleUrls: ['./planner-import-dialog.component.scss'],
})
export class PlannerImportDialogComponent implements OnInit {
  private plannerService = inject(TimePlanningPnPlannerService);
  private toastrService = inject(ToastrService);
  private translateService = inject(TranslateService);
  public dialogRef = inject(MatDialogRef<PlannerImportDialogComponent>);

  readonly rotationOptions = ROTATION_WEEK_OPTIONS;
  readonly importReasonKey = importReasonKey;
  readonly importConfidenceChip = importConfidenceChip;
  readonly mondaysOnly = mondaysOnly;

  step: 'preview' | 'review' | 'apply' | 'done' = 'preview';
  loading = true;
  busy = false;
  windowStart: Date | null = null;
  rows: PlannerImportRow[] = [];
  results: { name: string; result: PlannerImportAppliedModel }[] = [];

  ngOnInit(): void {
    // No reply to the preview: nothing to review (an empty list would read as
    // "nobody on the sheet"), so close as the refusal does.
    this.plannerService.getImportPreview().pipe(onNoReply(() => {
      this.loading = false;
      this.dialogRef.close(false);
    })).subscribe({
      next: (result) => {
        this.loading = false;
        if (!result || !result.success || !result.model) {
          this.dialogRef.close(false);
          return;
        }
        this.windowStart = parseCalendarDate(result.model.windowStart);
        this.rows = result.model.workers.map(worker => {
          const weeks = worker.rotationWeeks ?? 1;
          const days = fullPatternGrid(weeks, worker.days ?? []);
          const noPlan = worker.recognised && isEmptyPattern(days);
          return {
            worker,
            include: worker.recognised && !noPlan,
            rotationWeeks: weeks,
            anchorDate: worker.anchorDate ? parseCalendarDate(worker.anchorDate) : this.windowStart,
            days,
            expanded: false,
            noPlan,
          };
        });
      },
      error: () => {
        this.loading = false;
        this.toastrService.error(this.translateService.instant('plannerLoadFailed'));
        this.dialogRef.close(false);
      },
    });
  }

  /** "Trin n af 3": the apply and done steps are both step 3. */
  get stepNumber(): number {
    if (this.step === 'preview') {
      return 1;
    }
    return this.step === 'review' ? 2 : 3;
  }

  get recognisedCount(): number {
    return this.rows.filter(r => r.worker.recognised).length;
  }

  get unrecognisedCount(): number {
    return this.rows.length - this.recognisedCount;
  }

  get includedRows(): PlannerImportRow[] {
    return this.rows.filter(r => r.worker.recognised && r.include);
  }

  rowValid(row: PlannerImportRow): boolean {
    return isValidPattern(row.anchorDate, row.days);
  }

  get reviewValid(): boolean {
    return this.includedRows.every(r => this.rowValid(r));
  }

  setRotationWeeks(row: PlannerImportRow, weeks: number): void {
    row.rotationWeeks = weeks;
    row.days = fullPatternGrid(weeks, row.days);
  }

  /** A short read-only summary of week 1: "Man 7:00-15:00/0.5 · Tir …". */
  summary(row: PlannerImportRow): string {
    const dayNames = WEEKDAY_KEYS.map(k => this.translateService.instant(k).substring(0, 3));
    const worked = row.days.filter(d => d.weekIndex === 0 && (d.planText ?? '').trim());
    return worked.length === 0
      ? this.translateService.instant('plannerNoPlannedDays')
      : worked.map(d => `${dayNames[d.dayOfWeek]} ${d.planText}`).join(' · ');
  }

  next(): void {
    if (this.step === 'preview') {
      this.step = 'review';
    } else if (this.step === 'review' && this.reviewValid) {
      this.step = 'apply';
    }
  }

  back(): void {
    if (this.step === 'apply') {
      this.step = 'review';
    } else if (this.step === 'review') {
      this.step = 'preview';
    }
  }

  apply(): void {
    this.busy = true;
    const included = this.includedRows;
    this.plannerService.applyImport({
      patterns: included.map(r => ({
        assignedSiteId: r.worker.assignedSiteId,
        rotationWeeks: r.rotationWeeks,
        anchorDate: toTransportDate(r.anchorDate),
        validFrom: null,
        days: trimmedPatternDays(r.days),
      })),
    }).pipe(onNoReply(() => this.outcomeUnknown())).subscribe({
      next: (result) => {
        this.busy = false;
        if (!result || !result.success || !result.model) {
          return;
        }
        const names = new Map(this.rows.map(r => [r.worker.assignedSiteId, r.worker.siteName]));
        this.results = result.model.workers.map(w => ({name: names.get(w.assignedSiteId) ?? `#${w.assignedSiteId}`, result: w}));
        for (const r of this.results.filter(x => x.result.warning)) {
          this.toastrService.warning(`${r.name}: ${r.result.warning}`);
        }
        this.toastrService.success(this.translateService.instant('plannerImportDone', {count: this.results.length}));
        this.step = 'done';
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

  /** Not while the import is applied: it retires the sheet, and the grid must reload. */
  close(): void {
    if (this.busy) {
      return;
    }
    this.dialogRef.close(this.step === 'done');
  }

  formatDate(date: Date | null): string {
    return formatPlannerDate(this.translateService, date, {day: 'numeric', month: 'short', year: 'numeric'});
  }
}

import {Component, DestroyRef, inject} from '@angular/core';
import {takeUntilDestroyed} from '@angular/core/rxjs-interop';
import {FormsModule} from '@angular/forms';
import {MAT_DIALOG_DATA, MatDialogActions, MatDialogContent, MatDialogRef, MatDialogTitle} from '@angular/material/dialog';
import {
  MatDatepicker, MatDatepickerInput, MatDatepickerToggle, MatDateRangeInput, MatDateRangePicker, MatEndDate,
  MatStartDate,
} from '@angular/material/datepicker';
import {MatFormField, MatHint, MatLabel, MatSuffix} from '@angular/material/form-field';
import {MatIcon} from '@angular/material/icon';
import {MatInput} from '@angular/material/input';
import {MatProgressSpinner} from '@angular/material/progress-spinner';
import {MatOption, MatSelect, MatSelectTrigger} from '@angular/material/select';
import {TranslatePipe, TranslateService} from '@ngx-translate/core';
import {ToastrService} from 'ngx-toastr';
import {catchError, EMPTY, map, Subject, switchMap, timer} from 'rxjs';
import {OperationDataResult} from 'src/app/common/models';
import {
  PlannerAbsenceOutcome, PlannerAbsenceOutcomes, PlannerAbsencePreviewDayModel, PlannerAbsencePreviewModel,
  PlannerAbsencePreviewRequestModel, PlannerWorkerModel,
} from '../../models';
import {TimePlanningPnPlannerService} from '../../services';
import {
  dateKey, formatPlannerDate, formatPlannerHours, isoDayOfWeek, labelInSentence, onNoReply, parseCalendarDate, segmentedKeydown,
  toTransportDate, workerInitials,
} from '../../planner.utils';
import {absenceTypeKey, DEFAULT_ABSENCE_TYPE, PLANNER_ABSENCE_TYPES} from '../../planner-absence-types';

export interface PlannerAbsenceDialogData {
  workers: PlannerWorkerModel[];
  assignedSiteId?: number | null;
  from?: Date | null;
  to?: Date | null;
  /** The signed-in user is an admin: the "set up as leader" advice is for everyone else. */
  isAdmin?: boolean;
}

/** Why "Registrér" is disabled, in the order the "Mangler:" line lists them. */
export type PlannerAbsenceMissing = 'worker' | 'notLeader' | 'type' | 'dates' | 'noWorkDays' | 'locked' | 'overlap';

export type PlannerAbsencePreviewState = 'idle' | 'loading' | 'ready' | 'failed';

/** One row of the per-day preview, ready to show. */
export interface PlannerAbsencePreviewRow {
  key: string;
  date: Date;
  planText: string;
  planHours: number;
  outcome: PlannerAbsenceOutcome | 'other';
}

const KNOWN_OUTCOMES: string[] = Object.values(PlannerAbsenceOutcomes);

const MISSING_KEYS: Record<PlannerAbsenceMissing, string> = {
  worker: 'plannerMissingWorker',
  notLeader: 'plannerMissingNotLeader',
  type: 'plannerMissingType',
  dates: 'plannerMissingDates',
  noWorkDays: 'plannerMissingNoWorkDays',
  locked: 'plannerMissingLocked',
  overlap: 'plannerMissingOverlap',
};

/** Typing a date fires a change per keystroke; the preview waits for a pause. */
export const PREVIEW_DEBOUNCE_MS = 250;

/**
 * Screen 4: a manager registers an absence over a period (#5489). The per-day
 * preview (planner/absence/preview) shows which days become absence before
 * anything is written; the server creates an approved absence request in one
 * transaction, a locked day refuses the whole request, and the locked dates are
 * listed here. While the button is disabled the dialog says why.
 */
@Component({
  selector: 'app-planner-absence-dialog',
  imports: [
    FormsModule, TranslatePipe, MatDialogTitle, MatDialogContent, MatDialogActions, MatDatepicker, MatDatepickerInput,
    MatDatepickerToggle, MatDateRangeInput, MatDateRangePicker, MatStartDate, MatEndDate, MatFormField, MatLabel,
    MatHint, MatSuffix, MatIcon, MatInput, MatProgressSpinner, MatSelect, MatSelectTrigger, MatOption,
  ],
  templateUrl: './planner-absence-dialog.component.html',
  styleUrls: ['./planner-absence-dialog.component.scss'],
})
export class PlannerAbsenceDialogComponent {
  private plannerService = inject(TimePlanningPnPlannerService);
  private toastrService = inject(ToastrService);
  private translateService = inject(TranslateService);
  private destroyRef = inject(DestroyRef);
  public dialogRef = inject(MatDialogRef<PlannerAbsenceDialogComponent>);
  public data = inject<PlannerAbsenceDialogData>(MAT_DIALOG_DATA);

  readonly absenceTypes = PLANNER_ABSENCE_TYPES;
  readonly initials = workerInitials;

  assignedSiteId: number | null = this.data.assignedSiteId
    ?? (this.data.workers.length === 1 ? this.data.workers[0].assignedSiteId : null);
  from: Date | null = this.data.from ?? null;
  to: Date | null = this.data.to ?? null;
  messageId: number | null = DEFAULT_ABSENCE_TYPE;
  comment = '';
  busy = false;
  lockedDates: Date[] = [];

  previewState: PlannerAbsencePreviewState = 'idle';
  preview: PlannerAbsencePreviewModel | null = null;
  previewRows: PlannerAbsencePreviewRow[] = [];
  /** The server's refusal of the preview, when it sent one. */
  previewError: string | null = null;

  private previewRequests = new Subject<void>();

  constructor() {
    // The debounce sits inside the switch: every change drops the request in
    // flight at once, so an older period's answer can never land during the
    // pause and show (or enable "Registrér" for) the wrong days.
    this.previewRequests.pipe(
      switchMap(() => timer(PREVIEW_DEBOUNCE_MS).pipe(
        map(() => this.previewRequest()),
        switchMap(request => request
          ? this.plannerService.previewAbsence(request).pipe(
            onNoReply(() => this.previewFailed(null)),
            catchError(() => {
              this.previewFailed(null);
              return EMPTY;
            }),
          )
          : EMPTY),
      )),
      takeUntilDestroyed(this.destroyRef),
    ).subscribe(result => this.applyPreview(result));
    this.refreshPreview();
  }

  // ------------------------------------------------------------------ state

  get selectedWorker(): PlannerWorkerModel | undefined {
    return this.data.workers.find(w => w.assignedSiteId === this.assignedSiteId);
  }

  /** Nobody in the list may have absence registered by this user. */
  get noApprovableWorker(): boolean {
    return this.data.workers.every(w => !w.canApproveAbsence);
  }

  /** The explanation shown when nobody is approvable, or null. */
  get noApprovableMessageKey(): string | null {
    if (!this.noApprovableWorker) {
      return null;
    }
    if (this.data.workers.length === 0) {
      return 'plannerNoWorkers';
    }
    return this.data.isAdmin ? 'plannerAbsenceNoneApprovable' : 'plannerAbsenceNotLeaderOfAnyone';
  }

  get datesValid(): boolean {
    return !!this.from && !!this.to && this.from.getTime() <= this.to.getTime();
  }

  get lockedCount(): number {
    return this.previewRows.filter(r => r.outcome === 'locked').length;
  }

  get overlapCount(): number {
    return this.previewRows.filter(r => r.outcome === 'overlap').length;
  }

  /** Everything that keeps "Registrér" disabled, in display order. */
  get missing(): PlannerAbsenceMissing[] {
    const missing: PlannerAbsenceMissing[] = [];
    const worker = this.selectedWorker;
    if (!worker) {
      missing.push('worker');
    } else if (!worker.canApproveAbsence) {
      missing.push('notLeader');
    }
    if (this.messageId == null) {
      missing.push('type');
    }
    if (!this.datesValid) {
      missing.push('dates');
    } else if (this.previewState === 'ready' && this.preview) {
      if (this.preview.workDays === 0) {
        missing.push('noWorkDays');
      }
      // The server refuses the whole request when one day is locked or already held by another request.
      if (this.lockedCount > 0) {
        missing.push('locked');
      }
      if (this.overlapCount > 0) {
        missing.push('overlap');
      }
    }
    return missing;
  }

  /**
   * A failed or unanswered preview does not block: the server checks again on
   * create and stays the authority. Only a preview in flight waits.
   */
  get canSubmit(): boolean {
    return !this.busy && this.previewState !== 'loading' && this.missing.length === 0;
  }

  /** "Mangler: vælg en medarbejder, …", or null when nothing is missing (or while the preview loads). */
  get missingText(): string | null {
    if (this.busy || this.previewState === 'loading') {
      return null;
    }
    const missing = this.missing;
    if (missing.length === 0) {
      return null;
    }
    const items = missing
      .map(m => this.translateService.instant(MISSING_KEYS[m], this.missingParams(m)))
      .join(', ');
    return this.translateService.instant('plannerMissing', {items});
  }

  private missingParams(missing: PlannerAbsenceMissing): object | undefined {
    if (missing === 'locked') {
      return {count: this.lockedCount};
    }
    return missing === 'overlap' ? {count: this.overlapCount} : undefined;
  }

  /** The chosen type as it reads mid-sentence, for "… bliver til ferie" and "Registrér ferie". */
  get typeLabelInSentence(): string {
    const key = absenceTypeKey(this.messageId);
    return key ? labelInSentence(this.translateService, key) : '';
  }

  get typeLabel(): string {
    const key = absenceTypeKey(this.messageId);
    return key ? String(this.translateService.instant(key)) : '';
  }

  get ctaText(): string {
    return this.messageId == null
      ? this.translateService.instant('plannerRegisterAbsence')
      : this.translateService.instant('plannerRegisterAbsenceType', {type: this.typeLabelInSentence});
  }

  get previewHeadline(): string {
    if (!this.preview) {
      return '';
    }
    return this.translateService.instant('plannerAbsencePreviewHeadline', {
      days: this.preview.workDays,
      hours: this.hours(this.preview.plannedHours),
      type: this.typeLabelInSentence,
    });
  }

  // ---------------------------------------------------------------- changes

  onWorkerChange(assignedSiteId: number | null): void {
    this.assignedSiteId = assignedSiteId;
    this.refreshPreview();
  }

  pickType(id: number): void {
    if (this.messageId === id) {
      return;
    }
    this.messageId = id;
    this.refreshPreview();
  }

  onTypeKeydown(event: KeyboardEvent, index: number): void {
    const next = segmentedKeydown(event, index, this.absenceTypes.length);
    if (next !== null) {
      this.pickType(this.absenceTypes[next].id);
    }
  }

  onDatesChange(): void {
    this.lockedDates = [];
    this.refreshPreview();
  }

  /**
   * Asks for a new preview. The state turns to loading at once, so the button
   * never looks ready for a period whose preview has not arrived; a request
   * that cannot be made (no worker, not a leader, no dates) clears the preview.
   */
  refreshPreview(): void {
    if (this.previewRequest()) {
      this.previewState = 'loading';
    } else {
      this.previewState = 'idle';
      this.preview = null;
      this.previewRows = [];
      this.previewError = null;
    }
    this.previewRequests.next();
  }

  private previewRequest(): PlannerAbsencePreviewRequestModel | null {
    const worker = this.selectedWorker;
    if (!worker || !worker.canApproveAbsence || !this.datesValid || this.messageId == null) {
      return null;
    }
    return {
      assignedSiteId: worker.assignedSiteId,
      from: toTransportDate(this.from),
      to: toTransportDate(this.to),
      messageId: this.messageId,
    };
  }

  private applyPreview(result: OperationDataResult<PlannerAbsencePreviewModel>): void {
    if (!result || !result.success || !result.model) {
      this.previewFailed(result?.message || null);
      return;
    }
    this.preview = result.model;
    this.previewRows = (result.model.days ?? []).map(day => this.toRow(day));
    this.previewError = null;
    this.previewState = 'ready';
  }

  private previewFailed(message: string | null): void {
    this.preview = null;
    this.previewRows = [];
    this.previewError = message;
    this.previewState = 'failed';
  }

  private toRow(day: PlannerAbsencePreviewDayModel): PlannerAbsencePreviewRow {
    return {
      key: dateKey(day.date),
      date: parseCalendarDate(day.date),
      planText: day.planText ?? '',
      planHours: day.planHours ?? 0,
      outcome: KNOWN_OUTCOMES.includes(day.outcome) ? day.outcome as PlannerAbsenceOutcome : 'other',
    };
  }

  // ----------------------------------------------------------------- submit

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
    }).pipe(onNoReply(() => this.outcomeUnknown())).subscribe({
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
   * A write that completed without a reply (the HTTP interceptor's EMPTY): it
   * may or may not have been applied, so close as "changed" and let the grid
   * reload and show the true state.
   */
  private outcomeUnknown(): void {
    this.busy = false;
    this.toastrService.warning(this.translateService.instant('plannerOutcomeUnknown'));
    this.dialogRef.close(true);
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

  // ---------------------------------------------------------------- display

  formatDate(date: Date): string {
    return formatPlannerDate(this.translateService, date);
  }

  /** "ons. 7. okt." */
  formatDay(date: Date): string {
    return formatPlannerDate(this.translateService, date, {weekday: 'short', day: 'numeric', month: 'short'});
  }

  hours(value: number | null | undefined): string {
    return formatPlannerHours(this.translateService, value);
  }

  /** The plan column: the plan text, else "weekend" or "ingen plan". */
  planLabel(row: PlannerAbsencePreviewRow): string {
    if (row.planText) {
      return row.planText;
    }
    return this.translateService.instant(isoDayOfWeek(row.date) >= 5 ? 'plannerAbsencePlanWeekend' : 'plannerAbsencePlanNone');
  }
}

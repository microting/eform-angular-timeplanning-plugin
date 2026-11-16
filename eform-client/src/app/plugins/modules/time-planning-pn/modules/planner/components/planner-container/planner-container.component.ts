import {Component, HostListener, OnInit, inject} from '@angular/core';
import {FormsModule} from '@angular/forms';
import {Overlay} from '@angular/cdk/overlay';
import {MatDialog} from '@angular/material/dialog';
import {MatButtonToggle, MatButtonToggleGroup} from '@angular/material/button-toggle';
import {MatIconButton} from '@angular/material/button';
import {MatFormField, MatLabel} from '@angular/material/form-field';
import {MatIcon} from '@angular/material/icon';
import {MatInput} from '@angular/material/input';
import {MatProgressSpinner} from '@angular/material/progress-spinner';
import {MatRadioButton, MatRadioGroup} from '@angular/material/radio';
import {MatOption, MatSelect} from '@angular/material/select';
import {MatDrawer, MatDrawerContainer, MatDrawerContent} from '@angular/material/sidenav';
import {MatSlideToggle} from '@angular/material/slide-toggle';
import {MatTooltip} from '@angular/material/tooltip';
import {MtxSelect} from '@ng-matero/extensions/select';
import {TranslatePipe, TranslateService} from '@ngx-translate/core';
import {ToastrService} from 'ngx-toastr';
import {filter, Subscription, take} from 'rxjs';
import {dialogConfigHelper} from 'src/app/common/helpers';
import {EformNewSubheaderComponent} from 'src/app/common/modules/eform-shared/components';
import {CommonTagModel} from '../../../../models';
import {TimePlanningPnSettingsService} from '../../../../services';
import {
  PlannerCellModel, PlannerCellsUpdateModel, PlannerDayTotalModel, PlannerEditMode, PlannerEditModes, PlannerGridModel,
  PlannerWeekSumModel, PlannerWorkerModel,
} from '../../models';
import {TimePlanningPnPlannerService} from '../../services';
import {
  addDays, dateKey, formatPlannerDate, formatPlannerHours, isoDayOfWeek, isoWeekNumber, keyToTransportDate,
  localDateKey, mondayOf, onNoReply, parseCalendarDate, ShortFormKind, shortFormKind, todayLocal, toTransportDate,
  WEEKDAY_KEYS,
} from '../../planner.utils';
import {absenceTypeKey, PLANNER_ABSENCE_TYPES} from '../../planner-absence-types';
import {PlannerAbsenceDialogComponent, PlannerAbsenceDialogData} from '../planner-absence-dialog/planner-absence-dialog.component';
import {PlannerImportDialogComponent} from '../planner-import-dialog/planner-import-dialog.component';
import {PlannerPatternDialogComponent, PlannerPatternDialogData} from '../planner-pattern-dialog/planner-pattern-dialog.component';
import {PlannerRequestsPanelComponent} from '../planner-requests-panel/planner-requests-panel.component';

/** A grid column: a shown day, or the sum of the ISO week that ends there. */
export type PlannerColumn =
  | { kind: 'day'; key: string; date: Date; dayIndex: number; total: PlannerDayTotalModel | null }
  | { kind: 'week'; key: string; weekStart: string; isoWeek: number };

export type PlannerDayColumn = Extract<PlannerColumn, { kind: 'day' }>;

/** A grid row: the worker with its cells and week sums looked up by date. */
export interface PlannerRow {
  worker: PlannerWorkerModel;
  cells: Map<string, PlannerCellModel>;
  weeks: Map<string, PlannerWeekSumModel>;
}

interface CellPos {
  row: number;
  day: number;
}

/**
 * Screens 1 and 2 of the web planner, "Planlægning (beta)" (#5489): workers x
 * 7 or 14 days with week sums against the norm and the daily headcount, multi
 * cell selection (click, shift-click, drag) and the short-form editor. The
 * pattern, absence and import dialogs and the vacation-requests panel open
 * from here.
 */
@Component({
  selector: 'app-planner-container',
  imports: [
    FormsModule, TranslatePipe, EformNewSubheaderComponent, MatButtonToggleGroup, MatButtonToggle, MatIconButton,
    MatFormField, MatLabel, MatIcon, MatInput, MatProgressSpinner, MatRadioGroup, MatRadioButton, MatSelect, MatOption,
    MatDrawerContainer, MatDrawer, MatDrawerContent, MatSlideToggle, MatTooltip, MtxSelect,
    PlannerRequestsPanelComponent,
  ],
  templateUrl: './planner-container.component.html',
  styleUrls: ['./planner-container.component.scss'],
})
export class PlannerContainerComponent implements OnInit {
  private plannerService = inject(TimePlanningPnPlannerService);
  private settingsService = inject(TimePlanningPnSettingsService);
  private dialog = inject(MatDialog);
  private overlay = inject(Overlay);
  private toastrService = inject(ToastrService);
  private translateService = inject(TranslateService);

  readonly absenceTypes = PLANNER_ABSENCE_TYPES;
  readonly modes = PlannerEditModes;
  readonly absenceTypeKey = absenceTypeKey;

  from: Date = mondayOf(todayLocal());
  days: 7 | 14 = 14;
  availableTags: CommonTagModel[] = [];
  selectedTagIds: number[] = [];

  loading = false;
  grid: PlannerGridModel | null = null;
  columns: PlannerColumn[] = [];
  rows: PlannerRow[] = [];
  dayColumns: PlannerDayColumn[] = [];

  /** Selected cells as "assignedSiteId|yyyy-MM-dd". */
  selection = new Set<string>();
  private anchor: CellPos | null = null;
  private drag: { start: CellPos; base: Set<string>; moved: boolean } | null = null;

  shortForm = '';
  mode: PlannerEditMode = PlannerEditModes.onlyTheseDays;
  absenceMessageId: number | null = null;
  saving = false;

  drawerOpen = false;
  pendingRequests = 0;
  private load$: Subscription | null = null;
  private pending$: Subscription | null = null;
  highlightSiteId: number | null = null;

  ngOnInit(): void {
    this.settingsService.getAvailableTags().pipe(take(1)).subscribe((result) => {
      if (result && result.success) {
        this.availableTags = result.model || [];
      }
    });
    this.load();
  }

  // ------------------------------------------------------------------ loading

  load(afterLoad?: () => void): void {
    this.loading = true;
    // A newer read replaces an older one still in flight, so a fast next/prev never shows a stale grid.
    this.load$?.unsubscribe();
    this.refreshPendingRequests();
    this.load$ = this.plannerService.getGrid(toTransportDate(this.from), this.days, this.selectedTagIds)
      .pipe(onNoReply(() => { this.loading = false; })).subscribe({
      next: (result) => {
        this.loading = false;
        if (result && result.success && result.model) {
          this.applyGrid(result.model);
          afterLoad?.();
        }
      },
      error: () => {
        this.loading = false;
        this.toastrService.error(this.translateService.instant('plannerLoadFailed'));
      },
    });
  }

  /** The count on the Ferieønsker button, read with the grid so it is right before the panel opens. */
  private refreshPendingRequests(): void {
    this.pending$?.unsubscribe();
    this.pending$ = this.plannerService.getInboxNoToast().subscribe({
      next: (result) => {
        if (result && result.success) {
          this.pendingRequests = (result.model ?? []).filter(r => r.status === 'Pending').length;
        }
      },
      // Silent: the panel reports a failed inbox read when it is opened; the count just stays as it was.
      error: () => undefined,
    });
  }

  private applyGrid(grid: PlannerGridModel): void {
    this.grid = grid;
    const totals = new Map(grid.dayTotals.map(t => [dateKey(t.date), t]));
    const columns: PlannerColumn[] = [];
    grid.dates.forEach((raw, dayIndex) => {
      const key = dateKey(raw);
      const date = parseCalendarDate(raw);
      columns.push({kind: 'day', key, date, dayIndex, total: totals.get(key) ?? null});
      const isLastShownOfWeek = isoDayOfWeek(date) === 6 || dayIndex === grid.dates.length - 1;
      if (isLastShownOfWeek) {
        const weekStart = localDateKey(mondayOf(date));
        columns.push({kind: 'week', key: `week-${weekStart}`, weekStart, isoWeek: isoWeekNumber(date)});
      }
    });
    this.columns = columns;
    this.dayColumns = columns.filter((c): c is PlannerDayColumn => c.kind === 'day');
    this.rows = grid.workers.map(worker => ({
      worker,
      cells: new Map(worker.cells.map(c => [dateKey(c.date), c])),
      weeks: new Map(worker.weeks.map(w => [dateKey(w.weekStart), w])),
    }));
    // Keep only selected cells that are still on screen. The anchor is a row/day
    // index into the previous grid, so it no longer means anything.
    const onScreen = new Set(this.rows.flatMap(r => this.dayColumns.map(c => this.cellKey(r.worker, c.key))));
    this.anchor = null;
    this.drag = null;
    this.setSelection(new Set([...this.selection].filter(k => onScreen.has(k))));
  }

  // ------------------------------------------------------------- navigation

  get periodLabel(): string {
    const to = addDays(this.from, this.days - 1);
    const first = isoWeekNumber(this.from);
    const last = isoWeekNumber(to);
    const weeks = first === last
      ? this.translateService.instant('plannerWeekN', {n: first})
      : this.translateService.instant('plannerWeeksNM', {n: first, m: last});
    return `${weeks} · ${this.formatDate(this.from, false)} – ${this.formatDate(to, true)}`;
  }

  previous(): void {
    this.goTo(addDays(this.from, -this.days));
  }

  next(): void {
    this.goTo(addDays(this.from, this.days));
  }

  goToToday(): void {
    this.goTo(mondayOf(todayLocal()));
  }

  private goTo(from: Date): void {
    this.from = from;
    this.clearSelection();
    this.load();
  }

  onDaysChange(days: 7 | 14): void {
    this.days = days;
    this.clearSelection();
    this.load();
  }

  onTagsChanged(tagIds: number[] | null): void {
    this.selectedTagIds = tagIds || [];
    this.clearSelection();
    this.load();
  }

  // -------------------------------------------------------------- selection

  cellKey(worker: PlannerWorkerModel, key: string): string {
    return `${worker.assignedSiteId}|${key}`;
  }

  isSelected(row: PlannerRow, key: string): boolean {
    return this.selection.has(this.cellKey(row.worker, key));
  }

  private rect(a: CellPos, b: CellPos): string[] {
    const keys: string[] = [];
    for (let r = Math.min(a.row, b.row); r <= Math.max(a.row, b.row); r++) {
      for (let d = Math.min(a.day, b.day); d <= Math.max(a.day, b.day); d++) {
        keys.push(this.cellKey(this.rows[r].worker, this.dayColumns[d].key));
      }
    }
    return keys;
  }

  private toggle(pos: CellPos): void {
    const key = this.cellKey(this.rows[pos.row].worker, this.dayColumns[pos.day].key);
    const next = new Set(this.selection);
    if (next.has(key)) {
      next.delete(key);
    } else {
      next.add(key);
    }
    this.setSelection(next);
    this.anchor = pos;
  }

  private extendTo(pos: CellPos): void {
    if (!this.anchor) {
      this.toggle(pos);
      return;
    }
    this.setSelection(new Set([...this.selection, ...this.rect(this.anchor, pos)]));
  }

  onCellMouseDown(event: MouseEvent, row: number, day: number): void {
    if (event.button !== 0) {
      return;
    }
    event.preventDefault();
    const pos = {row, day};
    if (event.shiftKey) {
      this.extendTo(pos);
      return;
    }
    this.drag = {start: pos, base: new Set(this.selection), moved: false};
  }

  onCellMouseEnter(row: number, day: number): void {
    if (!this.drag) {
      return;
    }
    this.drag.moved = true;
    this.setSelection(new Set([...this.drag.base, ...this.rect(this.drag.start, {row, day})]));
  }

  @HostListener('document:mouseup')
  onMouseUp(): void {
    if (!this.drag) {
      return;
    }
    const {start, moved} = this.drag;
    this.drag = null;
    if (moved) {
      this.anchor = start;
    } else {
      this.toggle(start);
    }
  }

  onCellKeydown(event: KeyboardEvent, row: number, day: number): void {
    if (event.key !== 'Enter' && event.key !== ' ') {
      return;
    }
    event.preventDefault();
    if (event.shiftKey) {
      this.extendTo({row, day});
    } else {
      this.toggle({row, day});
    }
  }

  /**
   * Every change of the selection goes through here: "alle … fra" stays chosen
   * only while every selected worker has a pattern.
   */
  private setSelection(next: Set<string>): void {
    this.selection = next;
    if (this.mode === PlannerEditModes.everyWeekdayFrom && !this.everyWeekdayAllowed) {
      this.mode = PlannerEditModes.onlyTheseDays;
    }
  }

  clearSelection(): void {
    this.selection = new Set();
    this.anchor = null;
    this.shortForm = '';
    this.absenceMessageId = null;
    this.mode = PlannerEditModes.onlyTheseDays;
  }

  private selectedRefs(): { assignedSiteId: number; key: string }[] {
    return [...this.selection].map(k => {
      const [id, key] = k.split('|');
      return {assignedSiteId: Number(id), key};
    }).sort((a, b) => a.key.localeCompare(b.key));
  }

  private selectedSiteIds(): Set<number> {
    return new Set(this.selectedRefs().map(r => r.assignedSiteId));
  }

  get selectedWorkerCount(): number {
    return this.selectedSiteIds().size;
  }

  /** "alle onsdage fra 30. sep": the earliest selected date, and the weekday when all share one. */
  get everyWeekdayLabel(): string {
    const refs = this.selectedRefs();
    if (refs.length === 0) {
      return '';
    }
    const weekdays = new Set(refs.map(r => isoDayOfWeek(parseCalendarDate(r.key))));
    const every = weekdays.size === 1
      ? this.translateService.instant(`plannerEvery${WEEKDAY_KEYS[[...weekdays][0]]}`)
      : this.translateService.instant('plannerEverySelectedWeekday');
    return this.translateService.instant('plannerEveryWeekdayFrom', {
      every,
      date: this.formatDate(parseCalendarDate(refs[0].key), false),
    });
  }

  /** "alle … fra" changes patterns, so every selected worker needs one. */
  get everyWeekdayAllowed(): boolean {
    const ids = this.selectedSiteIds();
    return this.rows.filter(r => ids.has(r.worker.assignedSiteId)).every(r => r.worker.hasPattern);
  }

  get shortFormState(): ShortFormKind {
    return shortFormKind(this.shortForm);
  }

  // ----------------------------------------------------------------- editing

  /**
   * Enter in the short-form box: the same guards as the Udfyld button, so an
   * empty box never clears the selection ("Ryd" is its own button).
   */
  onShortFormEnter(): void {
    if (this.shortFormState !== 'clear') {
      this.applyShortForm(this.shortForm);
    }
  }

  /** "Udfyld" with the typed short form; "Fri" and "Ryd" pass theirs. */
  applyShortForm(text: string): void {
    if (this.saving || shortFormKind(text) === 'invalid') {
      return;
    }
    this.send({shortForm: text.trim(), messageId: null});
  }

  applyAbsence(): void {
    if (this.saving || this.absenceMessageId == null || this.mode !== PlannerEditModes.onlyTheseDays) {
      return;
    }
    this.send({shortForm: null, messageId: this.absenceMessageId});
  }

  private send(edit: Pick<PlannerCellsUpdateModel, 'shortForm' | 'messageId'>): void {
    const refs = this.selectedRefs();
    if (refs.length === 0) {
      return;
    }
    const everyWeekday = this.mode === PlannerEditModes.everyWeekdayFrom;
    const model: PlannerCellsUpdateModel = {
      cells: refs.map(r => ({assignedSiteId: r.assignedSiteId, date: keyToTransportDate(r.key)})),
      shortForm: edit.shortForm,
      messageId: edit.messageId,
      mode: this.mode,
      fromDate: everyWeekday ? keyToTransportDate(refs[0].key) : null,
    };
    this.saving = true;
    // No reply (the HTTP interceptor's EMPTY): the edit may or may not have
    // been applied, so reload and let the grid show the true state.
    this.plannerService.updateCells(model).pipe(onNoReply(() => {
      this.saving = false;
      this.toastrService.warning(this.translateService.instant('plannerOutcomeUnknown'));
      this.clearSelection();
      this.load();
    })).subscribe({
      next: (result) => {
        this.saving = false;
        if (!result || !result.success || !result.model) {
          return;
        }
        const outcome = result.model;
        const names = new Map(this.rows.map(r => [r.worker.assignedSiteId, r.worker.siteName]));
        for (const error of outcome.errors) {
          this.toastrService.error(`${names.get(error.assignedSiteId) ?? ''}: ${error.message || error.key}`);
        }
        if (outcome.lockedMessage) {
          this.toastrService.warning(outcome.lockedMessage);
        }
        if (outcome.updatedCount > 0) {
          this.toastrService.success(this.translateService.instant('plannerDaysUpdated', {count: outcome.updatedCount}));
        }
        this.clearSelection();
        this.load();
      },
      error: () => {
        this.saving = false;
        this.toastrService.error(this.translateService.instant('plannerErrorOccurred'));
      },
    });
  }

  // ----------------------------------------------------------------- dialogs

  openPattern(worker: PlannerWorkerModel): void {
    const data: PlannerPatternDialogData = {
      worker,
      colleagues: this.rows.map(r => r.worker).filter(w => w.hasPattern && w.assignedSiteId !== worker.assignedSiteId),
    };
    this.dialog.open(PlannerPatternDialogComponent, {...dialogConfigHelper(this.overlay, data), maxWidth: '95vw'})
      .afterClosed().pipe(filter(Boolean)).subscribe(() => this.load());
  }

  openAbsence(): void {
    const refs = this.selectedRefs();
    const ids = this.selectedSiteIds();
    const data: PlannerAbsenceDialogData = {
      workers: this.rows.map(r => r.worker),
      assignedSiteId: ids.size === 1 ? [...ids][0] : null,
      from: refs.length ? parseCalendarDate(refs[0].key) : null,
      to: refs.length ? parseCalendarDate(refs[refs.length - 1].key) : null,
    };
    this.dialog.open(PlannerAbsenceDialogComponent, dialogConfigHelper(this.overlay, data))
      .afterClosed().pipe(filter(Boolean)).subscribe(() => {
        this.clearSelection();
        this.load();
      });
  }

  openImport(): void {
    this.dialog.open(PlannerImportDialogComponent, {...dialogConfigHelper(this.overlay), maxWidth: '95vw'})
      .afterClosed().pipe(filter(Boolean)).subscribe(() => this.load());
  }

  /** "Brug Google Sheet igen": the beta's escape hatch; patterns stay. */
  useGoogleSheetAgain(): void {
    this.plannerService.updateSettings({googleSheetRetired: false}).subscribe({
      next: (result) => {
        if (result && result.success) {
          this.load();
        }
      },
      error: () => this.toastrService.error(this.translateService.instant('plannerErrorOccurred')),
    });
  }

  /** "Vis i planen": show the request's range with the worker highlighted. */
  showInPlan(event: { from: Date; to: Date; siteId: number }): void {
    this.from = mondayOf(event.from);
    const span = Math.round((event.to.getTime() - this.from.getTime()) / (24 * 3600 * 1000)) + 1;
    if (span > 7) {
      this.days = 14;
    }
    this.highlightSiteId = event.siteId;
    this.drawerOpen = false;
    this.clearSelection();
    this.load(() => setTimeout(() => {
      const row = this.rows.find(r => r.worker.siteId === event.siteId);
      if (row) {
        document.getElementById(`planner-row-${row.worker.assignedSiteId}`)?.scrollIntoView({block: 'center'});
      }
    }));
  }

  // ----------------------------------------------------------------- display

  formatDate(date: Date, withYear: boolean): string {
    const options: Intl.DateTimeFormatOptions = withYear
      ? {day: 'numeric', month: 'short', year: 'numeric'}
      : {day: 'numeric', month: 'short'};
    return formatPlannerDate(this.translateService, date, options);
  }

  weekdayShort(date: Date): string {
    return formatPlannerDate(this.translateService, date, {weekday: 'short'});
  }

  hours(value: number | null | undefined): string {
    return formatPlannerHours(this.translateService, value);
  }

  isWeekend(date: Date): boolean {
    return isoDayOfWeek(date) >= 5;
  }

  cellLabel(row: PlannerRow, column: PlannerDayColumn, cell: PlannerCellModel | undefined): string {
    const parts = [row.worker.siteName, this.formatDate(column.date, false)];
    const typeKey = absenceTypeKey(cell?.messageId);
    if (typeKey) {
      parts.push(this.translateService.instant(typeKey));
    } else if (cell?.planText) {
      parts.push(cell.planText);
    }
    if (cell?.planChangedByAdmin) {
      parts.push(this.translateService.instant('plannerLegendManual'));
    }
    if (cell?.locked) {
      parts.push(this.translateService.instant('plannerLegendLocked'));
    }
    if (cell?.hasPendingAbsenceRequest) {
      parts.push(this.translateService.instant('plannerLegendPending'));
    }
    return parts.join(', ');
  }

  workerMeta(worker: PlannerWorkerModel): string {
    const parts: string[] = [];
    if (worker.weeklyNormHours != null) {
      parts.push(this.translateService.instant('plannerNormPerWeek', {hours: this.hours(worker.weeklyNormHours)}));
    }
    if (!worker.hasPattern) {
      parts.push(this.translateService.instant('plannerNoPattern'));
    } else if (worker.rotationWeeks && worker.rotationWeeks > 1) {
      parts.push(this.translateService.instant('plannerRotationNWeeks', {n: worker.rotationWeeks}));
    } else {
      parts.push(this.translateService.instant('plannerFormSameWeek'));
    }
    if (worker.useGoogleSheetAsDefault) {
      parts.push(this.translateService.instant('plannerFromGoogleSheet'));
    }
    return parts.join(' · ');
  }
}

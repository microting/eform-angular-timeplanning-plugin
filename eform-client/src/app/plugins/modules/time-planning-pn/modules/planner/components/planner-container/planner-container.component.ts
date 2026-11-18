import {AfterViewInit, Component, DestroyRef, ElementRef, HostListener, NgZone, OnInit, ViewChild, inject} from '@angular/core';
import {takeUntilDestroyed} from '@angular/core/rxjs-interop';
import {FormsModule} from '@angular/forms';
import {Overlay} from '@angular/cdk/overlay';
import {MatDialog} from '@angular/material/dialog';
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
import {Store} from '@ngrx/store';
import {TranslatePipe, TranslateService} from '@ngx-translate/core';
import {ToastrService} from 'ngx-toastr';
import {filter, Subscription, take} from 'rxjs';
import {selectCurrentUserIsAdmin} from 'src/app/state';
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
  addDays, addMonthsToStart, dateKey, daysInMonthOf, formatPlannerDate, formatPlannerHours, isoDayOfWeek, isoWeekNumber,
  keyToTransportDate, localDateKey, mondayOf, monthStartOf, onNoReply, parseCalendarDate, plannerLocale,
  segmentedKeydown, ShortFormKind, shortFormKind, todayLocal, toTransportDate, WEEKDAY_KEYS, workerInitials,
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

/** The period the grid shows: one or two ISO weeks, or a calendar month. */
export type PlannerView = 'week' | 'twoWeeks' | 'month';

/** Where the help panel's dismissal is kept, per viewer (browser). */
export const PLANNER_HELP_DISMISSED_KEY = 'timePlanningPn.planner.helpDismissed';

interface CellPos {
  row: number;
  day: number;
}

/**
 * Screens 1 and 2 of the web planner, "Planlægning (beta)" (#5489): workers x
 * 7 or 14 days or a calendar month, with week sums against the norm and the daily headcount, multi
 * cell selection (click, shift-click, drag) and the short-form editor. The
 * pattern, absence and import dialogs and the vacation-requests panel open
 * from here.
 */
@Component({
  selector: 'app-planner-container',
  imports: [
    FormsModule, TranslatePipe, EformNewSubheaderComponent, MatIconButton,
    MatFormField, MatLabel, MatIcon, MatInput, MatProgressSpinner, MatRadioGroup, MatRadioButton, MatSelect, MatOption,
    MatDrawerContainer, MatDrawer, MatDrawerContent, MatSlideToggle, MatTooltip, MtxSelect,
    PlannerRequestsPanelComponent,
  ],
  templateUrl: './planner-container.component.html',
  styleUrls: ['./planner-container.component.scss'],
})
export class PlannerContainerComponent implements OnInit, AfterViewInit {
  private plannerService = inject(TimePlanningPnPlannerService);
  private settingsService = inject(TimePlanningPnSettingsService);
  private dialog = inject(MatDialog);
  private overlay = inject(Overlay);
  private toastrService = inject(ToastrService);
  private translateService = inject(TranslateService);
  private store = inject(Store);
  private destroyRef = inject(DestroyRef);
  private zone = inject(NgZone);

  readonly absenceTypes = PLANNER_ABSENCE_TYPES;
  readonly modes = PlannerEditModes;
  readonly absenceTypeKey = absenceTypeKey;
  readonly initials = workerInitials;
  readonly views: { value: PlannerView; key: string; testId: string }[] = [
    {value: 'week', key: 'plannerOneWeek', testId: 'planner-days-7'},
    {value: 'twoWeeks', key: 'plannerTwoWeeks', testId: 'planner-days-14'},
    {value: 'month', key: 'plannerMonth', testId: 'planner-days-month'},
  ];

  from: Date = mondayOf(todayLocal());
  view: PlannerView = 'twoWeeks';
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
  highlightSiteId: number | null = null;
  isAdmin = false;
  helpOpen = !readHelpDismissed();

  @ViewChild('gridScroll') private gridScroll?: ElementRef<HTMLElement>;
  /**
   * Where the selection bar sits: it is fixed to the bottom of the viewport
   * (the host scrolls the page, not the planner, so sticky never engages),
   * lined up with the grid's own left edge and width.
   */
  editorFrame: { left: number; width: number } | null = null;

  ngOnInit(): void {
    this.store.select(selectCurrentUserIsAdmin).pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(isAdmin => this.isAdmin = !!isAdmin);
    this.settingsService.getAvailableTags().pipe(take(1)).subscribe((result) => {
      if (result && result.success) {
        this.availableTags = result.model || [];
      }
    });
    this.load();
  }

  ngAfterViewInit(): void {
    const el = this.gridScroll?.nativeElement;
    if (el && typeof ResizeObserver !== 'undefined') {
      // The host has no zone patch for ResizeObserver: re-enter the zone so the
      // new frame is rendered (sidebar toggle, a scrollbar appearing, a window resize).
      const observer = new ResizeObserver(() => this.zone.run(() => this.measureEditorFrame()));
      observer.observe(el);
      this.destroyRef.onDestroy(() => observer.disconnect());
    }
  }

  measureEditorFrame(): void {
    const rect = this.gridScroll?.nativeElement.getBoundingClientRect();
    this.editorFrame = rect && rect.width > 0 ? {left: rect.left, width: rect.width} : null;
  }

  // ------------------------------------------------------------------ loading

  load(afterLoad?: () => void): void {
    this.loading = true;
    // A newer read replaces an older one still in flight, so a fast next/prev never shows a stale grid.
    this.load$?.unsubscribe();
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

  private applyGrid(grid: PlannerGridModel): void {
    this.grid = grid;
    // The count on the Ferieønsker button comes only from the grid; a decision in the panel reloads the grid.
    this.pendingRequests = grid.pendingRequestCount ?? 0;
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

  /** Days the grid reads: 7, 14, or the days of the shown calendar month. */
  get days(): number {
    switch (this.view) {
      case 'week':
        return 7;
      case 'month':
        return daysInMonthOf(this.from);
      default:
        return 14;
    }
  }

  get periodEnd(): Date {
    return addDays(this.from, this.days - 1);
  }

  /** "Uge 47–48", or "November 2026" for a month. */
  get periodTitle(): string {
    if (this.view === 'month') {
      const month = formatPlannerDate(this.translateService, this.from, {month: 'long', year: 'numeric'});
      return month.charAt(0).toLocaleUpperCase(plannerLocale(this.translateService)) + month.slice(1);
    }
    const first = isoWeekNumber(this.from);
    const last = isoWeekNumber(this.periodEnd);
    return first === last
      ? this.translateService.instant('plannerWeekN', {n: first})
      : this.translateService.instant('plannerWeeksNM', {n: first, m: last});
  }

  /** "16. nov. – 29. nov. 2026". */
  get periodRange(): string {
    return `${this.formatDate(this.from, false)} – ${this.formatDate(this.periodEnd, true)}`;
  }

  /** The grid's start for `date` in the current view: its Monday, or the 1st of its month. */
  private periodStartOf(date: Date): Date {
    return this.view === 'month' ? monthStartOf(date) : mondayOf(date);
  }

  previous(): void {
    this.goTo(this.view === 'month' ? addMonthsToStart(this.from, -1) : addDays(this.from, -this.days));
  }

  next(): void {
    this.goTo(this.view === 'month' ? addMonthsToStart(this.from, 1) : addDays(this.from, this.days));
  }

  goToToday(): void {
    this.goTo(this.periodStartOf(todayLocal()));
  }

  private goTo(from: Date): void {
    this.from = from;
    this.clearSelection();
    this.load();
  }

  /** Switching view keeps the period in view: a month starts on the 1st, weeks on a Monday. */
  onViewChange(view: PlannerView): void {
    if (view === this.view) {
      return;
    }
    // Leaving a month that holds today lands on today's week, not the month's first.
    const today = todayLocal();
    const anchor = this.view === 'month' && today >= this.from && today <= this.periodEnd ? today : this.from;
    this.view = view;
    this.from = this.periodStartOf(anchor);
    this.clearSelection();
    this.load();
  }

  onViewKeydown(event: KeyboardEvent, index: number): void {
    const next = segmentedKeydown(event, index, this.views.length);
    if (next !== null) {
      this.onViewChange(this.views[next].value);
    }
  }

  // ------------------------------------------------------------------- help

  closeHelp(): void {
    this.helpOpen = false;
    writeHelpDismissed(true);
  }

  /** "Hjælp" opens the panel again, and toggles it while open. */
  toggleHelp(): void {
    if (this.helpOpen) {
      this.closeHelp();
      return;
    }
    this.helpOpen = true;
    writeHelpDismissed(false);
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
    if (this.selection.size === 0 && next.size > 0) {
      this.measureEditorFrame();
    }
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

  /** A worker's pattern; with no worker ("Ugemønstre" in the header) the dialog opens on a picker. */
  openPattern(worker: PlannerWorkerModel | null): void {
    const workers = this.rows.map(r => r.worker);
    const data: PlannerPatternDialogData = worker
      ? {worker, colleagues: workers.filter(w => w.hasPattern && w.assignedSiteId !== worker.assignedSiteId)}
      : {worker: workers.length === 1 ? workers[0] : null, workers};
    this.dialog.open(PlannerPatternDialogComponent, {...dialogConfigHelper(this.overlay, data), maxWidth: '95vw'})
      .afterClosed().pipe(filter(Boolean)).subscribe(() => this.load());
  }

  /** "Ugemønstre": one selected worker opens straight on that worker's pattern. */
  openPatterns(): void {
    const ids = this.selectedSiteIds();
    const selected = ids.size === 1 ? this.rows.find(r => r.worker.assignedSiteId === [...ids][0])?.worker : null;
    this.openPattern(selected ?? null);
  }

  /**
   * "Registrér fravær" and the selection bar's "Ferie": prefilled from the
   * selection (the worker when only one is selected, the first to the last
   * selected date).
   */
  openAbsence(): void {
    const data: PlannerAbsenceDialogData = {...this.absencePrefill(), workers: this.rows.map(r => r.worker), isAdmin: this.isAdmin};
    this.dialog.open(PlannerAbsenceDialogComponent, {
      ...dialogConfigHelper(this.overlay, data), width: '720px', maxWidth: '95vw',
    })
      .afterClosed().pipe(filter(Boolean)).subscribe(() => {
        this.clearSelection();
        this.load();
      });
  }

  private absencePrefill(): Pick<PlannerAbsenceDialogData, 'assignedSiteId' | 'from' | 'to'> {
    const refs = this.selectedRefs();
    const ids = this.selectedSiteIds();
    return {
      assignedSiteId: ids.size === 1 ? [...ids][0] : null,
      from: refs.length ? parseCalendarDate(refs[0].key) : null,
      to: refs.length ? parseCalendarDate(refs[refs.length - 1].key) : null,
    };
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
    this.from = this.periodStartOf(event.from);
    const span = Math.round((event.to.getTime() - this.from.getTime()) / (24 * 3600 * 1000)) + 1;
    if (this.view === 'week' && span > 7) {
      this.view = 'twoWeeks';
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

  /** "28/9", the design's column date. */
  shortDate(date: Date): string {
    return `${date.getDate()}/${date.getMonth() + 1}`;
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

function readHelpDismissed(): boolean {
  try {
    return localStorage.getItem(PLANNER_HELP_DISMISSED_KEY) === 'true';
  } catch {
    return false;
  }
}

function writeHelpDismissed(dismissed: boolean): void {
  try {
    if (dismissed) {
      localStorage.setItem(PLANNER_HELP_DISMISSED_KEY, 'true');
    } else {
      localStorage.removeItem(PLANNER_HELP_DISMISSED_KEY);
    }
  } catch {
    // No storage (private window, blocked site data): the panel just shows again next time.
  }
}

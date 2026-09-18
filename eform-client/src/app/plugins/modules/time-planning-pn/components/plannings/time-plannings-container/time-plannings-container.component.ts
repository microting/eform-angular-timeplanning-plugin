import { Component, OnDestroy, OnInit,
  inject
} from '@angular/core';
import { AutoUnsubscribe } from 'ngx-auto-unsubscribe';
import {Subscription, take, forkJoin} from 'rxjs';
import { SiteDto } from 'src/app/common/models';
import {
  AssignedSiteModel, CommonTagModel, ReconcileThroughResultModel, TimePlanningModel, TimePlanningsRequestModel,
} from '../../../models';
import {
  TimePlanningPnPlanningsService,
  TimePlanningPnSettingsService,
} from '../../../services';
import {startOfWeek, endOfWeek, format, startOfDay, subDays} from 'date-fns';
import {ToastrService} from 'ngx-toastr';
import {TranslateService} from '@ngx-translate/core';
import {
  assertLockOutcomeHandled, buildReconcilePreview, ReconcilePreview, sendLockRequest,
} from '../day-lock.util';
import {ExcelIcon, iOSIcon, PARSING_DATE_FORMAT} from 'src/app/common/const';
import {Store} from '@ngrx/store';
import {selectCurrentUserLocale, selectCurrentUserIsAdmin, selectCurrentUserIsFirstUser} from 'src/app/state';
import {MatDialog} from '@angular/material/dialog';
import {
  DownloadExcelDialogComponent, DownloadExcelDialogData, PayrollExportDialogComponent
} from 'src/app/plugins/modules/time-planning-pn/components';
import {MatDatepickerInputEvent} from '@angular/material/datepicker';
import {HelpAudience, HelpEntryId, HelpTourName, HelpUiStrings} from '../../../help/help.model';
import {HelpContentService} from '../../../help/services/help-content.service';
import {HelpPanelService} from '../../../help/services/help-panel.service';
import {HelpTourService} from '../../../help/services/help-tour.service';
import {HelpVisibilityService} from '../../../help/services/help-visibility.service';

@AutoUnsubscribe()
@Component({
  selector: 'app-time-plannings-container',
  templateUrl: './time-plannings-container.component.html',
  styleUrls: ['./time-plannings-container.component.scss'],
  standalone: false
})
export class TimePlanningsContainerComponent implements OnInit, OnDestroy {
  private store = inject(Store);
  private planningsService = inject(TimePlanningPnPlanningsService);
  private settingsService = inject(TimePlanningPnSettingsService);
  private dialog = inject(MatDialog);
  private helpContent = inject(HelpContentService);
  private helpPanel = inject(HelpPanelService);
  private helpTour = inject(HelpTourService);
  private toastrService = inject(ToastrService);
  private translateService = inject(TranslateService);
  /** Protected, not private: the ? button binds isVisible$ straight from the template. */
  protected helpVisibility = inject(HelpVisibilityService);

  timePlanningsRequest: TimePlanningsRequestModel;
  availableSites: SiteDto[] = [];
  availableTags: CommonTagModel[] = [];
  selectedTagIds: number[] = [];
  showResignedSites: boolean = false;
  /**
   * Gates the payroll export button and the help panel, and nothing to do with the day
   * lock: that is isFirstUser below. The two live side by side and are not each other's
   * duplicate — collapsing them would quietly change who gets which.
   */
  isAdmin: boolean = false;
  /**
   * Whether this user may reconcile at all. Only the first user may, and the server
   * refuses everyone else.
   *
   * Subscribed live rather than with take(1): the flag is not fixed for the life of the
   * page — signing out resets it — and the subscription is also what takes a drawn
   * preview down when it changes. See ngOnInit.
   */
  isFirstUser: boolean = false;
  payrollSystem: number = 0;
  payrollCutoffDay: number = 19;
  timePlannings: TimePlanningModel[] = [];
  selectedDate: Date = new Date();
  dateFrom: Date;
  dateTo: Date;
  siteId: number = null; // Default to 0 to get all sites

  /** Ticked rows (site ids). Empty means every worker currently visible. */
  selectedSiteIds: number[] = [];
  /** The bulk target, always a day on screen. See refreshReconcileBounds. */
  reconcileThroughDate: Date | null = null;
  reconcilePreview: ReconcilePreview | null = null;
  reconcileInFlight = false;
  reconcileMinDate: Date | null = null;
  reconcileMaxDate: Date | null = null;

  getTimePlannings$: Subscription;
  updateTimePlanning$: Subscription;
  getAvailableSites$: Subscription;
  reconcileThrough$: Subscription;
  isFirstUser$: Subscription;
  public selectCurrentUserLocale$ = this.store.select(selectCurrentUserLocale);
  locale: string;

  /**
   * The page tour is offered once per session at most. hasSeen() alone is not
   * enough: it only flips when the tour ends, and getPlannings() reruns on every
   * filter change, so an unfinished tour would otherwise restart from step 1 each
   * time the grid reloads.
   */
  private pageTourOffered = false;

  ngOnInit(): void {
    // Load available tags
    this.settingsService
      .getAvailableTags()
      .pipe(take(1))
      .subscribe((data) => {
        if (data && data.success) {
          // A success with no body still means "no tags", and everything downstream —
          // the filter, the export dialog — is written against a list.
          this.availableTags = data.model || [];
        }
      });

    if (!this.showResignedSites) {
      this.settingsService
        .getAvailableSites()
        .subscribe((data) => {
          if (data && data.success) {
            this.availableSites = data.model;
            if (this.availableSites.length === 1) {
              this.siteId = this.availableSites[0].siteId;
            }
          }
        });
    } else {
      this.getAvailableSites$ = this.settingsService
        .getResignedSites()
        .subscribe((data) => {
          if (data && data.success) {
            this.availableSites = data.model;
            if (this.availableSites.length === 1) {
              this.siteId = this.availableSites[0].siteId;
            }
          }
        });
    }
    this.selectCurrentUserLocale$.pipe(take(1)).subscribe((locale) => {
      this.locale = locale;
      this.getPlannings();
    });

    // The day lock's own flag. Live, never take(1) — the flag can change while this
    // page is alive, and the preview drawn over the grid has to follow it.
    this.isFirstUser$ = this.store.select(selectCurrentUserIsFirstUser)
      .subscribe((isFirstUser) => {
        this.isFirstUser = !!isFirstUser;
        // A flag that turns false while a preview is drawn would otherwise leave the
        // scope bar on screen with a Confirm button that silently does nothing — a
        // control disabled without explanation, by accident. rebuildReconcilePreview
        // cancels for anyone but the first user, and does nothing on a late true,
        // when there is no target date yet.
        this.rebuildReconcilePreview();
      });

    // Load payroll settings only for admins — export is admin-gated
    this.store.select(selectCurrentUserIsAdmin).pipe(take(1)).subscribe((admin) => {
      this.isAdmin = admin;
      if (admin) {
        this.settingsService.getPayrollSettings().pipe(take(1)).subscribe((data) => {
          if (data && data.success && data.model) {
            this.payrollSystem = data.model.payrollSystem || 0;
            this.payrollCutoffDay = data.model.cutoffDay || 19;
          }
        });
      }
    });
  }

  private buildTimePlanningsRequest() {
    const now = new Date();
    now.setHours(0, 0, 0, 0);
    if (!this.dateFrom) {
      this.dateFrom = startOfWeek(now, { weekStartsOn: 1 });
      this.dateTo = endOfWeek(now, { weekStartsOn: 1 });
    }
    this.timePlanningsRequest = {
      dateFrom: format(this.dateFrom, PARSING_DATE_FORMAT),
      dateTo: format(this.dateTo, PARSING_DATE_FORMAT),
      sort: 'Date',
      isSortDsc: true,
      siteId: this.siteId,
      showResignedSites: this.showResignedSites,
      tagIds: this.selectedTagIds.length > 0 ? this.selectedTagIds : undefined
    };
  }

  getPlannings() {
    this.buildTimePlanningsRequest();
    this.getTimePlannings$ = this.planningsService
      .getPlannings(this.timePlanningsRequest)
      .subscribe((data) => {
        if (data && data.success) {
          this.applyPlannings(data.model);
        }
        this.startPageTourOnce();
      });
    }

  /**
   * Drops the bulk scope without rebuilding anything.
   *
   * Anything that makes the grid throw the ticked rows away has to clear the scope BEFORE
   * change detection reaches the scope bar. The grid reports its own reset synchronously,
   * from inside the very pass that renders the bar, so a scope still standing at that
   * moment would flip the bar from shown to gone after it had already been checked — an
   * NG0100 in a dev build. Clearing first is what lets onSelectionReset return early
   * instead.
   */
  private clearBulkScope(): void {
    this.selectedSiteIds = [];
    this.cancelReconcilePreview();
  }

  /**
   * Every path that replaces the rows with a fresh load comes through here: a day save,
   * the assigned-site dialog, Reload, a filter, the resigned toggle. The grid drops the
   * ticked rows on new data and says nothing.
   * - If rows were ticked, the preview is CANCELLED, never rebuilt. A rebuild would fall
   *   back to "every visible worker" and silently widen the scope from the ticked rows to
   *   all of them, and a mistaken commit then costs one typed-word unlock per worker.
   * - If nothing was ticked, the scope was already "everyone visible", so the preview is
   *   redrawn on the new rows.
   * Both happen here, before change detection, for the reason clearBulkScope gives.
   *
   * The date-range handlers cannot wait for this: they change the day columns in the same
   * pass and their load answers later, so they call clearBulkScope themselves first.
   */
  private applyPlannings(model: TimePlanningModel[]): void {
    const hadSelection = this.selectedSiteIds.length > 0;
    this.timePlannings = model;
    this.selectedSiteIds = [];
    this.refreshReconcileBounds();
    if (hadSelection) {
      this.cancelReconcilePreview();
    } else {
      this.rebuildReconcilePreview();
    }
  }

  /**
   * The bulk target must be on screen, because the preview can only draw what is on
   * screen. Nothing at or after today may be reconciled, so it is capped at yesterday.
   * A null max means nothing in view can be reconciled at all.
   */
  private refreshReconcileBounds(): void {
    const from = startOfDay(this.dateFrom);
    const yesterday = startOfDay(subDays(new Date(), 1));
    const lastVisible = startOfDay(this.dateTo);
    const max = lastVisible < yesterday ? lastVisible : yesterday;
    if (max < from) {
      // Nothing in view can be reconciled, so there is no range to offer — not a range
      // whose start is still a real day. Both ends go, or the field would advertise a
      // minimum it will never accept.
      this.reconcileMinDate = null;
      this.reconcileMaxDate = null;
      return;
    }
    this.reconcileMinDate = from;
    this.reconcileMaxDate = max;
  }

  get reconcileTargetLabel(): string {
    return this.reconcileThroughDate ? format(this.reconcileThroughDate, 'dd.MM.yyyy') : '';
  }

  /**
   * The user ticked or unticked a row. Unticking the last one returns the scope to
   * "every visible worker", as the spec defines, and the scope bar's count changes in
   * plain sight.
   */
  onSelectionChanged(siteIds: number[]): void {
    this.selectedSiteIds = siteIds;
    this.rebuildReconcilePreview();
  }

  /** The grid dropped the ticked rows by itself (new columns). Cancel rather than widen. */
  onSelectionReset(): void {
    if (this.selectedSiteIds.length === 0) {
      // The scope is already gone: cleared by applyPlannings on a reload, or eagerly by
      // the date-range handlers. Touching it again here would be the NG0100.
      return;
    }
    this.clearBulkScope();
  }

  /** From the toolbar field or a day-column header. Starts the preview and commits nothing. */
  onReconcileDateChanged(date: Date | null): void {
    this.reconcileThroughDate = date ? startOfDay(date) : null;
    this.rebuildReconcilePreview();
  }

  cancelReconcilePreview(): void {
    this.reconcileThroughDate = null;
    this.reconcilePreview = null;
  }

  private rebuildReconcilePreview(): void {
    if (!this.isFirstUser) {
      // Only the first user may reconcile, so only the first user gets the preview
      // that leads to it. Guarded here rather than at each caller: this is the one
      // place a preview is ever built, and the toolbar field the flag also hides is
      // not the only way in — a day-column header asks for one too.
      this.cancelReconcilePreview();
      return;
    }
    const target = this.reconcileThroughDate;
    const onScreen = !!target && !!this.reconcileMinDate && !!this.reconcileMaxDate
      && target >= this.reconcileMinDate && target <= this.reconcileMaxDate;
    if (!onScreen) {
      // Also covers navigating away from the target's week: the preview leaves with it.
      this.cancelReconcilePreview();
      return;
    }
    // No selection means every worker currently visible under the active filters.
    const scope = this.selectedSiteIds.length
      ? this.selectedSiteIds
      : this.timePlannings.map(x => x.siteId);
    this.reconcilePreview = buildReconcilePreview(this.timePlannings, scope, format(target, 'yyyy-MM-dd'));
  }

  /**
   * Commits the previewed region. The request spans many rows, so it is the likeliest of
   * all the lock requests to outlive a client timeout with a commit already in progress —
   * which is why the outcome comes from the shared helper rather than a plain
   * {next, error} subscribe: an unanswered bulk request reported as "nothing happened"
   * would have the user reconcile the same region twice.
   */
  confirmReconcileThrough(): void {
    const preview = this.reconcilePreview;
    // The first-user rule again at the commit, not only where the preview is built:
    // this is where the request is actually made, and the server refuses anyone else.
    if (!this.isFirstUser || !preview || this.reconcileInFlight || preview.willReconcileCount === 0) {
      return;
    }
    this.reconcileInFlight = true;
    // Exactly the rows the preview drew. Skipped rows go too, so that the toast reports
    // what the server decided rather than what the client predicted.
    this.reconcileThrough$ = sendLockRequest(
      this.planningsService.reconcileThrough(preview.target, preview.siteIds),
      outcome => {
        this.reconcileInFlight = false;
        switch (outcome.kind) {
          case 'success':
            if (!this.reportReconcileThrough(outcome.result.model)) {
              // A success with no body still sealed the rows; we just cannot say how
              // many. Saying the state is uncertain is true and the reload settles it,
              // where saying nothing would leave a commit with no acknowledgement at all.
              this.toastrService.error(this.translateService.instant('lockRequestUncertain'));
            }
            this.refreshAfterReconcileThrough();
            return;
          case 'refused':
            // KNOWN: the server answered "no" and nothing changed, so the preview stays
            // up to be retried. ApiBaseService toasts body.message when there is one.
            if (!outcome.message) {
              this.toastrService.error(this.translateService.instant('lockRequestFailed'));
            }
            return;
          case 'error':
            // KNOWN: the interceptor's 400 branch, rejected before anything happened and
            // already toasted. The preview stays up.
            return;
          case 'unknown':
            // Our own timeout, or the interceptor giving up after retrying. Part of the
            // region may well be sealed, so the grid on screen is no longer trustworthy:
            // drop the preview drawn over it and reload.
            this.toastrService.error(this.translateService.instant('lockRequestUncertain'));
            this.refreshAfterReconcileThrough();
            return;
        }
        return assertLockOutcomeHandled(outcome);
      });
  }

  /**
   * The grid memoises a cell's background on the row reference, so only a whole new set
   * of rows redraws the region that was just sealed.
   */
  private refreshAfterReconcileThrough(): void {
    this.cancelReconcilePreview();
    this.getPlannings();
  }

  /**
   * What the server decided, worker by worker. Returns false when there was no body to
   * report, so the caller can say something rather than nothing.
   *
   * The two skip reasons are reported SEPARATELY, and the server keeps them apart for the
   * same reason. One merged "skipped" figure reads as "already sealed further ahead" —
   * that is the benign one — while it may equally mean "these workers have no
   * registration, so nothing was sealed for them at all". Those call for opposite
   * responses from a planner, and the bulk action exists precisely to surface the second.
   */
  private reportReconcileThrough(model: ReconcileThroughResultModel | null): boolean {
    if (!model) {
      return false;
    }
    const message = this.buildReconcileThroughMessage(model);
    if (model.applied > 0) {
      this.toastrService.success(message);
    } else {
      this.toastrService.warning(message);
    }
    return true;
  }

  /** The segments the result is read as, in the order a planner needs them. */
  private buildReconcileThroughMessage(model: ReconcileThroughResultModel): string {
    const segments = [this.translateService.instant('reconcileThroughResult',
      {applied: model.applied, skipped: model.skippedAlreadyFurtherForward.length})];
    if (model.skippedNoRegistration.length) {
      segments.push(this.translateService.instant('reconcileThroughNoRegistration',
        {count: model.skippedNoRegistration.length}));
    }
    if (model.alreadyReconciledSiteIds.length) {
      segments.push(this.translateService.instant('reconcileThroughUnchanged',
        {count: model.alreadyReconciledSiteIds.length}));
    }
    return segments.join(' · ');
  }

  /**
   * Who is looking, for everything that filters the help registry. Both axes, so an
   * entry gated on either is judged by the right one.
   */
  get helpAudience(): HelpAudience {
    return { isAdmin: this.isAdmin, isFirstUser: this.isFirstUser };
  }

  /** Help chrome labels. Never the shared ngx-translate catalogue. */
  get helpUi(): HelpUiStrings {
    return this.helpContent.ui();
  }

  openHelp(target?: HelpEntryId): void {
    this.helpPanel.open(target);
  }

  /**
   * Replays whichever tour the panel says applies to the surface it was opened
   * from. Opened from the toolbar that is the page tour; opened from inside the
   * day-cell dialog it is the dialog tour, whose anchors are the only ones in
   * front of the dialog backdrop. This is also the only way the dialog tour can
   * be seen a second time: the dialog itself offers it once, gated on hasSeen.
   */
  replayTour(tour: HelpTourName): void {
    // The panel has already closed itself; let that settle before querying anchors.
    setTimeout(() => this.helpTour.start(tour, this.helpAudience));
  }

  private startPageTourOnce(): void {
    // Steps 4-6 point at grid rows. HelpTourService records a tour as seen the
    // moment it runs out of steps, and that flag lives in localStorage, so
    // offering the tour on an empty grid would drop those three steps and then
    // permanently suppress them. Wait for rows.
    // The gate is checked here rather than after the fact, because
    // pageTourOffered is a once-per-page-visit latch: burning it on a start the
    // gate refuses would mean the tour never comes up again for this container
    // instance, even if help becomes visible a moment later.
    if (this.pageTourOffered
      || this.timePlannings.length === 0
      || !this.helpVisibility.isVisible
      || this.helpTour.hasSeen('page')) {
      return;
    }
    this.pageTourOffered = true;
    // Let the current change-detection pass render the grid, or start() finds no
    // anchors and drops every step it was meant to point at.
    setTimeout(() => this.helpTour.start('page', this.helpAudience));
  }

  ngOnDestroy(): void {
  }

  goBackward() {
    // The columns change in this same pass; the load answers later. See clearBulkScope.
    this.clearBulkScope();
    const tempEndDate = new Date(this.dateTo);
    tempEndDate.setHours(0, 0, 0, 0);

    let daysCount = (Math.floor((tempEndDate.getTime() - this.dateFrom.getTime()) / (1000 * 3600 * 24)) * -1) -1;
    this.dateFrom = this.addDays(this.dateFrom, daysCount);
    this.dateTo = this.addDays(this.dateTo, daysCount);
    this.getPlannings();
  }

  openDownloadExcelDialog() {
    // The export opens on what the page is showing. The dialog copies these and
    // never writes back, so narrowing an export leaves the page as it was.
    const data: DownloadExcelDialogData = {
      availableSites: this.availableSites,
      availableTags: this.availableTags,
      dateFrom: this.dateFrom,
      dateTo: this.dateTo,
      selectedTagIds: this.selectedTagIds,
      siteId: this.siteId,
    };
    this.dialog.open(DownloadExcelDialogComponent, {
      width: '600px',
      data,
    });
  }

  openPayrollExportDialog() {
    const dialogRef = this.dialog.open(PayrollExportDialogComponent, {
      width: '600px',
      data: {
        cutoffDay: this.payrollCutoffDay,
        payrollSystem: this.payrollSystem,
      },
    });
    dialogRef.afterClosed().subscribe((result) => {
      if (result) {
        this.getPlannings();
      }
    });
  }

  goForward() {
    // The columns change in this same pass; the load answers later. See clearBulkScope.
    this.clearBulkScope();
    const tempEndDate = new Date(this.dateTo);
    tempEndDate.setHours(0, 0, 0, 0);
    let daysCount = Math.floor((tempEndDate.getTime() - this.dateFrom.getTime()) / (1000 * 3600 * 24)) +1;
    this.dateFrom = this.addDays(this.dateFrom, daysCount);
    this.dateTo = this.addDays(this.dateTo, daysCount);
    this.getPlannings();
  }

  private addDays(date: Date, days: number): Date {
    const result = new Date(date);
    result.setDate(result.getDate() + days);
    return result;
  }

  formatDateRange(): string {
    const options = { year: 'numeric', month: 'numeric', day: 'numeric' } as const;
    //const from = this.dateFrom.toLocaleDateString(undefined, options);
    const from = format(this.dateFrom, 'dd.MM.yyyy');
    //const to = this.dateTo.toLocaleDateString(undefined, options);
    const to = format(this.dateTo, 'dd.MM.yyyy');
    return `${from} - ${to}`;
  }

  onTimePlanningChanged($event: any) {
    this.getPlannings();
  }

  onAssignedSiteChanged($event: any) {
    this.getPlannings();
  }

  onSiteChanged($event: any) {
    this.siteId = $event;
    this.getPlannings();
  }

  updateDateFrom(dateFrom: MatDatepickerInputEvent<any, any>) {
    this.dateFrom = dateFrom.value;
  }

  updateDateTo(dateTo: MatDatepickerInputEvent<any, any>) {
    if (dateTo.value) {
      // The columns change in this same pass; the load answers later. See clearBulkScope.
      this.clearBulkScope();
      this.dateTo = dateTo.value;
      this.dateTo.setHours(23, 59, 59, 999);
      this.getPlannings();
    }
  }

  onShowResignedSitesChanged($event: any) {
    this.showResignedSites = $event.checked;
    this.buildTimePlanningsRequest();
    const sitesCall$ = this.showResignedSites
      ? this.settingsService.getResignedSites()
      : this.settingsService.getAvailableSites();
    this.getTimePlannings$ = forkJoin([
      sitesCall$,
      this.planningsService.getPlannings(this.timePlanningsRequest),
    ]).subscribe(([sitesResult, planningsResult]) => {
      if (sitesResult && sitesResult.success) {
        this.availableSites = sitesResult.model;
      }
      if (planningsResult && planningsResult.success) {
        this.applyPlannings(planningsResult.model);
      }
    });
  }

  onTagsChanged($event: number[]) {
    // The select emits null when the last tag is cleared, and "no tags" is an empty
    // list everywhere else: the request builder, the row chips, the export dialog.
    this.selectedTagIds = $event || [];
    this.getPlannings();
  }

  /**
   * A tag chip was clicked in a table row: add the tag to the Etiketter
   * filter (new array reference so the mtx-select model reflects it) and
   * reload. Clicking a tag that is already selected is a no-op.
   */
  onTagSelectedFromRow(tagId: number) {
    if (this.selectedTagIds.includes(tagId)) {
      return;
    }
    this.selectedTagIds = [...this.selectedTagIds, tagId];
    this.getPlannings();
  }

  /**
   * Called by the table component when it has finished rendering
   * the highlighted row/cell in the DOM. This guarantees both the service
   * call and the mtx-grid rendering are complete before we consider the
   * highlight cycle done.
   */
  onHighlightedRowRendered(): void {
    // Highlight cleanup is handled inside the table component's scrollAndHighlightCell.
    // This handler exists for consistency with the report-container pattern and can be
    // extended if the container needs to react to the completed highlight.
  }
}

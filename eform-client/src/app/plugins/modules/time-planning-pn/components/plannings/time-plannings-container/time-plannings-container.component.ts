import { Component, OnDestroy, OnInit,
  inject
} from '@angular/core';
import { AutoUnsubscribe } from 'ngx-auto-unsubscribe';
import {Subscription, take, forkJoin} from 'rxjs';
import { SiteDto } from 'src/app/common/models';
import {AssignedSiteModel, CommonTagModel, TimePlanningModel, TimePlanningsRequestModel} from '../../../models';
import {
  TimePlanningPnPlanningsService,
  TimePlanningPnSettingsService,
} from '../../../services';
import {startOfWeek, endOfWeek, format} from 'date-fns';
import {ExcelIcon, iOSIcon, PARSING_DATE_FORMAT} from 'src/app/common/const';
import {Store} from '@ngrx/store';
import {selectCurrentUserLocale, selectCurrentUserIsAdmin} from 'src/app/state';
import {MatDialog} from '@angular/material/dialog';
import {DownloadExcelDialogComponent, PayrollExportDialogComponent} from 'src/app/plugins/modules/time-planning-pn/components';
import {MatDatepickerInputEvent} from '@angular/material/datepicker';
import {HelpEntryId, HelpUiStrings} from '../../../help/help.model';
import {HelpContentService} from '../../../help/services/help-content.service';
import {HelpPanelService} from '../../../help/services/help-panel.service';
import {HelpTourService} from '../../../help/services/help-tour.service';

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

  timePlanningsRequest: TimePlanningsRequestModel;
  availableSites: SiteDto[] = [];
  availableTags: CommonTagModel[] = [];
  selectedTagIds: number[] = [];
  showResignedSites: boolean = false;
  isAdmin: boolean = false;
  payrollSystem: number = 0;
  payrollCutoffDay: number = 19;
  timePlannings: TimePlanningModel[] = [];
  selectedDate: Date = new Date();
  dateFrom: Date;
  dateTo: Date;
  siteId: number = null; // Default to 0 to get all sites

  getTimePlannings$: Subscription;
  updateTimePlanning$: Subscription;
  getAvailableSites$: Subscription;
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
          this.availableTags = data.model;
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
          this.timePlannings = data.model;
        }
        this.startPageTourOnce();
      });
    }

  /** Help chrome labels. Never the shared ngx-translate catalogue. */
  get helpUi(): HelpUiStrings {
    return this.helpContent.ui();
  }

  openHelp(target?: HelpEntryId): void {
    this.helpPanel.open(target);
  }

  replayPageTour(): void {
    // The panel has already closed itself; let that settle before querying anchors.
    setTimeout(() => this.helpTour.start('page', { isAdmin: this.isAdmin }));
  }

  private startPageTourOnce(): void {
    // Steps 4-6 point at grid rows. HelpTourService records a tour as seen the
    // moment it runs out of steps, and that flag lives in localStorage, so
    // offering the tour on an empty grid would drop those three steps and then
    // permanently suppress them. Wait for rows.
    if (this.pageTourOffered || this.timePlannings.length === 0 || this.helpTour.hasSeen('page')) {
      return;
    }
    this.pageTourOffered = true;
    // Let the current change-detection pass render the grid, or start() finds no
    // anchors and drops every step it was meant to point at.
    setTimeout(() => this.helpTour.start('page', { isAdmin: this.isAdmin }));
  }

  ngOnDestroy(): void {
  }

  goBackward() {
    const tempEndDate = new Date(this.dateTo);
    tempEndDate.setHours(0, 0, 0, 0);

    let daysCount = (Math.floor((tempEndDate.getTime() - this.dateFrom.getTime()) / (1000 * 3600 * 24)) * -1) -1;
    this.dateFrom = this.addDays(this.dateFrom, daysCount);
    this.dateTo = this.addDays(this.dateTo, daysCount);
    this.getPlannings();
  }

  openDownloadExcelDialog() {
          const dialogRef = this.dialog.open(DownloadExcelDialogComponent, {
            width: '600px',
            data: this.availableSites,
          });
          dialogRef.afterClosed().subscribe((result) => {
            // if (result) {
            //   this.getPlannings();
            // }
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
        this.timePlannings = planningsResult.model;
      }
    });
  }

  onTagsChanged($event: number[]) {
    this.selectedTagIds = $event;
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

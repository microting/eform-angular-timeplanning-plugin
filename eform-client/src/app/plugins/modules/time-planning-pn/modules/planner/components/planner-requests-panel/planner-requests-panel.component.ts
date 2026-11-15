import {Component, EventEmitter, Input, OnChanges, OnInit, Output, inject} from '@angular/core';
import {MatDialog} from '@angular/material/dialog';
import {Overlay} from '@angular/cdk/overlay';
import {dialogConfigHelper} from 'src/app/common/helpers';
import {MatIcon} from '@angular/material/icon';
import {MatIconButton} from '@angular/material/button';
import {MatTooltip} from '@angular/material/tooltip';
import {TranslatePipe, TranslateService} from '@ngx-translate/core';
import {ToastrService} from 'ngx-toastr';
import {filter} from 'rxjs';
import {AbsenceRequestModel} from '../../../../models';
import {TimePlanningPnAbsenceRequestsService} from '../../../../services';
import {
  AbsenceRequestsApproveModalComponent, AbsenceRequestsRejectModalComponent,
} from '../../../absence-requests/components';
import {PlannerGridModel, PlannerWorkerModel} from '../../models';
import {absenceTypeKey} from '../../planner-absence-types';
import {dateKey, dateKeysBetween, formatPlannerDate, parseCalendarDate} from '../../planner.utils';

/** A pending request as the panel shows it. */
export interface PlannerRequestView {
  request: AbsenceRequestModel;
  worker: PlannerWorkerModel | null;
  name: string;
  from: Date;
  to: Date;
  typeKey: string | null;
  /** False only where the grid says the caller is not this worker's manager. */
  canDecide: boolean;
  /** Days on screen that approving would leave with nobody planned. */
  emptyDays: Date[];
}

/**
 * Screen 5, the vacation-requests side panel (#5489). It reads the existing
 * absence-requests inbox and decides through the existing approve and reject
 * dialogs, so the rule (only a current manager may decide) and the toasts are
 * those of the absence-requests screen.
 */
@Component({
  selector: 'app-planner-requests-panel',
  imports: [TranslatePipe, MatIcon, MatIconButton, MatTooltip],
  templateUrl: './planner-requests-panel.component.html',
  styleUrls: ['./planner-requests-panel.component.scss'],
})
export class PlannerRequestsPanelComponent implements OnInit, OnChanges {
  private absenceRequestsService = inject(TimePlanningPnAbsenceRequestsService);
  private dialog = inject(MatDialog);
  private overlay = inject(Overlay);
  private toastrService = inject(ToastrService);
  private translateService = inject(TranslateService);

  @Input() grid: PlannerGridModel | null = null;
  @Output() closed = new EventEmitter<void>();
  /** "Vis i planen": the grid jumps to the request's range. */
  @Output() showInPlan = new EventEmitter<{ from: Date; to: Date; siteId: number }>();
  /** A request was decided: the grid reloads. */
  @Output() decided = new EventEmitter<void>();
  @Output() pendingCount = new EventEmitter<number>();

  loading = false;
  private requests: AbsenceRequestModel[] = [];
  views: PlannerRequestView[] = [];

  ngOnInit(): void {
    this.load();
  }

  ngOnChanges(): void {
    this.buildViews();
  }

  load(): void {
    this.loading = true;
    this.absenceRequestsService.getInbox().subscribe({
      next: (result) => {
        this.loading = false;
        if (result && result.success) {
          this.requests = (result.model ?? []).filter(r => r.status === 'Pending');
          this.buildViews();
          // Only after a finished read: before it, requests is [] and would reset the button's count.
          this.pendingCount.emit(this.requests.length);
        }
      },
      error: () => {
        this.loading = false;
        this.toastrService.error(this.translateService.instant('Error loading absence requests'));
      },
    });
  }

  private buildViews(): void {
    const workers = this.grid?.workers ?? [];
    this.views = this.requests.map(request => {
      const worker = workers.find(w => w.siteId === request.requestedBySdkSitId) ?? null;
      const from = parseCalendarDate(String(request.dateFrom));
      const to = parseCalendarDate(String(request.dateTo));
      return {
        request,
        worker,
        name: worker?.siteName ?? this.translateService.instant('plannerWorkerN', {id: request.requestedBySdkSitId}),
        from,
        to,
        typeKey: absenceTypeKey(request.days?.[0]?.messageId),
        canDecide: worker?.canApproveAbsence ?? true,
        emptyDays: this.emptyDaysIfApproved(worker, from, to),
      };
    });
  }

  /**
   * The shown days in the request's range where the worker is the only one
   * planned: approving would leave the headcount at 0. Days outside the grid
   * are not known here; "Vis i planen" brings them on screen.
   */
  private emptyDaysIfApproved(worker: PlannerWorkerModel | null, from: Date, to: Date): Date[] {
    if (!worker || !this.grid) {
      return [];
    }
    const inRange = new Set(dateKeysBetween(from, to));
    const cells = new Map(worker.cells.map(c => [dateKey(c.date), c]));
    return this.grid.dayTotals
      .filter(total => {
        const key = dateKey(total.date);
        const cell = cells.get(key);
        const working = !!cell && cell.planHours > 0 && cell.messageId == null;
        return inRange.has(key) && working && total.plannedHeadcount <= 1;
      })
      .map(total => parseCalendarDate(total.date));
  }

  approve(view: PlannerRequestView): void {
    this.dialog.open(AbsenceRequestsApproveModalComponent,
      dialogConfigHelper(this.overlay, {selectedAbsenceRequest: {...view.request}}))
      .afterClosed().pipe(filter(Boolean)).subscribe(() => this.afterDecision());
  }

  reject(view: PlannerRequestView): void {
    this.dialog.open(AbsenceRequestsRejectModalComponent,
      dialogConfigHelper(this.overlay, {selectedAbsenceRequest: {...view.request}}))
      .afterClosed().pipe(filter(Boolean)).subscribe(() => this.afterDecision());
  }

  private afterDecision(): void {
    this.load();
    this.decided.emit();
  }

  show(view: PlannerRequestView): void {
    this.showInPlan.emit({from: view.from, to: view.to, siteId: view.request.requestedBySdkSitId});
  }

  formatDate(date: Date): string {
    return formatPlannerDate(this.translateService, date);
  }

  formatDates(dates: Date[]): string {
    return dates.map(d => this.formatDate(d)).join(', ');
  }
}

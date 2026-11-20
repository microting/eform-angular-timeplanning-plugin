import {Component, DestroyRef, EventEmitter, Input, OnChanges, OnInit, Output, inject} from '@angular/core';
import {MatDialog} from '@angular/material/dialog';
import {Overlay} from '@angular/cdk/overlay';
import {dialogConfigHelper} from 'src/app/common/helpers';
import {MatIcon} from '@angular/material/icon';
import {MatIconButton} from '@angular/material/button';
import {MatTooltip} from '@angular/material/tooltip';
import {TranslatePipe, TranslateService} from '@ngx-translate/core';
import {ToastrService} from 'ngx-toastr';
import {format, isToday, isYesterday, parseJSON} from 'date-fns';
import {filter, Subscription} from 'rxjs';
import {AbsenceRequestModel} from '../../../../models';
import {TimePlanningPnAbsenceRequestsService} from '../../../../services';
import {
  AbsenceRequestsApproveModalComponent, AbsenceRequestsRejectModalComponent,
} from '../../../absence-requests/components';
import {PlannerAbsenceOutcomes, PlannerAbsencePreviewModel, PlannerGridModel, PlannerWorkerModel} from '../../models';
import {TimePlanningPnPlannerService} from '../../services';
import {absenceTypeKey} from '../../planner-absence-types';
import {
  capitalizeFirst, dateKey, dateKeysBetween, formatPlannerDate, formatPlannerHours, onNoReply, parseCalendarDate, toTransportDate,
  workerInitials,
} from '../../planner.utils';

/** A pending request as the panel shows it. */
export interface PlannerRequestView {
  request: AbsenceRequestModel;
  worker: PlannerWorkerModel | null;
  name: string;
  initials: string;
  /** "i dag kl. 08:40", "i går kl. 14:12" or "9. nov. 2026 kl. 10:05". */
  sent: string;
  from: Date;
  to: Date;
  /** The requested absence type (Message id), or null. */
  messageId: number | null;
  typeKey: string | null;
  /** False only where the grid says the caller is not this worker's manager. */
  canDecide: boolean;
  /** "Fra planen": "2 arbejdsdage · 10 t", or null until (or unless) the absence preview answers. */
  fromPlan: string | null;
  /** Staffing on the shown days of the request, one line per day that needs a look. */
  staffing: string[];
}

/** One shown day of a request where approving thins out the staffing. */
export interface PlannerStaffingDay {
  date: Date;
  /** Other workers planned that day who already have absence. */
  absentNames: string[];
  /** Workers still at work after approving. */
  working: number;
  /** Workers planned that day before any absence. */
  planned: number;
}

/**
 * The shown days in the request's range where the worker is planned to work
 * and approving needs a look: someone else is already absent, or nobody would
 * be left. `planned` counts every worker with plan hours that day (absent or
 * not); `working` is the grid's headcount (plan hours and no absence) less the
 * worker. Days outside the grid are not known here; "Vis i planen" brings them
 * on screen.
 */
export function staffingDays(grid: PlannerGridModel | null, worker: PlannerWorkerModel | null,
                             from: Date, to: Date): PlannerStaffingDay[] {
  if (!worker || !grid) {
    return [];
  }
  const inRange = new Set(dateKeysBetween(from, to));
  const cellsByDate = (w: PlannerWorkerModel) => new Map(w.cells.map(c => [dateKey(c.date), c]));
  const own = cellsByDate(worker);
  const others = grid.workers
    .filter(w => w.assignedSiteId !== worker.assignedSiteId)
    .map(w => ({name: w.siteName, cells: cellsByDate(w)}));
  const days: PlannerStaffingDay[] = [];
  for (const total of grid.dayTotals) {
    const key = dateKey(total.date);
    if (!inRange.has(key)) {
      continue;
    }
    const ownCell = own.get(key);
    if (!ownCell || ownCell.planHours <= 0 || ownCell.messageId != null) {
      continue;
    }
    const absentNames = others
      .filter(o => {
        const cell = o.cells.get(key);
        return !!cell && cell.planHours > 0 && cell.messageId != null;
      })
      .map(o => o.name);
    const working = Math.max(total.plannedHeadcount - 1, 0);
    if (absentNames.length > 0 || working === 0) {
      days.push({date: parseCalendarDate(total.date), absentNames, working, planned: total.plannedHeadcount + absentNames.length});
    }
  }
  return days;
}

/**
 * "Fra planen" from the absence preview: the days with plan hours that the
 * request would turn into absence. The pending request itself holds its whole
 * period, so the preview reports those days as overlap rather than apply;
 * both count. Locked days and days without a plan do not.
 */
export function planFromPreview(preview: PlannerAbsencePreviewModel): { days: number; hours: number } {
  const counted = (preview.days ?? []).filter(d => d.planHours > 0
    && (d.outcome === PlannerAbsenceOutcomes.apply || d.outcome === PlannerAbsenceOutcomes.overlap));
  return {days: counted.length, hours: counted.reduce((sum, d) => sum + d.planHours, 0)};
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
  private plannerService = inject(TimePlanningPnPlannerService);
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

  loading = false;
  private requests: AbsenceRequestModel[] = [];
  private load$: Subscription | null = null;
  /** The "Fra planen" previews of the current views; replaced (cancelled) on every rebuild. */
  private previews$ = new Subscription();
  views: PlannerRequestView[] = [];

  constructor() {
    inject(DestroyRef).onDestroy(() => {
      this.load$?.unsubscribe();
      this.previews$.unsubscribe();
    });
  }

  ngOnInit(): void {
    this.load();
  }

  ngOnChanges(): void {
    this.buildViews();
  }

  load(): void {
    // Cancel an older read: its reply could otherwise land last and bring back
    // requests that were decided since, with a stale pending count.
    this.load$?.unsubscribe();
    this.loading = true;
    this.load$ = this.absenceRequestsService.getInbox().pipe(onNoReply(() => { this.loading = false; })).subscribe({
      next: (result) => {
        this.loading = false;
        if (result && result.success) {
          this.requests = (result.model ?? []).filter(r => r.status === 'Pending');
          this.buildViews();
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
      const from = parseCalendarDate(request.dateFrom);
      const to = parseCalendarDate(request.dateTo);
      const name = worker?.siteName ?? this.translateService.instant('plannerWorkerN', {id: request.requestedBySdkSitId});
      const messageId = request.days?.[0]?.messageId ?? null;
      return {
        request,
        worker,
        name,
        initials: workerInitials(name),
        sent: this.formatSent(request.requestedAtUtc),
        from,
        to,
        messageId,
        typeKey: absenceTypeKey(messageId),
        canDecide: worker?.canApproveAbsence ?? true,
        fromPlan: null,
        staffing: staffingDays(this.grid, worker, from, to).map(day => this.staffingText(day)),
      };
    });
    this.loadPreviews();
  }

  /**
   * One absence preview per request, for "Fra planen". A refusal (not this
   * worker's manager, a range too long), a failure or no reply leaves the row
   * out; the preview is a no-toast call, so nothing is shown for it.
   */
  private loadPreviews(): void {
    this.previews$.unsubscribe();
    this.previews$ = new Subscription();
    for (const view of this.views) {
      if (!view.worker || view.messageId == null) {
        continue;
      }
      this.previews$.add(this.plannerService.previewAbsence({
        assignedSiteId: view.worker.assignedSiteId,
        from: toTransportDate(view.from),
        to: toTransportDate(view.to),
        messageId: view.messageId,
      }).subscribe({
        next: (result) => {
          if (result?.success && result.model) {
            view.fromPlan = this.formatFromPlan(planFromPreview(result.model));
          }
        },
        // Deliberately silent: "Fra planen" is a hint, so a failed preview just leaves the row out.
        error: () => undefined,
      }));
    }
  }

  private formatFromPlan(plan: { days: number; hours: number }): string {
    const hours = formatPlannerHours(this.translateService, plan.hours);
    return plan.days === 1
      ? this.translateService.instant('plannerOneWorkDayHours', {hours})
      : this.translateService.instant('plannerWorkDaysHours', {days: plan.days, hours});
  }

  /** "i dag kl. 08:40" / "i går kl. 14:12" / "9. nov. 2026 kl. 10:05", in local time. */
  formatSent(value: Date | string | null | undefined): string {
    if (!value) {
      return '';
    }
    const sent = value instanceof Date ? value : parseJSON(value);
    if (isNaN(sent.getTime())) {
      return '';
    }
    const time = format(sent, 'HH:mm');
    if (isToday(sent)) {
      return this.translateService.instant('plannerSentToday', {time});
    }
    if (isYesterday(sent)) {
      return this.translateService.instant('plannerSentYesterday', {time});
    }
    return this.translateService.instant('plannerSentOn', {
      date: formatPlannerDate(this.translateService, sent, {day: 'numeric', month: 'short', year: 'numeric'}),
      time,
    });
  }

  private staffingText(day: PlannerStaffingDay): string {
    const date = formatPlannerDate(this.translateService, day.date, {weekday: 'long', day: 'numeric', month: 'short'});
    const params = {day: capitalizeFirst(this.translateService, date), n: day.working, m: day.planned};
    return day.absentNames.length > 0
      ? this.translateService.instant('plannerStaffingOthersAbsent', {...params, names: day.absentNames.join(', ')})
      : this.translateService.instant('plannerStaffingNobodyLeft', params);
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

  /** "Periode": one day, or the first – the last. */
  formatPeriod(view: PlannerRequestView): string {
    const from = formatPlannerDate(this.translateService, view.from);
    return view.from.getTime() === view.to.getTime()
      ? from
      : `${from} – ${formatPlannerDate(this.translateService, view.to)}`;
  }
}

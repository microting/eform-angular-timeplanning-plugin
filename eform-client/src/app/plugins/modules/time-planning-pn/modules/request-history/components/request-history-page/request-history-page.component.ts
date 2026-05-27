import { Component, OnInit, OnDestroy, inject, LOCALE_ID } from '@angular/core';
import { formatDate } from '@angular/common';
import { TimePlanningPnRequestHistoryService, RequestHistoryQueryParams } from '../../../../services';
import { Subscription, forkJoin, of } from 'rxjs';
import { catchError } from 'rxjs/operators';
import { TranslateService } from '@ngx-translate/core';
import { MtxGridColumn } from '@ng-matero/extensions/grid';
import { AutoUnsubscribe } from 'ngx-auto-unsubscribe';
import { ToastrService } from 'ngx-toastr';
import { messages } from '../../../../consts/messages';

export interface RequestHistoryRow {
  type: string;
  date: string;
  detail?: string;
  dateFrom?: string;
  dateTo?: string;
  fromWorker: string;
  toWorker: string;
  status: string;
  requestedAt: Date;
  respondedAt?: Date;
  comment?: string;
}

@AutoUnsubscribe()
@Component({
  selector: 'app-request-history-page',
  templateUrl: './request-history-page.component.html',
  styleUrls: ['./request-history-page.component.scss'],
  standalone: false
})
export class RequestHistoryPageComponent implements OnInit, OnDestroy {
  private requestHistoryService = inject(TimePlanningPnRequestHistoryService);
  private translateService = inject(TranslateService);
  private toastrService = inject(ToastrService);
  private locale = inject(LOCALE_ID);

  rows: RequestHistoryRow[] = [];
  filteredRows: RequestHistoryRow[] = [];
  isLoading = false;

  // Filter state
  filterType = '';
  filterStatus = '';
  filterFromDate = '';
  filterToDate = '';

  // Filter dropdown options (id = filter value, value = localized label)
  typeOptions: { id: string; value: string }[] = [];
  statusOptions: { id: string; value: string }[] = [];

  loadSub$: Subscription;

  private _tableHeaders: MtxGridColumn[];

  get tableHeaders(): MtxGridColumn[] {
    return this._tableHeaders;
  }

  ngOnInit(): void {
    this.typeOptions = [
      { id: '', value: this.translateService.instant('All') },
      { id: 'Handover', value: this.translateService.instant('Handover') },
      { id: 'Absence', value: this.translateService.instant('Absence') },
    ];
    this.statusOptions = [
      { id: '', value: this.translateService.instant('All') },
      { id: 'Pending', value: this.translateService.instant('Pending') },
      { id: 'Approved', value: this.translateService.instant('Approved') },
      { id: 'Accepted', value: this.translateService.instant('Accepted') },
      { id: 'Rejected', value: this.translateService.instant('Rejected') },
      { id: 'Cancelled', value: this.translateService.instant('Cancelled') },
      { id: 'Expired', value: this.translateService.instant('Expired') },
    ];

    this._tableHeaders = [
      { header: this.translateService.stream('Type'), field: 'type', width: '100px' },
      { header: this.translateService.stream('Date'), field: 'date', width: '170px' },
      { header: this.translateService.stream('Details'), field: 'detail' },
      { header: this.translateService.stream('From'), field: 'fromWorker' },
      { header: this.translateService.stream('To'), field: 'toWorker' },
      { header: this.translateService.stream('Status'), field: 'status', width: '110px' },
      { header: this.translateService.stream('Requested'), field: 'requestedAt', type: 'date', typeParameter: { format: 'dd.MM.y HH:mm' }, width: '150px' },
      { header: this.translateService.stream('Responded'), field: 'respondedAt', type: 'date', typeParameter: { format: 'dd.MM.y HH:mm' }, width: '150px' },
      { header: this.translateService.stream('Comment'), field: 'comment' },
    ];
    this.loadData();
  }

  ngOnDestroy(): void {}

  applyFilters(): void {
    this.loadData();
  }

  loadData(): void {
    this.isLoading = true;
    const params: RequestHistoryQueryParams = {};

    if (this.filterStatus) {
      params.status = this.filterStatus;
    }
    if (this.filterFromDate) {
      params.fromDate = this.filterFromDate;
    }
    if (this.filterToDate) {
      params.toDate = this.filterToDate;
    }

    this.loadSub$ = forkJoin({
      handovers: this.requestHistoryService.getAllHandoverRequests(params).pipe(
        catchError(() => {
          this.toastrService.error(
            this.translateService.instant('Error loading handover requests')
          );
          return of({ success: false, model: [] });
        })
      ),
      absences: this.requestHistoryService.getAllAbsenceRequests(params).pipe(
        catchError(() => {
          this.toastrService.error(
            this.translateService.instant('Error loading absence requests')
          );
          return of({ success: false, model: [] });
        })
      ),
    }).subscribe({
      next: ({ handovers, absences }) => {
        const merged: RequestHistoryRow[] = [];

        const messageLabels = this.buildMessageLabels();

        if (handovers && handovers.success && handovers.model) {
          for (const h of handovers.model) {
            merged.push({
              type: 'Handover',
              date: this.formatDay(h.date),
              detail: this.buildHandoverDetail(h),
              fromWorker: h.fromWorkerName || '',
              toWorker: h.toWorkerName || '',
              status: h.status,
              requestedAt: h.requestedAtUtc,
              respondedAt: h.respondedAtUtc || null,
              comment: h.requestComment || '',
            });
          }
        }

        if (absences && absences.success && absences.model) {
          for (const a of absences.model) {
            const fromDay = this.formatDay(a.dateFrom);
            const toDay = this.formatDay(a.dateTo);
            const dateStr = fromDay === toDay ? fromDay : `${fromDay} – ${toDay}`;
            merged.push({
              type: 'Absence',
              date: dateStr,
              detail: this.buildAbsenceDetail(a, messageLabels),
              dateFrom: a.dateFrom,
              dateTo: a.dateTo,
              fromWorker: a.requestedByWorkerName || '',
              toWorker: a.decidedByWorkerName || '',
              status: a.status,
              requestedAt: a.requestedAtUtc,
              respondedAt: a.decidedAtUtc || null,
              comment: a.requestComment || '',
            });
          }
        }

        // Sort by requestedAt descending
        merged.sort((a, b) => {
          const da = new Date(a.requestedAt).getTime();
          const db = new Date(b.requestedAt).getTime();
          return db - da;
        });

        this.rows = merged;
        this.applyClientFilters();
        this.isLoading = false;
      },
      error: () => {
        this.toastrService.error(
          this.translateService.instant('Error loading request history')
        );
        this.isLoading = false;
      }
    });
  }

  applyClientFilters(): void {
    let result = [...this.rows];

    if (this.filterType) {
      result = result.filter(r => r.type === this.filterType);
    }

    this.filteredRows = result;
  }

  /** Formats a date as dd.MM.y, matching the reportsv2 (Logbøger) view. */
  private formatDay(value: string | Date): string {
    return value ? formatDate(value, 'dd.MM.y', this.locale) : '';
  }

  /** Maps absence message ids to their localized labels (Vacation, Sick, ...). */
  private buildMessageLabels(): Map<number, string> {
    return new Map(messages(this.translateService).map(m => [m.id, m.value]));
  }

  /** Distinct absence types in the request, e.g. "Ferie" or "Sygdom, Fridag". */
  private buildAbsenceDetail(
    absence: { days?: { messageId: number }[] },
    labels: Map<number, string>
  ): string {
    const ids = (absence.days || []).map(d => d.messageId);
    const distinct = Array.from(new Set(ids));
    return distinct
      .map(id => labels.get(id))
      .filter((label): label is string => !!label && label.trim() !== '')
      .join(', ');
  }

  /** What is being handed over, e.g. "Skift 1: 08:00–16:00". */
  private buildHandoverDetail(
    handover: { shiftIndex?: number; shiftStartTime?: number; shiftEndTime?: number }
  ): string {
    if (!handover.shiftIndex) {
      return '';
    }
    const shiftLabel = `${this.translateService.instant('Shift')} ${handover.shiftIndex}`;
    const start = this.minutesToTime(handover.shiftStartTime);
    const end = this.minutesToTime(handover.shiftEndTime);
    return start && end ? `${shiftLabel}: ${start}–${end}` : shiftLabel;
  }

  /** Converts minutes-from-midnight to HH:mm, or '' when unset. */
  private minutesToTime(minutes?: number): string {
    if (minutes == null || minutes === 0) {
      return '';
    }
    const hours = Math.floor(minutes / 60);
    const mins = minutes % 60;
    return `${this.padZero(hours)}:${this.padZero(mins)}`;
  }

  private padZero(value: number): string {
    return value.toString().padStart(2, '0');
  }
}

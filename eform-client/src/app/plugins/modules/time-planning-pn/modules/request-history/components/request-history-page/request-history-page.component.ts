import { Component, OnInit, OnDestroy, inject } from '@angular/core';
import { TimePlanningPnRequestHistoryService, RequestHistoryQueryParams } from '../../../../services';
import { Subscription, forkJoin, of } from 'rxjs';
import { catchError } from 'rxjs/operators';
import { TranslateService } from '@ngx-translate/core';
import { MtxGridColumn } from '@ng-matero/extensions/grid';
import { AutoUnsubscribe } from 'ngx-auto-unsubscribe';
import { ToastrService } from 'ngx-toastr';

export interface RequestHistoryRow {
  type: string;
  date: string;
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

  rows: RequestHistoryRow[] = [];
  filteredRows: RequestHistoryRow[] = [];
  isLoading = false;

  // Filter state
  filterType = '';
  filterStatus = '';
  filterFromDate = '';
  filterToDate = '';

  loadSub$: Subscription;

  private _tableHeaders: MtxGridColumn[];

  get tableHeaders(): MtxGridColumn[] {
    return this._tableHeaders;
  }

  ngOnInit(): void {
    this._tableHeaders = [
      { header: this.translateService.stream('Type'), field: 'type', width: '100px' },
      { header: this.translateService.stream('Date'), field: 'date', width: '110px' },
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

        if (handovers && handovers.success && handovers.model) {
          for (const h of handovers.model) {
            merged.push({
              type: 'Handover',
              date: h.date,
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
            const dateStr = a.dateFrom === a.dateTo
              ? a.dateFrom
              : `${a.dateFrom} - ${a.dateTo}`;
            merged.push({
              type: 'Absence',
              date: dateStr,
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
}

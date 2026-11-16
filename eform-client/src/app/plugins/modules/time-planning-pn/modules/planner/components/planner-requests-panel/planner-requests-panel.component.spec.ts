import {describe, it, expect, vi} from 'vitest';
import {TestBed} from '@angular/core/testing';
import {Overlay} from '@angular/cdk/overlay';
import {MatDialog} from '@angular/material/dialog';
import {TranslateService} from '@ngx-translate/core';
import {ToastrService} from 'ngx-toastr';
import {EMPTY, Subject} from 'rxjs';
import {TimePlanningPnAbsenceRequestsService} from '../../../../services';
import {PlannerRequestsPanelComponent} from './planner-requests-panel.component';

function createPanel(getInbox: () => unknown): PlannerRequestsPanelComponent {
  TestBed.configureTestingModule({
    providers: [
      {provide: TimePlanningPnAbsenceRequestsService, useValue: {getInbox}},
      {provide: MatDialog, useValue: {}},
      {provide: Overlay, useValue: {}},
      {provide: ToastrService, useValue: {error: vi.fn()}},
      {provide: TranslateService, useValue: {instant: (key: string) => key, currentLang: 'da', defaultLang: 'da'}},
    ],
  });
  return TestBed.runInInjectionContext(() => new PlannerRequestsPanelComponent());
}

describe('PlannerRequestsPanelComponent', () => {
  it('stops loading when the inbox read gets no reply (the host interceptor answers with EMPTY)', () => {
    const panel = createPanel(vi.fn(() => EMPTY));

    panel.load();

    expect(panel.loading).toBe(false);
  });

  it('ignores an older inbox reply that lands after a newer read was started', () => {
    const first = new Subject<unknown>();
    const second = new Subject<unknown>();
    const panel = createPanel(vi.fn().mockReturnValueOnce(first).mockReturnValueOnce(second));
    const counts: number[] = [];
    panel.pendingCount.subscribe(n => counts.push(n));

    panel.load();
    panel.load();
    second.next({success: true, model: []});
    first.next({success: true, model: [{status: 'Pending', requestedBySdkSitId: 1, dateFrom: '2026-11-19', dateTo: '2026-11-20', days: []}]});

    expect(counts).toEqual([0]);
    expect(panel.views).toEqual([]);
  });
});

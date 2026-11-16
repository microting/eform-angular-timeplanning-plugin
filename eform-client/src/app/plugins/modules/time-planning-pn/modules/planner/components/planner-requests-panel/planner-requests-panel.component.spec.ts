import {describe, it, expect, vi} from 'vitest';
import {TestBed} from '@angular/core/testing';
import {Overlay} from '@angular/cdk/overlay';
import {MatDialog} from '@angular/material/dialog';
import {TranslateService} from '@ngx-translate/core';
import {ToastrService} from 'ngx-toastr';
import {EMPTY} from 'rxjs';
import {TimePlanningPnAbsenceRequestsService} from '../../../../services';
import {PlannerRequestsPanelComponent} from './planner-requests-panel.component';

describe('PlannerRequestsPanelComponent', () => {
  it('stops loading when the inbox read gets no reply (the host interceptor answers with EMPTY)', () => {
    TestBed.configureTestingModule({
      providers: [
        {provide: TimePlanningPnAbsenceRequestsService, useValue: {getInbox: vi.fn(() => EMPTY)}},
        {provide: MatDialog, useValue: {}},
        {provide: Overlay, useValue: {}},
        {provide: ToastrService, useValue: {error: vi.fn()}},
        {provide: TranslateService, useValue: {instant: (key: string) => key, currentLang: 'da', defaultLang: 'da'}},
      ],
    });
    const panel = TestBed.runInInjectionContext(() => new PlannerRequestsPanelComponent());

    panel.load();

    expect(panel.loading).toBe(false);
  });
});

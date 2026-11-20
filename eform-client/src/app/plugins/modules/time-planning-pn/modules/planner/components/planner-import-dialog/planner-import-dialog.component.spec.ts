import {describe, it, expect, vi} from 'vitest';
import {TestBed} from '@angular/core/testing';
import {provideNativeDateAdapter} from '@angular/material/core';
import {MatDialogRef} from '@angular/material/dialog';
import {provideNoopAnimations} from '@angular/platform-browser/animations';
import {TranslateModule} from '@ngx-translate/core';
import {ToastrService} from 'ngx-toastr';
import {of} from 'rxjs';
import {PlannerImportWorkerModel} from '../../models';
import {TimePlanningPnPlannerService} from '../../services';
import {da} from '../../../../i18n/da';
import {enUS} from '../../../../i18n/enUS';
import {importConfidenceChip, PlannerImportDialogComponent} from './planner-import-dialog.component';

const week = (text: string) => Array.from({length: 7}, (_, d) => ({weekIndex: 0, dayOfWeek: d, planText: d < 5 ? text : ''}));

function importWorker(assignedSiteId: number, extra: Partial<PlannerImportWorkerModel>): PlannerImportWorkerModel {
  return {
    assignedSiteId, siteId: assignedSiteId + 100, siteName: `Worker ${assignedSiteId}`, recognised: true, reason: null,
    confidence: null, rotationWeeks: 1, anchorDate: '2026-07-06T00:00:00', days: week('7:00-15:00/0.5'), ...extra,
  };
}

describe('PlannerImportDialogComponent "Sikkerhed"', () => {
  it('maps the server\'s confidence codes to a chip, and nothing else to one', () => {
    expect(importConfidenceChip('high')).toEqual({key: 'plannerConfidenceHigh', chipClass: 'planner-chip-success'});
    expect(importConfidenceChip('medium')).toEqual({key: 'plannerConfidenceMedium', chipClass: 'planner-chip-warning'});
    expect(importConfidenceChip(null)).toBeNull();
    expect(importConfidenceChip('unknown')).toBeNull();
    for (const key of ['plannerImportConfidence', 'plannerConfidenceHigh', 'plannerConfidenceMedium']) {
      expect(Object.keys(da)).toContain(key);
      expect(Object.keys(enUS)).toContain(key);
    }
  });

  it('shows Høj in green and Middel in amber in the review, and no chip for an unrecognised worker', async () => {
    const getImportPreview = vi.fn(() => of({success: true, model: {
      windowStart: '2026-07-06T00:00:00',
      workers: [
        importWorker(1, {confidence: 'high'}),
        importWorker(2, {confidence: 'medium'}),
        importWorker(3, {recognised: false, reason: 'inconsistent', rotationWeeks: null, anchorDate: null, days: []}),
      ],
    }}));
    TestBed.configureTestingModule({
      imports: [PlannerImportDialogComponent, TranslateModule.forRoot()],
      providers: [
        provideNoopAnimations(),
        provideNativeDateAdapter(),
        {provide: TimePlanningPnPlannerService, useValue: {getImportPreview}},
        {provide: MatDialogRef, useValue: {close: vi.fn()}},
        {provide: ToastrService, useValue: {success: vi.fn(), error: vi.fn(), warning: vi.fn()}},
      ],
    });
    const fixture = TestBed.createComponent(PlannerImportDialogComponent);
    // Straight to step 2 (review); the first render reads the preview.
    fixture.componentInstance.step = 'review';
    fixture.detectChanges();
    await fixture.whenStable();

    const el: HTMLElement = fixture.nativeElement;
    const high = el.querySelector('[data-testid="planner-import-confidence-1"]');
    const medium = el.querySelector('[data-testid="planner-import-confidence-2"]');
    expect(high?.textContent?.trim()).toBe('plannerConfidenceHigh');
    expect(high?.classList).toContain('planner-chip');
    expect(high?.classList).toContain('planner-chip-success');
    expect(medium?.textContent?.trim()).toBe('plannerConfidenceMedium');
    expect(medium?.classList).toContain('planner-chip-warning');
    expect(el.querySelector('[data-testid="planner-import-confidence-3"]')).toBeNull();
    expect(el.querySelector('[data-testid="planner-import-unrecognised-3"]')?.getAttribute('colspan')).toBe('5');
    expect(el.querySelectorAll('thead th')).toHaveLength(7);
  });
});

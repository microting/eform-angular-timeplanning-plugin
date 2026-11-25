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
import {ALL_LANGUAGES} from '../../../../testing/all-languages';
import {importConfidenceChip, importEvidence, PlannerImportDialogComponent} from './planner-import-dialog.component';

const week = (text: string) => Array.from({length: 7}, (_, d) => ({weekIndex: 0, dayOfWeek: d, planText: d < 5 ? text : ''}));

function importWorker(assignedSiteId: number, extra: Partial<PlannerImportWorkerModel>): PlannerImportWorkerModel {
  return {
    assignedSiteId, siteId: assignedSiteId + 100, siteName: `Worker ${assignedSiteId}`, recognised: true, reason: null,
    confidence: null, rotationWeeks: 1, anchorDate: '2026-07-06T00:00:00', days: week('7:00-15:00/0.5'),
    cyclesSeen: null, weeksSeen: null, ...extra,
  };
}

/** The review step, rendered from a preview of these workers. */
async function renderReview(workers: PlannerImportWorkerModel[]): Promise<HTMLElement> {
  const getImportPreview = vi.fn(() => of({success: true, model: {windowStart: '2026-07-06T00:00:00', workers}}));
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
  return fixture.nativeElement;
}

describe('PlannerImportDialogComponent "Det vi fandt" evidence', () => {
  const empty = Array.from({length: 7}, (_, d) => ({weekIndex: 0, dayOfWeek: d, planText: ''}));

  it('says how much of the sheet backs the proposal, and warns on medium', () => {
    expect(importEvidence(importWorker(1, {confidence: 'high', weeksSeen: 8})))
      .toEqual({key: 'plannerImportSeenWeeks', params: {weeks: 8}, warning: false});
    // A 1-week pattern: whole weeks with every day seen (cyclesSeen), not weeks with any day seen.
    expect(importEvidence(importWorker(1, {confidence: 'medium', weeksSeen: 5, cyclesSeen: 2})))
      .toEqual({key: 'plannerImportSeenOnlyWeeks', params: {weeks: 2}, warning: true});
    expect(importEvidence(importWorker(1, {confidence: 'medium', rotationWeeks: 2, weeksSeen: 4, cyclesSeen: 2})))
      .toEqual({key: 'plannerImportSeenOnlyCycles', params: {cycles: 2}, warning: true});
    expect(importEvidence(importWorker(1, {confidence: 'medium', days: empty})))
      .toEqual({key: 'plannerImportEmptySheet', params: {}, warning: true});
  });

  it('says nothing without the numbers, without a confidence, or for an unrecognised worker', () => {
    expect(importEvidence(importWorker(1, {confidence: 'high'}))).toBeNull();
    expect(importEvidence(importWorker(1, {confidence: 'medium'}))).toBeNull();
    expect(importEvidence(importWorker(1, {confidence: 'medium', weeksSeen: 5}))).toBeNull();
    expect(importEvidence(importWorker(1, {confidence: 'medium', rotationWeeks: 2, weeksSeen: 4}))).toBeNull();
    expect(importEvidence(importWorker(1, {confidence: null, weeksSeen: 8}))).toBeNull();
    expect(importEvidence(importWorker(1, {recognised: false, reason: 'inconsistent', weeksSeen: 8}))).toBeNull();
  });

  it('renders the line under the shift summary, amber for medium', async () => {
    const el = await renderReview([
      importWorker(1, {confidence: 'high', weeksSeen: 8, cyclesSeen: 8}),
      importWorker(2, {confidence: 'medium', rotationWeeks: 2, weeksSeen: 4, cyclesSeen: 2}),
      importWorker(3, {confidence: 'high'}),
      importWorker(4, {recognised: false, reason: 'inconsistent', rotationWeeks: null, anchorDate: null, days: []}),
    ]);

    const high = el.querySelector('[data-testid="planner-import-evidence-1"]');
    const medium = el.querySelector('[data-testid="planner-import-evidence-2"]');
    expect(high?.textContent?.trim()).toBe('plannerImportSeenWeeks');
    expect(high?.classList).not.toContain('planner-import-evidence-warning');
    expect(high?.parentElement?.textContent).toContain('7:00-15:00/0.5');
    expect(medium?.textContent?.trim()).toBe('plannerImportSeenOnlyCycles');
    expect(medium?.classList).toContain('planner-import-evidence-warning');
    expect(el.querySelector('[data-testid="planner-import-evidence-3"]')).toBeNull();
    expect(el.querySelector('[data-testid="planner-import-evidence-4"]')).toBeNull();
  });

  it('has the four lines in every language, with the numbers in them', () => {
    for (const lang of ALL_LANGUAGES) {
      expect(lang['plannerImportSeenWeeks']).toContain('{{weeks}}');
      expect(lang['plannerImportSeenOnlyWeeks']).toContain('{{weeks}}');
      expect(lang['plannerImportSeenOnlyCycles']).toContain('{{cycles}}');
      expect(lang['plannerImportEmptySheet']).toBeTruthy();
    }
    expect(da.plannerImportSeenOnlyCycles).toBe('Kun {{cycles}} hele cyklusser i arket – tjek mønstret');
  });
});

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

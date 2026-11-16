import {describe, it, expect, beforeEach, vi} from 'vitest';
import {TestBed} from '@angular/core/testing';
import {MAT_DIALOG_DATA, MatDialogRef} from '@angular/material/dialog';
import {TranslateService} from '@ngx-translate/core';
import {ToastrService} from 'ngx-toastr';
import {EMPTY, NEVER, of} from 'rxjs';
import {PlannerWorkerModel} from '../models';
import {TimePlanningPnPlannerService} from '../services';
import {fullPatternGrid} from '../planner.utils';
import {PlannerAbsenceDialogComponent} from './planner-absence-dialog/planner-absence-dialog.component';
import {PlannerImportDialogComponent} from './planner-import-dialog/planner-import-dialog.component';
import {PlannerPatternDialogComponent} from './planner-pattern-dialog/planner-pattern-dialog.component';

/**
 * The host's HTTP error interceptor answers a 401, a 403 whose token refresh
 * failed, and exhausted retries with EMPTY: the request completes without
 * `next` or `error`. The dialogs disable Cancel while busy and have
 * disableClose on, so a busy flag left set would trap the user.
 */
describe('planner dialogs when a request gets no reply', () => {
  const worker = {
    assignedSiteId: 12, siteId: 34, siteName: 'Anna', hasPattern: true, canApproveAbsence: true,
  } as PlannerWorkerModel;

  let service: Record<string, ReturnType<typeof vi.fn>>;
  let dialogRef: { close: ReturnType<typeof vi.fn> };
  let toastr: { success: ReturnType<typeof vi.fn>; error: ReturnType<typeof vi.fn>; warning: ReturnType<typeof vi.fn>; info: ReturnType<typeof vi.fn> };

  beforeEach(() => {
    service = {
      createAbsence: vi.fn(() => EMPTY),
      savePattern: vi.fn(() => EMPTY),
      getManualDays: vi.fn(() => of({success: true, model: []})),
      copyPattern: vi.fn(() => EMPTY),
      deletePattern: vi.fn(() => EMPTY),
      applyImport: vi.fn(() => EMPTY),
    };
    dialogRef = {close: vi.fn()};
    toastr = {success: vi.fn(), error: vi.fn(), warning: vi.fn(), info: vi.fn()};
    TestBed.configureTestingModule({
      providers: [
        {provide: TimePlanningPnPlannerService, useValue: service},
        {provide: MatDialogRef, useValue: dialogRef},
        {provide: MAT_DIALOG_DATA, useValue: {worker, workers: [worker], colleagues: [], assignedSiteId: 12}},
        {provide: ToastrService, useValue: toastr},
        {provide: TranslateService, useValue: {instant: (key: string) => key, currentLang: 'da', defaultLang: 'da'}},
      ],
    });
  });

  it('absence create: releases busy and closes as changed, so the grid reloads', () => {
    const dialog = TestBed.runInInjectionContext(() => new PlannerAbsenceDialogComponent());
    dialog.from = new Date(2026, 10, 9);
    dialog.to = new Date(2026, 10, 13);
    dialog.messageId = 2;

    dialog.submit();

    expect(service.createAbsence).toHaveBeenCalledTimes(1);
    expect(dialog.busy).toBe(false);
    expect(dialogRef.close).toHaveBeenCalledWith(true);
    expect(toastr.warning).toHaveBeenCalledWith('plannerOutcomeUnknown');
  });

  describe('pattern dialog', () => {
    let dialog: PlannerPatternDialogComponent;

    beforeEach(() => {
      dialog = TestBed.runInInjectionContext(() => new PlannerPatternDialogComponent());
      dialog.loading = false;
      dialog.form = 'same';
      dialog.rotationWeeks = 1;
      dialog.anchorDate = new Date(2026, 10, 9);
      dialog.validFrom = new Date(2026, 10, 9);
      dialog.days = fullPatternGrid(1, [{weekIndex: 0, dayOfWeek: 0, planText: '7-15/0,5'}]);
    });

    it('save: releases busy and closes as changed', () => {
      dialog.persist(false);

      expect(dialog.busy).toBe(false);
      expect(dialogRef.close).toHaveBeenCalledWith(true);
      expect(toastr.warning).toHaveBeenCalledWith('plannerOutcomeUnknown');
    });

    it('delete: releases busy and closes as changed', () => {
      dialog.deletePattern();

      expect(dialog.busy).toBe(false);
      expect(dialogRef.close).toHaveBeenCalledWith(true);
    });

    it('copy (a read): releases busy and stays open', () => {
      dialog.copySourceId = 7;
      dialog.copyFromColleague();

      expect(dialog.busy).toBe(false);
      expect(dialogRef.close).not.toHaveBeenCalled();
    });

    it('manual-days lookup (a read): releases busy and stays open', () => {
      service.getManualDays.mockReturnValue(EMPTY);
      dialog.save();

      expect(dialog.busy).toBe(false);
      expect(service.savePattern).not.toHaveBeenCalled();
      expect(dialogRef.close).not.toHaveBeenCalled();
    });

    it('the lookup completing does not release the save it started', () => {
      service.savePattern.mockReturnValue(NEVER);
      dialog.save();

      expect(service.savePattern).toHaveBeenCalledTimes(1);
      expect(dialog.busy).toBe(true);
      expect(dialogRef.close).not.toHaveBeenCalled();
    });
  });

  it('import apply: releases busy and closes as changed', () => {
    const dialog = TestBed.runInInjectionContext(() => new PlannerImportDialogComponent());
    dialog.apply();

    expect(dialog.busy).toBe(false);
    expect(dialogRef.close).toHaveBeenCalledWith(true);
    expect(toastr.warning).toHaveBeenCalledWith('plannerOutcomeUnknown');
  });
});

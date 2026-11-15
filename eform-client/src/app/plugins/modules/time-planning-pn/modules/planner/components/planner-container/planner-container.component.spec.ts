import {describe, it, expect, beforeEach, vi} from 'vitest';
import {TestBed} from '@angular/core/testing';
import {Overlay} from '@angular/cdk/overlay';
import {MatDialog} from '@angular/material/dialog';
import {TranslateService} from '@ngx-translate/core';
import {ToastrService} from 'ngx-toastr';
import {of} from 'rxjs';
import {TimePlanningPnSettingsService} from '../../../../services';
import {TimePlanningPnPlannerService} from '../../services';
import {PlannerContainerComponent} from './planner-container.component';

/** The editor's guards, without rendering the grid. */
describe('PlannerContainerComponent short-form editor', () => {
  let component: PlannerContainerComponent;
  let updateCells: ReturnType<typeof vi.fn>;

  beforeEach(() => {
    updateCells = vi.fn(() => of({success: true, model: {updatedCount: 1, lockedCells: [], errors: [], lockedMessage: null}}));
    TestBed.configureTestingModule({
      providers: [
        {provide: TimePlanningPnPlannerService, useValue: {updateCells, getGrid: vi.fn(() => of({success: false})), getInboxNoToast: vi.fn(() => of({success: true, model: []}))}},
        {provide: TimePlanningPnSettingsService, useValue: {getAvailableTags: vi.fn(() => of({success: true, model: []}))}},
        {provide: MatDialog, useValue: {}},
        {provide: Overlay, useValue: {}},
        {provide: ToastrService, useValue: {success: vi.fn(), error: vi.fn(), warning: vi.fn()}},
        {provide: TranslateService, useValue: {instant: (key: string) => key, currentLang: 'da', defaultLang: 'da'}},
      ],
    });
    component = TestBed.runInInjectionContext(() => new PlannerContainerComponent());
    component.selection = new Set(['12|2026-11-09', '12|2026-11-10']);
  });

  it('does not clear the selected days when Enter is pressed in an empty box', () => {
    component.shortForm = '   ';
    component.onShortFormEnter();
    expect(updateCells).not.toHaveBeenCalled();
  });

  it('does not send unreadable text on Enter', () => {
    component.shortForm = '7-15 syg';
    component.onShortFormEnter();
    expect(updateCells).not.toHaveBeenCalled();
  });

  it('sends readable text on Enter, once while a save is in flight', () => {
    component.shortForm = '7-15/0,5';
    component.saving = true;
    component.onShortFormEnter();
    expect(updateCells).not.toHaveBeenCalled();

    component.saving = false;
    component.onShortFormEnter();
    expect(updateCells).toHaveBeenCalledTimes(1);
    expect(updateCells.mock.calls[0][0]).toMatchObject({shortForm: '7-15/0,5', messageId: null, mode: 'onlyTheseDays'});
  });

  it('still lets "Ryd" clear the days on purpose', () => {
    component.applyShortForm('');
    expect(updateCells).toHaveBeenCalledTimes(1);
    expect(updateCells.mock.calls[0][0]).toMatchObject({shortForm: ''});
  });
});

import { TestBed } from '@angular/core/testing';
import { TranslateService } from '@ngx-translate/core';
import { firstValueFrom } from 'rxjs';
import { HelpTourService, TOUR_STORAGE_KEY } from './help-tour.service';
import { HelpContentService } from './help-content.service';

describe('HelpTourService', () => {
  let service: HelpTourService;

  const anchor = (id: string) => {
    const element = document.createElement('div');
    element.setAttribute('data-tp-help', id);
    document.body.appendChild(element);
  };

  beforeEach(() => {
    document.body.innerHTML = '';
    localStorage.clear();
    TestBed.resetTestingModule();
    TestBed.configureTestingModule({
      providers: [
        HelpTourService,
        HelpContentService,
        { provide: TranslateService, useValue: { currentLang: 'en-US' } },
      ],
    });
    service = TestBed.inject(HelpTourService);
  });

  it('is idle before it starts', async () => {
    expect(await firstValueFrom(service.state$)).toBeNull();
    expect(service.isRunning).toBe(false);
  });

  it('starts on the first step whose anchor exists', async () => {
    anchor('grid.dayCellAnatomy');
    service.start('page', { isAdmin: false });
    const state = await firstValueFrom(service.state$);
    expect(state?.entry.id).toBe('grid.dayCellAnatomy');
    expect(state?.index).toBe(0);
    expect(state?.total).toBe(1);
    expect(service.isRunning).toBe(true);
  });

  it('skips steps with no anchor in the DOM', async () => {
    anchor('toolbar.dateRange');
    anchor('grid.openDay');
    service.start('page', { isAdmin: false });
    let state = await firstValueFrom(service.state$);
    expect(state?.entry.id).toBe('toolbar.dateRange');
    expect(state?.total).toBe(2);
    service.next();
    state = await firstValueFrom(service.state$);
    expect(state?.entry.id).toBe('grid.openDay');
    expect(state?.index).toBe(1);
  });

  it('walks the steps in tourStep order, not registry order', async () => {
    // Registry order puts navForward (step 2) before dateRange (step 1).
    anchor('toolbar.navForward');
    anchor('toolbar.dateRange');
    service.start('page', { isAdmin: false });
    expect((await firstValueFrom(service.state$))?.entry.id).toBe('toolbar.dateRange');
    service.next();
    expect((await firstValueFrom(service.state$))?.entry.id).toBe('toolbar.navForward');
  });

  it('offers the payroll step to an admin whose anchor exists', async () => {
    anchor('toolbar.payrollExport');
    anchor('toolbar.dateRange');
    service.start('page', { isAdmin: true });
    const state = await firstValueFrom(service.state$);
    expect(state?.total).toBe(2);
    service.next();
    expect((await firstValueFrom(service.state$))?.entry.id).toBe('toolbar.payrollExport');
  });

  it('never offers the payroll step to a non-admin even when its anchor exists', async () => {
    anchor('toolbar.payrollExport');
    anchor('toolbar.dateRange');
    service.start('page', { isAdmin: false });
    const state = await firstValueFrom(service.state$);
    expect(state?.total).toBe(1);
    expect(state?.entry.id).toBe('toolbar.dateRange');
  });

  it('runs the dialog tour independently of the page tour', async () => {
    anchor('toolbar.dateRange');
    anchor('dayCell.plannedTimes');
    service.start('dialog', { isAdmin: false });
    const state = await firstValueFrom(service.state$);
    expect(state?.entry.id).toBe('dayCell.plannedTimes');
    expect(state?.total).toBe(1);
  });

  it('ends after the last step', async () => {
    anchor('toolbar.dateRange');
    service.start('page', { isAdmin: false });
    service.next();
    expect(await firstValueFrom(service.state$)).toBeNull();
    expect(service.isRunning).toBe(false);
  });

  it('does not start when no anchor is present', async () => {
    service.start('page', { isAdmin: false });
    expect(await firstValueFrom(service.state$)).toBeNull();
  });

  it('does not mark a tour seen merely by being subscribed to', async () => {
    // Regression guard: state$ replays null to every new subscriber.
    await firstValueFrom(service.state$);
    expect(service.hasSeen('page')).toBe(false);
  });

  it('marks the tour seen once it runs to the end', () => {
    anchor('toolbar.dateRange');
    service.start('page', { isAdmin: false });
    expect(service.hasSeen('page')).toBe(false);
    service.next();
    expect(service.hasSeen('page')).toBe(true);
  });

  it('marks the tour seen when it is skipped', () => {
    anchor('toolbar.dateRange');
    service.start('page', { isAdmin: false });
    service.stop();
    expect(service.hasSeen('page')).toBe(true);
  });

  it('marks only the tour that ran, leaving the other one unseen', () => {
    anchor('dayCell.plannedTimes');
    service.start('dialog', { isAdmin: false });
    service.next();
    expect(service.hasSeen('dialog')).toBe(true);
    expect(service.hasSeen('page')).toBe(false);
  });

  it('does not mark a tour seen when it could not start for lack of anchors', () => {
    service.start('page', { isAdmin: false });
    expect(service.hasSeen('page')).toBe(false);
  });

  it('leaves a tour that never started unseen even after stop()', () => {
    // emit() already cleared `current` because no step was shown, so stop() has
    // nothing to record. This covers emit()'s guard, not a second guard in stop().
    service.start('page', { isAdmin: false });
    service.stop();
    expect(service.hasSeen('page')).toBe(false);
  });

  it('skips a step whose anchor vanished after the tour started', async () => {
    anchor('toolbar.dateRange');
    anchor('toolbar.navForward');
    anchor('grid.openDay');
    service.start('page', { isAdmin: false });
    expect((await firstValueFrom(service.state$))?.total).toBe(3);

    document.querySelector('[data-tp-help="toolbar.navForward"]')!.remove();
    service.next();

    const state = await firstValueFrom(service.state$);
    expect(state?.entry.id).toBe('grid.openDay');
    // The vanished step is dropped rather than counted toward a card never shown.
    expect(state?.total).toBe(2);
    expect(state?.index).toBe(1);
  });

  it('still marks the tour seen when the last remaining anchors vanish', () => {
    anchor('toolbar.dateRange');
    anchor('grid.openDay');
    service.start('page', { isAdmin: false });

    document.querySelector('[data-tp-help="grid.openDay"]')!.remove();
    service.next();

    expect(service.isRunning).toBe(false);
    expect(service.hasSeen('page')).toBe(true);
  });

  it('does not re-mark or throw when stopped twice', () => {
    anchor('toolbar.dateRange');
    service.start('page', { isAdmin: false });
    service.stop();
    expect(() => service.stop()).not.toThrow();
    expect(JSON.parse(localStorage.getItem(TOUR_STORAGE_KEY) as string)).toEqual(['page']);
  });

  it('records that a tour has been seen', () => {
    expect(service.hasSeen('page')).toBe(false);
    service.markSeen('page');
    expect(service.hasSeen('page')).toBe(true);
    expect(localStorage.getItem(TOUR_STORAGE_KEY)).toContain('page');
  });

  it('keeps a previously seen tour when a second one is marked', () => {
    service.markSeen('page');
    service.markSeen('dialog');
    expect(JSON.parse(localStorage.getItem(TOUR_STORAGE_KEY) as string).sort())
      .toEqual(['dialog', 'page']);
  });

  it('reads what an earlier session stored', () => {
    localStorage.setItem(TOUR_STORAGE_KEY, JSON.stringify(['dialog']));
    expect(service.hasSeen('dialog')).toBe(true);
    expect(service.hasSeen('page')).toBe(false);
  });

  it('treats unparsable stored state as nothing seen', () => {
    localStorage.setItem(TOUR_STORAGE_KEY, 'not json');
    expect(service.hasSeen('page')).toBe(false);
  });

  it('survives localStorage being unavailable', () => {
    const getItem = jest.spyOn(Storage.prototype, 'getItem').mockImplementation(() => {
      throw new Error('blocked');
    });
    expect(service.hasSeen('page')).toBe(false);
    getItem.mockRestore();
  });

  it('survives localStorage rejecting a write', () => {
    const setItem = jest.spyOn(Storage.prototype, 'setItem').mockImplementation(() => {
      throw new Error('quota');
    });
    expect(() => service.markSeen('page')).not.toThrow();
    setItem.mockRestore();
  });

  it('resolves the anchor element for an entry, and null when it is gone', () => {
    anchor('toolbar.dateRange');
    const entry = TestBed.inject(HelpContentService).entry('toolbar.dateRange');
    expect(service.anchorElement(entry!)).toBe(
      document.querySelector('[data-tp-help="toolbar.dateRange"]'),
    );
    document.body.innerHTML = '';
    expect(service.anchorElement(entry!)).toBeNull();
  });

  it('has no anchor element for an entry that declares none', () => {
    const task = TestBed.inject(HelpContentService).entry('task.registerVacation');
    expect(task?.anchor).toBeUndefined();
    expect(service.anchorElement(task!)).toBeNull();
  });

  it('restarts cleanly from step one', async () => {
    anchor('toolbar.dateRange');
    anchor('toolbar.navForward');
    service.start('page', { isAdmin: false });
    service.next();
    expect((await firstValueFrom(service.state$))?.index).toBe(1);
    service.start('page', { isAdmin: false });
    expect((await firstValueFrom(service.state$))?.index).toBe(0);
  });
});

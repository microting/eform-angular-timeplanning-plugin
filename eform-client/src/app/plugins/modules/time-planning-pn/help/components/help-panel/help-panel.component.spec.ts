import { SimpleChange } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { TranslateService } from '@ngx-translate/core';
import { HelpEntryId, HelpTourName } from '../../help.model';
import { enUS, enUSUi } from '../../i18n/enUS';
import { HelpPanelService } from '../../services/help-panel.service';
import { HelpPanelComponent } from './help-panel.component';

describe('HelpPanelComponent', () => {
  let fixture: ComponentFixture<HelpPanelComponent>;
  let component: HelpPanelComponent;
  let panel: HelpPanelService;

  const panelEl = () => fixture.nativeElement.querySelector('.tp-help-panel') as HTMLElement | null;
  const text = () => (fixture.nativeElement as HTMLElement).textContent ?? '';
  const sectionHeadings = () =>
    Array.from(fixture.nativeElement.querySelectorAll('.tp-help-panel__section'))
      .map(el => ((el as HTMLElement).textContent ?? '').trim());
  const browsedIds = () => component.sections.flatMap(group => group.entries.map(entry => entry.id));
  const resultIds = () => component.results.map(result => result.entry.id);
  const countText = () =>
    ((fixture.nativeElement.querySelector('.tp-help-panel__count') as HTMLElement | null)?.textContent ?? '')
      .replace(/\s+/g, ' ')
      .trim();

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      declarations: [HelpPanelComponent],
      imports: [FormsModule, MatIconModule, MatButtonModule],
      providers: [
        HelpPanelService,
        { provide: TranslateService, useValue: { currentLang: 'en-US' } },
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(HelpPanelComponent);
    component = fixture.componentInstance;
    panel = TestBed.inject(HelpPanelService);
    fixture.detectChanges();
  });

  it('renders nothing while closed', () => {
    expect(component.isOpen).toBe(false);
    expect(panelEl()).toBeNull();
  });

  it('renders its chrome from the help ui strings, never a translate key', () => {
    panel.open();
    fixture.detectChanges();

    const header = fixture.nativeElement.querySelector('.tp-help-panel__top h5') as HTMLElement;
    const input = fixture.nativeElement.querySelector('.tp-help-panel__search input') as HTMLInputElement;
    const closeButton = fixture.nativeElement.querySelector('.tp-help-panel__top button') as HTMLElement;
    const replayButton = fixture.nativeElement.querySelector('.tp-help-panel__replay button') as HTMLElement;

    expect(header.textContent?.trim()).toBe(enUSUi.help);
    expect(input.placeholder).toBe(enUSUi.searchHelp);
    expect(closeButton.getAttribute('aria-label')).toBe(enUSUi.close);
    expect(replayButton.textContent?.trim()).toBe(enUSUi.replayTour);
    expect(text()).not.toContain('sectionToolbar');
  });

  it('browses grouped sections in order when open with no query', () => {
    panel.open();
    fixture.detectChanges();

    expect(panelEl()).not.toBeNull();
    expect(component.isSearching).toBe(false);
    expect(component.sections.map(group => group.section))
      .toEqual(['task', 'toolbar', 'grid', 'dayCell', 'flex']);
    expect(sectionHeadings()).toEqual([
      enUSUi.sectionTask,
      enUSUi.sectionToolbar,
      enUSUi.sectionGrid,
      enUSUi.sectionDayCell,
      enUSUi.sectionFlex,
    ]);
    expect(text()).toContain(enUS['toolbar.dateRange'].title);
    expect(text()).toContain(enUS['toolbar.dateRange'].short);
  });

  it('groups every entry under its own section', () => {
    component.isAdmin = true;
    panel.open();
    fixture.detectChanges();

    for (const group of component.sections) {
      expect(group.entries.every(entry => entry.section === group.section)).toBe(true);
    }
    expect(browsedIds()).toContain('toolbar.payrollExport');
  });

  it('switches to results when a query is typed and back when it is cleared', () => {
    panel.open();
    fixture.detectChanges();

    component.onQueryChange('vacation');
    fixture.detectChanges();

    expect(component.isSearching).toBe(true);
    expect(component.results.length).toBeGreaterThan(0);
    expect(resultIds()).toContain('task.registerVacation');
    expect(sectionHeadings()).toEqual([]);
    expect(component.isFallback).toBe(false);
    expect(countText()).toBe(`${component.results.length} ${enUSUi.resultCount}`);
    expect(countText()).not.toContain(enUSUi.noResults);

    component.clearQuery();
    fixture.detectChanges();

    expect(component.isSearching).toBe(false);
    expect(component.results).toEqual([]);
    expect(sectionHeadings().length).toBe(5);
  });

  it('drives the query from the search input through ngModel', () => {
    panel.open();
    fixture.detectChanges();

    const input = fixture.nativeElement.querySelector('.tp-help-panel__search input') as HTMLInputElement;
    input.value = 'vacation';
    input.dispatchEvent(new Event('input'));
    fixture.detectChanges();

    expect(component.query).toBe('vacation');
    expect(component.isSearching).toBe(true);
    expect(resultIds()).toContain('task.registerVacation');
  });

  it('treats a whitespace-only query as browsing', () => {
    panel.open();
    component.onQueryChange('   ');
    fixture.detectChanges();

    expect(component.isSearching).toBe(false);
    expect(sectionHeadings().length).toBe(5);
  });

  it('hides admin-only entries when isAdmin is false, in browse and in results', () => {
    component.isAdmin = false;
    panel.open();
    fixture.detectChanges();

    expect(browsedIds()).not.toContain('toolbar.payrollExport');
    expect(text()).not.toContain(enUS['toolbar.payrollExport'].title);

    component.onQueryChange('payroll');
    fixture.detectChanges();

    expect(resultIds().length).toBeGreaterThan(0);
    expect(resultIds()).not.toContain('toolbar.payrollExport');
  });

  it('shows admin-only entries in results when isAdmin is true', () => {
    component.isAdmin = true;
    panel.open();
    component.onQueryChange('payroll');
    fixture.detectChanges();

    expect(resultIds()).toContain('toolbar.payrollExport');
    expect(text()).toContain(enUS['toolbar.payrollExport'].title);
  });

  it('marks and expands the deep-link target', () => {
    panel.open('flex.sumFlex');
    fixture.detectChanges();

    expect(component.targetId).toBe('flex.sumFlex');
    expect(component.expanded).toBe('flex.sumFlex');

    const marked = fixture.nativeElement.querySelector('.tp-help-entry--target') as HTMLElement;
    expect(marked).not.toBeNull();
    expect(marked.textContent).toContain(enUS['flex.sumFlex'].title);
    expect(marked.textContent).toContain(enUS['flex.sumFlex'].detail as string);
  });

  it('expands and collapses an entry on click', () => {
    panel.open();
    fixture.detectChanges();

    expect(text()).not.toContain(enUS['task.registerVacation'].steps?.[0] as string);

    component.toggleEntry('task.registerVacation');
    fixture.detectChanges();

    const steps = Array.from(fixture.nativeElement.querySelectorAll('.tp-help-entry__steps li'))
      .map(el => ((el as HTMLElement).textContent ?? '').trim());
    expect(steps).toEqual(enUS['task.registerVacation'].steps);

    component.toggleEntry('task.registerVacation');
    fixture.detectChanges();

    expect(component.expanded).toBeNull();
    expect(fixture.nativeElement.querySelector('.tp-help-entry__steps')).toBeNull();
  });

  it('closes through the service and forgets the query', () => {
    panel.open();
    component.onQueryChange('vacation');
    fixture.detectChanges();

    component.close();
    fixture.detectChanges();

    expect(panelEl()).toBeNull();
    expect(component.query).toBe('');
    expect(component.results).toEqual([]);

    panel.open();
    fixture.detectChanges();

    expect(component.isSearching).toBe(false);
    expect(sectionHeadings().length).toBe(5);
  });

  const clickReplay = () =>
    (fixture.nativeElement.querySelector('.tp-help-panel__replay button') as HTMLElement).click();

  it('closes and asks the host to replay the page tour', () => {
    const replays: HelpTourName[] = [];
    component.replayTourRequested.subscribe(tour => replays.push(tour));
    panel.open();
    fixture.detectChanges();

    clickReplay();
    fixture.detectChanges();

    expect(replays).toEqual(['page']);
    expect(panelEl()).toBeNull();
  });

  it('asks for the dialog tour when it was opened from the day-cell dialog', () => {
    // One panel serves both surfaces. Replaying the page tour from inside the
    // dialog would anchor every step behind the dialog backdrop and leave a card
    // nobody can reach until the dialog is closed.
    const replays: HelpTourName[] = [];
    component.replayTourRequested.subscribe(tour => replays.push(tour));
    panel.open('dayCell.save', 'dialog');
    fixture.detectChanges();

    clickReplay();
    fixture.detectChanges();

    expect(replays).toEqual(['dialog']);
  });

  it('goes back to the page tour once the panel has closed', () => {
    panel.open('dayCell.save', 'dialog');
    fixture.detectChanges();
    expect(component.surface).toBe('dialog');

    panel.close();
    fixture.detectChanges();
    panel.open();
    fixture.detectChanges();

    expect(component.surface).toBe('page');
  });

  it('says "1 result", not "1 results"', () => {
    panel.open();
    // 'avatar' appears in exactly one entry's prose.
    component.onQueryChange('avatar');
    fixture.detectChanges();

    expect(component.results.length).toBe(1);
    expect(enUSUi.resultCountOne).not.toBe(enUSUi.resultCount);
    expect(countText()).toBe(`1 ${enUSUi.resultCountOne}`);
  });

  it('ends an expanded task with links to the controls it touches', () => {
    // `related` is registry data the panel is the only consumer of; unrendered it
    // is dead weight the integrity spec alone keeps honest.
    panel.open();
    fixture.detectChanges();
    component.toggleEntry('task.registerVacation');
    fixture.detectChanges();

    const links = Array.from(
      fixture.nativeElement.querySelectorAll('.tp-help-entry__related-link'),
    ).map(el => ((el as HTMLElement).textContent ?? '').trim());

    const related = component.related('task.registerVacation');
    expect(related.length).toBeGreaterThan(0);
    expect(links).toEqual(related.map(id => enUS[id].title));
    expect(text()).toContain(enUSUi.relatedControls);
  });

  it('moves the panel to a related control when its link is used', () => {
    panel.open();
    fixture.detectChanges();
    component.toggleEntry('task.registerVacation');
    fixture.detectChanges();

    const first = component.related('task.registerVacation')[0];
    (fixture.nativeElement.querySelector('.tp-help-entry__related-link') as HTMLElement).click();
    fixture.detectChanges();

    expect(component.expanded).toBe(first);
    expect(component.targetId).toBe(first);
    expect(fixture.nativeElement.querySelector('.tp-help-entry--target')).not.toBeNull();
  });

  it('drops a related link to an entry the reader is not allowed to see', () => {
    // A link into an entry the panel does not list would deep-link to a row that
    // is not there. task.exportForPayroll points at the admin-only payroll export.
    component.isAdmin = false;
    expect(component.related('task.exportForPayroll')).not.toContain('toolbar.payrollExport');

    component.isAdmin = true;
    expect(component.related('task.exportForPayroll')).toContain('toolbar.payrollExport');
  });

  it('names the query and offers the tasks when nothing matches', () => {
    panel.open();
    component.onQueryChange('zzzqqq');
    fixture.detectChanges();

    expect(component.isSearching).toBe(true);
    expect(component.isFallback).toBe(true);
    expect(component.results.length).toBeGreaterThan(0);
    expect(component.results.every(result => result.entry.kind === 'task')).toBe(true);
    expect(countText()).toContain('zzzqqq');
    expect(countText()).toContain(enUSUi.noResults);
    expect(countText()).not.toContain(enUSUi.resultCount);
    expect(text()).toContain(enUS['task.registerVacation'].title);
  });

  it('names each result kind for screen readers', () => {
    component.isAdmin = true;
    panel.open();
    component.onQueryChange('payroll');
    fixture.detectChanges();

    const labels = Array.from(fixture.nativeElement.querySelectorAll('.tp-help-entry__kind'))
      .map(el => (el as HTMLElement).getAttribute('aria-label'));
    expect(labels.length).toBe(component.results.length);
    expect(labels).toEqual(component.results.map(result =>
      result.entry.kind === 'task' ? enUSUi.kindTask : enUSUi.kindControl));
    expect(labels).toContain(enUSUi.kindControl);
  });

  it('closes on Escape, and only while open', () => {
    document.dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape' }));
    fixture.detectChanges();
    expect(component.isOpen).toBe(false);

    panel.open();
    fixture.detectChanges();
    expect(panelEl()).not.toBeNull();

    document.dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape' }));
    fixture.detectChanges();

    expect(component.isOpen).toBe(false);
    expect(panelEl()).toBeNull();
  });

  it('swallows Escape before the CDK dispatcher can close the day-cell dialog', () => {
    // CDK's OverlayKeyboardDispatcher listens for keydown on document.body in the
    // bubble phase and routes it to the topmost overlay - which, when help is
    // opened from inside a day cell, is the MatDialog holding unsaved edits. A
    // plain document listener runs after that. Dispatch the event the way a real
    // key press reaches the page (from the focused element, bubbling through body)
    // and assert the body listener never sees it.
    panel.open();
    fixture.detectChanges();

    const dispatcherStandIn = jest.fn();
    document.body.addEventListener('keydown', dispatcherStandIn);
    const event = new KeyboardEvent('keydown', { key: 'Escape', bubbles: true, cancelable: true });
    document.body.dispatchEvent(event);
    document.body.removeEventListener('keydown', dispatcherStandIn);
    fixture.detectChanges();

    expect(dispatcherStandIn).not.toHaveBeenCalled();
    expect(event.defaultPrevented).toBe(true);
    expect(component.isOpen).toBe(false);
    expect(panelEl()).toBeNull();
  });

  it('lets Escape through to the page once the panel has closed', () => {
    // The shield must not outlive the panel, or Escape would stop closing the
    // day-cell dialog at all.
    const dispatcherStandIn = jest.fn();
    document.body.addEventListener('keydown', dispatcherStandIn);
    document.body.dispatchEvent(
      new KeyboardEvent('keydown', { key: 'Escape', bubbles: true, cancelable: true }));
    document.body.removeEventListener('keydown', dispatcherStandIn);

    expect(dispatcherStandIn).toHaveBeenCalledTimes(1);
  });

  it('leaves other keys alone while open', () => {
    panel.open();
    fixture.detectChanges();

    const dispatcherStandIn = jest.fn();
    document.body.addEventListener('keydown', dispatcherStandIn);
    document.body.dispatchEvent(
      new KeyboardEvent('keydown', { key: 'a', bubbles: true, cancelable: true }));
    document.body.removeEventListener('keydown', dispatcherStandIn);
    fixture.detectChanges();

    expect(dispatcherStandIn).toHaveBeenCalledTimes(1);
    expect(component.isOpen).toBe(true);
  });

  it('rebuilds what isAdmin filters when it arrives after opening', () => {
    panel.open();
    component.onQueryChange('payroll');
    fixture.detectChanges();

    expect(browsedIds()).not.toContain('toolbar.payrollExport');
    expect(resultIds()).not.toContain('toolbar.payrollExport');

    component.isAdmin = true;
    component.ngOnChanges({ isAdmin: new SimpleChange(false, true, false) });
    fixture.detectChanges();

    expect(browsedIds()).toContain('toolbar.payrollExport');
    expect(resultIds()).toContain('toolbar.payrollExport');
  });

  it('does not rebuild on the first isAdmin change, or while closed', () => {
    // Both guards, on their false side. ngOnChanges fires once at creation with
    // firstChange true, before the panel has ever opened; rebuilding then would
    // hand *ngFor a fresh array on every open and drop focus and scroll position.
    const rebuild = jest.spyOn(component as any, 'buildSections');

    component.isAdmin = true;
    component.ngOnChanges({ isAdmin: new SimpleChange(undefined, true, true) });
    expect(rebuild).not.toHaveBeenCalled();

    // Not the first change any more, but the panel is closed.
    component.ngOnChanges({ isAdmin: new SimpleChange(true, false, false) });
    expect(rebuild).not.toHaveBeenCalled();

    // A change that is neither of those does rebuild, so the test above is not
    // passing because buildSections is unreachable.
    panel.open();
    fixture.detectChanges();
    rebuild.mockClear();
    component.ngOnChanges({ isAdmin: new SimpleChange(false, true, false) });
    expect(rebuild).toHaveBeenCalled();

    rebuild.mockRestore();
  });

  it('stops listening to the panel service once destroyed', () => {
    fixture.destroy();

    panel.open('flex.sumFlex' as HelpEntryId);

    expect(component.isOpen).toBe(false);
    expect(component.targetId).toBeNull();
  });
});

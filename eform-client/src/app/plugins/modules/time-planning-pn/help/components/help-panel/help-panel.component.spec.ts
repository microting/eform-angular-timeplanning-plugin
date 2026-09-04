import { SimpleChange } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { TranslateService } from '@ngx-translate/core';
import { HelpEntryId } from '../../help.model';
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

  it('closes and asks the host to replay the tour', () => {
    const replays: void[] = [];
    component.replayTourRequested.subscribe(() => replays.push(undefined));
    panel.open();
    fixture.detectChanges();

    (fixture.nativeElement.querySelector('.tp-help-panel__replay button') as HTMLElement).click();
    fixture.detectChanges();

    expect(replays.length).toBe(1);
    expect(panelEl()).toBeNull();
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

  it('stops listening to the panel service once destroyed', () => {
    fixture.destroy();

    panel.open('flex.sumFlex' as HelpEntryId);

    expect(component.isOpen).toBe(false);
    expect(component.targetId).toBeNull();
  });
});

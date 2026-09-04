import { readdirSync, readFileSync } from 'fs';
import { join } from 'path';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { FormsModule } from '@angular/forms';
import { OverlayContainer, OverlayModule } from '@angular/cdk/overlay';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { TranslateService } from '@ngx-translate/core';
import { PLANNING_HELP_ENTRIES } from './planning-help.registry';
import { HelpPanelComponent } from './components/help-panel/help-panel.component';
import { HelpTourComponent } from './components/help-tour/help-tour.component';
import { HelpPanelService } from './services/help-panel.service';
import { HelpTourService } from './services/help-tour.service';
import { applyGridHelpAnchors } from './grid-help-anchors';
import { enUSUi } from './i18n/enUS';

const MODULE_ROOT = join(__dirname, '..');

const CONTAINER_HTML = 'components/plannings/time-plannings-container/time-plannings-container.component.html';
const CONTAINER_TS = 'components/plannings/time-plannings-container/time-plannings-container.component.ts';
const TABLE_HTML = 'components/plannings/time-plannings-table/time-plannings-table.component.html';
const TABLE_TS = 'components/plannings/time-plannings-table/time-plannings-table.component.ts';
const DIALOG_HTML = 'components/plannings/time-planning-actions/workday-entity/workday-entity-dialog.component.html';
const DIALOG_TS = 'components/plannings/time-planning-actions/workday-entity/workday-entity-dialog.component.ts';
const DIALOG_SCSS = 'components/plannings/time-planning-actions/workday-entity/workday-entity-dialog.component.scss';

const read = (relative: string): string => readFileSync(join(MODULE_ROOT, relative), 'utf8');

const MARKUP = [read(CONTAINER_HTML), read(TABLE_HTML), read(DIALOG_HTML)].join('\n');

/**
 * Every literal `'key' | translate` in the three wired templates, frozen as it
 * stood before the help system was mounted. The help chrome takes its labels
 * from HelpUiStrings, so this task added none; a new entry here means someone
 * added a key that has to be translated into all 25 shared locale files.
 */
const TEMPLATE_TRANSLATE_KEYS = [
  'Actual', 'Auto break calculation', 'Cancel', 'CommentOffice', 'CommentWorker', 'Date range',
  'Download Excel', 'Export to payroll', 'Flex', 'Flex balance at start of day',
  'Flex balance to date', 'keyboard_tab', 'keyboard_tab_rtl', 'Needs update!', 'NettoHours',
  'NettoHours override', 'No pay rule set selected', 'PaidOutFlex', 'Pause', 'Plan hours',
  'Planned working hours', 'Reload table', 'Reset pause to recorded', 'Save',
  'Shift not stopped by user!', 'Shifts across midnight', 'Show resigned', 'Start', 'Stop',
  'Tags', 'Total breaktime', 'Total working hours', 'Use 1-minute intervals',
  'View GPS Location', 'View history', 'View Snapshot', 'Worker', 'Worktime start',
  'Worktime stop',
];

describe('help wiring', () => {
  it('anchors every entry that a tour needs', () => {
    const tourEntries = PLANNING_HELP_ENTRIES.filter(entry => entry.tourStep !== undefined);
    // Guards the loop below: an empty registry would make it pass vacuously.
    expect(tourEntries.length).toBeGreaterThan(0);
    for (const entry of tourEntries) {
      expect(MARKUP).toContain(`data-tp-help="${entry.anchor}"`);
    }
  });

  it('only uses helpIds that exist in the registry', () => {
    const known = new Set<string>(PLANNING_HELP_ENTRIES.map(entry => entry.id));
    const used = [...MARKUP.matchAll(/helpId="([^"]+)"/g)].map(match => match[1]);
    expect(used.length).toBeGreaterThan(0);
    for (const id of used) {
      expect(known.has(id)).toBe(true);
    }
  });

  it('only uses anchors that exist in the registry', () => {
    const known = new Set(PLANNING_HELP_ENTRIES.map(entry => entry.anchor).filter(Boolean));
    const used = [...MARKUP.matchAll(/data-tp-help="([^"]+)"/g)].map(match => match[1]);
    expect(used.length).toBeGreaterThan(0);
    for (const anchor of used) {
      expect(known.has(anchor)).toBe(true);
    }
  });

  it('mounts the panel and both tours exactly once', () => {
    expect((MARKUP.match(/<tp-help-panel/g) ?? []).length).toBe(1);
    expect((MARKUP.match(/tour="page"/g) ?? []).length).toBe(1);
    expect((MARKUP.match(/tour="dialog"/g) ?? []).length).toBe(1);
  });

  it('anchors the flex entries on the page, not only in the panel', () => {
    for (const id of ['flex.whatIsFlex', 'flex.sumFlex', 'flex.paidOutFlexRelation']) {
      expect(MARKUP).toContain(`data-tp-help="${id}"`);
    }
  });

  it('starts each tour from a component, since mounting alone does not', () => {
    expect(read(CONTAINER_TS)).toMatch(/start\(\s*'page'/);
    expect(read(DIALOG_TS)).toMatch(/start\(\s*'dialog'/);
  });

  it('ends the dialog tour with abort, never stop, when the dialog goes away', () => {
    // stop() marks the tour seen. Closing a row is not "I have seen the tour".
    const dialogTs = read(DIALOG_TS);
    expect(dialogTs).toMatch(/helpTour\w*\.abort\(\)/);
    expect(dialogTs).not.toMatch(/helpTour\w*\.stop\(\)/);
  });

  it('never uses the translate pipe inside help templates', () => {
    const helpTemplates = [
      'help/components/help-panel/help-panel.component.html',
      'help/components/help-icon/help-icon.component.html',
      'help/components/help-tour/help-tour.component.html',
      'help/components/help-hint/help-hint.component.html',
    ].map(read).join('\n');
    expect(helpTemplates).not.toContain('| translate');
  });

  it('binds the help-icon and panel outputs, or they are inert', () => {
    // Every icon, not just one: an unbound "More in help" is a control that
    // silently does nothing.
    const icons = (MARKUP.match(/<tp-help-icon/g) ?? []).length;
    const bindings = (MARKUP.match(/\(openInPanel\)=/g) ?? []).length;
    expect(icons).toBeGreaterThan(1);
    expect(bindings).toBe(icons);
    expect((MARKUP.match(/<tp-help-panel/g) ?? []).length)
      .toBe((MARKUP.match(/\(replayTourRequested\)=/g) ?? []).length);
  });

  it('does not introduce new translate keys for help chrome', () => {
    // The help chrome must come from HelpUiStrings. This freezes the literal
    // translate keys the three wired templates use: adding `'Help' | translate`,
    // or any other new key, fails here and forces a deliberate update of all 25
    // shared locale files.
    const used = [...MARKUP.matchAll(/'([^']+)'\s*\|\s*translate/g)].map(match => match[1]);
    expect(new Set(used)).toEqual(new Set(TEMPLATE_TRANSLATE_KEYS));
  });

  it('does not add the help chrome to the shared locale catalogue', () => {
    // Wording that could only have come from this feature. Generic labels the
    // plugin already translates ('Close', 'Next') are deliberately not listed.
    const distinctive = [
      enUSUi.searchHelp, enUSUi.moreInHelp, enUSUi.replayTour, enUSUi.noResults,
      enUSUi.sectionTask, enUSUi.sectionDayCell,
    ];
    const localeDir = join(MODULE_ROOT, 'i18n');
    const localeFiles = readdirSync(localeDir).filter(name => name.endsWith('.ts'));
    expect(localeFiles.length).toBeGreaterThan(20);
    for (const name of localeFiles) {
      const contents = readFileSync(join(localeDir, name), 'utf8');
      for (const phrase of distinctive) {
        expect(contents).not.toContain(phrase);
      }
    }
  });

  it('keeps the help-paired form fields at the width they had', () => {
    // These five were direct children of the .d-flex.flex-column column, where a
    // flex item stretches. Rowing them up with their icon shrinks them to
    // mat-form-field's intrinsic width unless the stretch is restored.
    const dialogHtml = read(DIALOG_HTML);
    expect((dialogHtml.match(/class="field-with-help"/g) ?? []).length).toBe(5);

    const scss = read(DIALOG_SCSS);
    const start = scss.indexOf('.field-with-help {');
    expect(start).toBeGreaterThan(-1);
    const block = scss.slice(start, scss.indexOf('\n}', start));
    expect(block).toContain('mat-form-field');
    expect(block).toMatch(/flex:\s*1 1 auto/);
  });

  it('marks the day-cell dialog body scrollable so popovers dismiss on its scroll', () => {
    // ScrollDispatcher only watches containers carrying cdkScrollable. The dialog
    // declares its own overflow container next to Material's mat-dialog-content.
    expect(read(DIALOG_HTML)).toMatch(/class="main-content"[^>]*cdkScrollable|cdkScrollable[^>]*class="main-content"/);
  });

  it('stamps the name-column sort header, which mtx-grid renders itself', () => {
    const root = document.createElement('div');
    root.innerHTML = '<table><tr><th class="mat-column-siteName">Name</th>'
      + '<th class="mat-column-0">Mon</th></tr></table>';

    applyGridHelpAnchors(root);

    expect(root.querySelector('th.mat-column-siteName')?.getAttribute('data-tp-help'))
      .toBe('grid.sortName');
    expect(root.querySelector('th.mat-column-0')?.hasAttribute('data-tp-help')).toBe(false);
  });

  it('calls the header stamp from the table component', () => {
    // Importing it is not calling it: the stamp only lands from a render hook.
    const tableTs = read(TABLE_TS);
    const hook = /ngAfterViewChecked\(\)[\s\S]*?\n  \}/.exec(tableTs);
    expect(hook).not.toBeNull();
    expect((hook as RegExpExecArray)[0]).toContain('applyGridHelpAnchors(this.el.nativeElement)');
  });

  it('opts the panel back into pointer events, since the overlay container opts out', () => {
    const scss = read('help/components/help-panel/help-panel.component.scss');
    const block = scss.slice(0, scss.indexOf('\n}'));
    expect(block).toContain('pointer-events: auto');
  });
});

describe('help deep link and tour scrolling', () => {
  const scrolled: Element[] = [];
  let originalScrollIntoView: unknown;

  beforeAll(() => {
    originalScrollIntoView = (Element.prototype as any).scrollIntoView;
    (Element.prototype as any).scrollIntoView = function (this: Element) {
      scrolled.push(this);
    };
  });

  afterAll(() => {
    (Element.prototype as any).scrollIntoView = originalScrollIntoView;
  });

  beforeEach(() => {
    scrolled.length = 0;
    TestBed.resetTestingModule();
    document.body.querySelectorAll('[data-tp-help]').forEach(element => element.remove());
    localStorage.clear();
  });

  const mountPanel = (): ComponentFixture<HelpPanelComponent> => {
    TestBed.configureTestingModule({
      declarations: [HelpPanelComponent],
      imports: [FormsModule, MatIconModule, MatButtonModule],
      providers: [HelpPanelService, { provide: TranslateService, useValue: { currentLang: 'en-US' } }],
    });
    const fixture = TestBed.createComponent(HelpPanelComponent);
    fixture.detectChanges();
    return fixture;
  };

  it('scrolls the panel to a deep-link target, and only then', () => {
    const fixture = mountPanel();
    const panel = TestBed.inject(HelpPanelService);

    // Browsing the whole catalogue has nothing to scroll to.
    panel.open();
    fixture.detectChanges();
    expect(scrolled).toHaveLength(0);

    // flex.sumFlex sits in the last section, well below the fold of a panel that
    // opens scrolled to the top.
    panel.close();
    fixture.detectChanges();
    panel.open('flex.sumFlex');
    fixture.detectChanges();

    const target = fixture.nativeElement.querySelector('.tp-help-entry--target') as HTMLElement;
    expect(target).not.toBeNull();
    expect(scrolled).toEqual([target]);
  });

  it('parks the open panel inside the CDK overlay container and gives it focus', () => {
    // CDK's Dialog marks body siblings of the overlay container aria-hidden
    // while a modal is open, and the day-cell dialog links into this panel.
    const fixture = mountPanel();
    const host = fixture.nativeElement as HTMLElement;
    const container = TestBed.inject(OverlayContainer).getContainerElement();
    const panel = TestBed.inject(HelpPanelService);

    expect(host.parentNode).not.toBe(container);

    panel.open();
    fixture.detectChanges();

    expect(host.parentNode).toBe(container);
    expect(document.activeElement)
      .toBe(host.querySelector('.tp-help-panel__search input'));

    panel.close();
    fixture.detectChanges();

    expect(host.parentNode).not.toBe(container);
  });

  it('scrolls the current tour anchor into view when a step becomes current', () => {
    TestBed.configureTestingModule({
      declarations: [HelpTourComponent],
      imports: [OverlayModule],
      providers: [{ provide: TranslateService, useValue: { currentLang: 'en-US' } }],
    });
    const anchors = new Map<string, HTMLElement>();
    for (const entry of PLANNING_HELP_ENTRIES.filter(e => e.tour === 'page' && e.tourStep !== undefined)) {
      const element = document.createElement('div');
      element.setAttribute('data-tp-help', entry.anchor as string);
      document.body.appendChild(element);
      anchors.set(entry.anchor as string, element);
    }

    const fixture: ComponentFixture<HelpTourComponent> = TestBed.createComponent(HelpTourComponent);
    fixture.componentInstance.tour = 'page';
    fixture.detectChanges();

    const tour = TestBed.inject(HelpTourService);
    tour.start('page', { isAdmin: true });
    fixture.detectChanges();

    const first = anchors.get('toolbar.dateRange') as HTMLElement;
    expect(scrolled).toContain(first);

    scrolled.length = 0;
    tour.next();
    fixture.detectChanges();

    expect(scrolled).toContain(anchors.get('toolbar.navForward') as HTMLElement);
    expect(scrolled).not.toContain(first);
  });
});

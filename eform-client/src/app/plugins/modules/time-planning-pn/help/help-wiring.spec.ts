import { readdirSync, readFileSync } from 'fs';
import { dirname, join } from 'path';
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
    // And the host must forward what the panel emitted. Hardcoding 'page' here
    // would replay the page tour from inside the day-cell dialog, pointing every
    // step at an anchor behind the dialog backdrop.
    expect(read(CONTAINER_HTML)).toContain('(replayTourRequested)="replayTour($event)"');
  });

  it('places an inline hint at each of the four spots the spec names', () => {
    // Registry entries with no hint on the page are content nobody ever reaches
    // in the situation it was written for.
    const hints = [...MARKUP.matchAll(/<tp-help-hint[\s\S]*?>/g)].map(match => match[0]);
    const hintIds = hints
      .map(hint => /helpId="([^"]+)"/.exec(hint)?.[1])
      .filter((id): id is string => !!id);
    expect(hintIds.sort()).toEqual([
      'dayCell.futureDisabled', 'dayCell.planHoursLimit', 'grid.nameColumn', 'grid.noWorkers',
    ]);
  });

  it('shows the plan-hours hint with the validation error, not always', () => {
    const dialogHtml = read(DIALOG_HTML);
    const hint = /<tp-help-hint[^>]*helpId="dayCell.planHoursLimit"[\s\S]*?><\/tp-help-hint>|<tp-help-hint[\s\S]*?helpId="dayCell.planHoursLimit"[\s\S]*?><\/tp-help-hint>/
      .exec(dialogHtml);
    expect(hint).not.toBeNull();
    expect((hint as RegExpExecArray)[0]).toContain("hasError('tooManyHours')");
    // Beside the error it explains, not somewhere else in the form.
    expect(dialogHtml.indexOf('helpId="dayCell.planHoursLimit"'))
      .toBeGreaterThan(dialogHtml.indexOf('data-testid="planHours-Error"'));
  });

  it('renders the empty-grid hint where the grid renders no rows', () => {
    // mtx-grid swaps noResultTemplate in for the row area when `data` is empty;
    // dropped anywhere else the hint would be a permanent banner.
    const tableHtml = read(TABLE_HTML);
    expect(tableHtml).toContain('[noResultTemplate]="noWorkersTemplate"');
    const template = /<ng-template #noWorkersTemplate>([\s\S]*?)<\/ng-template>/.exec(tableHtml);
    expect(template).not.toBeNull();
    expect((template as RegExpExecArray)[1]).toContain('helpId="grid.noWorkers"');
  });

  it('puts the name-column hint above the grid, not below the whole table', () => {
    // Below </mtx-grid> it reads as a footnote on the table rather than as
    // something about the column it describes.
    const tableHtml = read(TABLE_HTML);
    expect(tableHtml.indexOf('helpId="grid.nameColumn"'))
      .toBeLessThan(tableHtml.indexOf('<mtx-grid'));
  });

  it('gives the help button the same markup as its toolbar siblings', () => {
    const containerHtml = read(CONTAINER_HTML);
    const button = /<button[^>]*id="planningHelp"[\s\S]*?>/.exec(containerHtml);
    expect(button).not.toBeNull();
    // mat-icon-button sizes from Material's state-layer variables and fights the
    // shared class the other five toolbar buttons use on their own.
    expect((button as RegExpExecArray)[0]).not.toContain('mat-icon-button');
    expect((button as RegExpExecArray)[0])
      .toContain('class="btn-secondary btn-secondary--icon-rounded-border"');
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

  it('warns instead of stopping silently if the registry drops the sortName anchor', () => {
    jest.isolateModules(() => {
      jest.doMock('./planning-help.registry', () => ({ PLANNING_HELP_ENTRIES: [] }));
      const warn = jest.spyOn(console, 'warn').mockImplementation(() => undefined);
      // eslint-disable-next-line @typescript-eslint/no-var-requires
      const { applyGridHelpAnchors: stamp } = require('./grid-help-anchors');

      const root = document.createElement('div');
      const header = document.createElement('th');
      header.className = 'mat-column-siteName';
      root.appendChild(header);

      stamp(root);

      expect(header.hasAttribute('data-tp-help')).toBe(false);
      expect(warn).toHaveBeenCalledWith(expect.stringContaining('grid.sortName'));
      warn.mockRestore();
    });
    jest.dontMock('./planning-help.registry');
  });

  it('calls the header stamp from the table component', () => {
    // Importing it is not calling it: the stamp only lands from a render hook.
    const tableTs = read(TABLE_TS);
    const hook = /ngAfterViewChecked\(\)[\s\S]*?\n  \}/.exec(tableTs);
    expect(hook).not.toBeNull();
    expect((hook as RegExpExecArray)[0]).toContain('applyGridHelpAnchors(this.el.nativeElement)');
  });

  it('raises the panel above the CDK overlay layer it is parked in', () => {
    // .cdk-overlay-container is a stacking context, and inside it CDK puts the
    // backdrop, the global wrapper and every pane on one z-index. Below that the
    // panel paints under the day-cell dialog's backdrop, is dimmed by it and
    // loses hit-testing to it — a click on the panel would reach the backdrop
    // and close the dialog. The CDK value is read, not assumed.
    const cdkCss = readFileSync(
      join(dirname(require.resolve('@angular/cdk/package.json')), 'overlay-prebuilt.css'), 'utf8');
    const cdkLayers = [...cdkCss.matchAll(
      /\.cdk-(?:overlay-backdrop|overlay-pane|global-overlay-wrapper)\{[^}]*?z-index:\s*(\d+)/g)]
      .map(match => Number(match[1]));
    expect(cdkLayers.length).toBeGreaterThan(0);

    const panelScss = read('help/components/help-panel/help-panel.component.scss');
    const block = panelScss.slice(0, panelScss.indexOf('\n}'));
    const declared = /z-index:\s*(\d+)/.exec(block);
    expect(declared).not.toBeNull();

    expect(Number((declared as RegExpExecArray)[1])).toBeGreaterThan(Math.max(...cdkLayers));
    // .cdk-overlay-container is pointer-events: none; panes opt back in one by one.
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

  it('puts the host back where it was, not merely back in the parent', () => {
    // Appending on close walks the panel down past its siblings — in the real
    // template, past <tp-help-tour> — a little further on every open/close.
    const fixture = mountPanel();
    const host = fixture.nativeElement as HTMLElement;
    const marker = document.createElement('div');
    (host.parentNode as Node).appendChild(marker);
    const panel = TestBed.inject(HelpPanelService);

    for (let cycle = 0; cycle < 2; cycle++) {
      panel.open();
      fixture.detectChanges();
      panel.close();
      fixture.detectChanges();
    }

    expect(host.nextSibling).toBe(marker);
  });

  it('takes focus when it opens, but not when an already-open panel is deep-linked', () => {
    const fixture = mountPanel();
    const host = fixture.nativeElement as HTMLElement;
    const trigger = document.createElement('button');
    document.body.appendChild(trigger);
    trigger.focus();
    const panel = TestBed.inject(HelpPanelService);

    panel.open();
    fixture.detectChanges();
    const search = host.querySelector('.tp-help-panel__search input') as HTMLElement;
    expect(document.activeElement).toBe(search);

    // The planner clicks into the list; a second "More in help" must not yank
    // focus back to the search box.
    const entryHead = host.querySelector('.tp-help-entry__head') as HTMLElement;
    entryHead.focus();
    panel.open('flex.sumFlex');
    fixture.detectChanges();
    expect(document.activeElement).toBe(entryHead);

    // Closing hands focus back to whatever opened it.
    panel.close();
    fixture.detectChanges();
    expect(document.activeElement).toBe(trigger);

    trigger.remove();
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

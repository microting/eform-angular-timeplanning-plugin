import { readFileSync } from 'fs';
import { join } from 'path';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { FormsModule } from '@angular/forms';
import { OverlayModule } from '@angular/cdk/overlay';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { TranslateService } from '@ngx-translate/core';
import { PLANNING_HELP_ENTRIES } from './planning-help.registry';
import { HelpPanelComponent } from './components/help-panel/help-panel.component';
import { HelpTourComponent } from './components/help-tour/help-tour.component';
import { HelpPanelService } from './services/help-panel.service';
import { HelpTourService } from './services/help-tour.service';
import { applyGridHelpAnchors } from './grid-help-anchors';

const MODULE_ROOT = join(__dirname, '..');

const CONTAINER_HTML = 'components/plannings/time-plannings-container/time-plannings-container.component.html';
const CONTAINER_TS = 'components/plannings/time-plannings-container/time-plannings-container.component.ts';
const TABLE_HTML = 'components/plannings/time-plannings-table/time-plannings-table.component.html';
const TABLE_TS = 'components/plannings/time-plannings-table/time-plannings-table.component.ts';
const DIALOG_HTML = 'components/plannings/time-planning-actions/workday-entity/workday-entity-dialog.component.html';
const DIALOG_TS = 'components/plannings/time-planning-actions/workday-entity/workday-entity-dialog.component.ts';

const read = (relative: string): string => readFileSync(join(MODULE_ROOT, relative), 'utf8');

const MARKUP = [read(CONTAINER_HTML), read(TABLE_HTML), read(DIALOG_HTML)].join('\n');

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
    expect(MARKUP).toContain('(openInPanel)=');
    expect(MARKUP).toContain('(replayTourRequested)=');
  });

  it('does not introduce new translate keys for help chrome', () => {
    // The help button's tooltip must come from HelpUiStrings, not a new shared key.
    expect(MARKUP).not.toContain("'Help' | translate");
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
    expect(read(TABLE_TS)).toContain('applyGridHelpAnchors');
  });

  it('stacks the panel above the CDK overlay container', () => {
    // The day-cell dialog is modal and its popovers link into the panel. Below
    // .cdk-overlay-container (z-index 1000) that link opens the panel behind the
    // dialog backdrop, which reads as a control that did nothing.
    const scss = read('help/components/help-panel/help-panel.component.scss');
    const zIndex = /\.tp-help-panel\s*\{[^}]*?z-index:\s*(\d+)/.exec(scss);
    expect(zIndex).not.toBeNull();
    expect(Number((zIndex as RegExpExecArray)[1])).toBeGreaterThan(1000);
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

  it('scrolls the panel to the entry a deep link targets', () => {
    TestBed.configureTestingModule({
      declarations: [HelpPanelComponent],
      imports: [FormsModule, MatIconModule, MatButtonModule],
      providers: [HelpPanelService, { provide: TranslateService, useValue: { currentLang: 'en-US' } }],
    });
    const fixture: ComponentFixture<HelpPanelComponent> = TestBed.createComponent(HelpPanelComponent);
    fixture.detectChanges();

    // flex.sumFlex sits in the last section, well below the fold of a panel that
    // opens scrolled to the top.
    TestBed.inject(HelpPanelService).open('flex.sumFlex');
    fixture.detectChanges();

    const target = fixture.nativeElement.querySelector('.tp-help-entry--target') as HTMLElement;
    expect(target).not.toBeNull();
    expect(scrolled).toContain(target);
  });

  it('does not scroll the panel when it is opened without a target', () => {
    TestBed.configureTestingModule({
      declarations: [HelpPanelComponent],
      imports: [FormsModule, MatIconModule, MatButtonModule],
      providers: [HelpPanelService, { provide: TranslateService, useValue: { currentLang: 'en-US' } }],
    });
    const fixture: ComponentFixture<HelpPanelComponent> = TestBed.createComponent(HelpPanelComponent);
    fixture.detectChanges();

    TestBed.inject(HelpPanelService).open();
    fixture.detectChanges();

    expect(scrolled).toHaveLength(0);
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

import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ElementRef } from '@angular/core';
import { OverlayModule } from '@angular/cdk/overlay';
import { TranslateService } from '@ngx-translate/core';
import { HelpTourComponent } from './help-tour.component';
import { HelpTourService } from '../../services/help-tour.service';
import { HelpTourName } from '../../help.model';
import { enUS, enUSUi } from '../../i18n/enUS';

describe('HelpTourComponent', () => {
  const anchor = (id: string): HTMLElement => {
    const element = document.createElement('div');
    element.setAttribute('data-tp-help', id);
    document.body.appendChild(element);
    return element;
  };

  const mount = (tour: HelpTourName = 'page'): ComponentFixture<HelpTourComponent> => {
    const fixture = TestBed.createComponent(HelpTourComponent);
    fixture.componentInstance.tour = tour;
    fixture.detectChanges();
    return fixture;
  };

  const cards = (): HTMLElement[] =>
    Array.from(document.querySelectorAll<HTMLElement>('.tp-help-tour'));
  const card = (): HTMLElement | null => cards()[0] ?? null;

  beforeEach(() => {
    TestBed.resetTestingModule();
    document.body.querySelectorAll('[data-tp-help]').forEach(element => element.remove());
    localStorage.clear();
    TestBed.configureTestingModule({
      declarations: [HelpTourComponent],
      imports: [OverlayModule],
      providers: [{ provide: TranslateService, useValue: { currentLang: 'en-US' } }],
    });
  });

  it('does not mark the tour seen just by being mounted', () => {
    // state$ is a BehaviorSubject seeded null, so the subscription fires once at
    // mount with a null state. Marking "seen" there would permanently suppress
    // the automatic first run for every genuine first-time user.
    mount();
    expect(TestBed.inject(HelpTourService).hasSeen('page')).toBe(false);
    expect(localStorage.getItem('tp.planning.tour.v1')).toBeNull();
  });

  it('shows no card while no tour is running', () => {
    const fixture = mount();
    expect(fixture.componentInstance.state).toBeNull();
    expect(fixture.componentInstance.origin).toBeNull();
    expect(card()).toBeNull();
  });

  it('renders only in the instance whose tour is running', () => {
    // The service is a singleton and Task 9 mounts this component twice: once on
    // the page, once inside the day-cell dialog. Without a per-instance filter
    // both would render the same card at the same time.
    anchor('toolbar.dateRange');
    anchor('dayCell.plannedTimes');
    const pageTour = mount('page');
    const dialogTour = mount('dialog');

    TestBed.inject(HelpTourService).start('dialog', { isAdmin: false });
    pageTour.detectChanges();
    dialogTour.detectChanges();

    expect(dialogTour.componentInstance.state?.entry.id).toBe('dayCell.plannedTimes');
    expect(pageTour.componentInstance.state).toBeNull();
    expect(pageTour.componentInstance.origin).toBeNull();
    expect(cards().length).toBe(1);
    expect(card()!.querySelector('h5')!.textContent!.trim())
      .toBe(enUS['dayCell.plannedTimes'].title);
  });

  it('renders the running step against its anchor', () => {
    const element = anchor('toolbar.dateRange');
    TestBed.inject(HelpTourService).start('page', { isAdmin: false });
    const fixture = mount();

    expect(fixture.componentInstance.state?.entry.id).toBe('toolbar.dateRange');
    expect(fixture.componentInstance.origin).toBeInstanceOf(ElementRef);
    expect(fixture.componentInstance.origin?.nativeElement).toBe(element);

    const rendered = card();
    expect(rendered).not.toBeNull();
    expect(rendered!.querySelector('h5')!.textContent!.trim())
      .toBe(enUS['toolbar.dateRange'].title);
    expect(rendered!.querySelector('.tp-help-tour__body')!.textContent!.trim())
      .toBe(enUS['toolbar.dateRange'].short);
  });

  it('names the dialog for screen readers without claiming to be modal', () => {
    anchor('toolbar.dateRange');
    TestBed.inject(HelpTourService).start('page', { isAdmin: false });
    mount();

    const rendered = card()!;
    expect(rendered.getAttribute('role')).toBe('dialog');
    expect(rendered.getAttribute('aria-modal')).toBe('false');
    const labelledBy = rendered.getAttribute('aria-labelledby');
    expect(labelledBy).toBeTruthy();
    expect(rendered.querySelector('h5')!.id).toBe(labelledBy);
  });

  it('gives two mounted instances distinct label ids', () => {
    expect(mount('page').componentInstance.titleId)
      .not.toBe(mount('dialog').componentInstance.titleId);
  });

  it('moves focus to Next so the card is reachable from the keyboard', () => {
    // The overlay is appended at the end of <body>, nowhere near the anchor in
    // tab order, so without this the buttons are effectively unreachable.
    anchor('toolbar.dateRange');
    TestBed.inject(HelpTourService).start('page', { isAdmin: false });
    mount();

    expect(document.activeElement).toBe(card()!.querySelector('.tp-help-tour__next'));
  });

  it('shows the step counter as one-based', () => {
    anchor('toolbar.dateRange');
    anchor('grid.openDay');
    TestBed.inject(HelpTourService).start('page', { isAdmin: false });
    const fixture = mount();

    expect(card()!.querySelector('.tp-help-tour__step')!.textContent!.replace(/\s+/g, ' ').trim())
      .toBe('1 / 2');

    fixture.componentInstance.next();
    fixture.detectChanges();

    expect(card()!.querySelector('.tp-help-tour__step')!.textContent!.replace(/\s+/g, ' ').trim())
      .toBe('2 / 2');
  });

  it('labels its buttons from HelpUiStrings, never the shared translate catalogue', () => {
    anchor('toolbar.dateRange');
    TestBed.inject(HelpTourService).start('page', { isAdmin: false });
    mount();

    expect(card()!.querySelector('.tp-help-tour__skip')!.textContent!.trim()).toBe(enUSUi.skip);
    expect(card()!.querySelector('.tp-help-tour__next')!.textContent!.trim()).toBe(enUSUi.next);
  });

  it('advances to the next step when Next is clicked', () => {
    anchor('toolbar.dateRange');
    const second = anchor('grid.openDay');
    TestBed.inject(HelpTourService).start('page', { isAdmin: false });
    const fixture = mount();

    (card()!.querySelector('.tp-help-tour__next') as HTMLButtonElement).click();
    fixture.detectChanges();

    expect(fixture.componentInstance.state?.entry.id).toBe('grid.openDay');
    expect(fixture.componentInstance.origin?.nativeElement).toBe(second);
  });

  it('closes and marks the tour seen when Skip is clicked', () => {
    anchor('toolbar.dateRange');
    TestBed.inject(HelpTourService).start('page', { isAdmin: false });
    const fixture = mount();

    (card()!.querySelector('.tp-help-tour__skip') as HTMLButtonElement).click();
    fixture.detectChanges();

    expect(fixture.componentInstance.state).toBeNull();
    expect(card()).toBeNull();
    expect(TestBed.inject(HelpTourService).hasSeen('page')).toBe(true);
  });

  it('skips the tour on Escape, and ignores other keys', () => {
    anchor('toolbar.dateRange');
    TestBed.inject(HelpTourService).start('page', { isAdmin: false });
    const fixture = mount();

    // Dispatched as a real event, not called on the instance: CDK's keyboard
    // dispatcher listens on document.body and routes to the top overlay, so this
    // proves the (overlayKeydown) binding exists, not just the handler method.
    const press = (key: string) =>
      document.body.dispatchEvent(new KeyboardEvent('keydown', { key, bubbles: true }));

    press('a');
    fixture.detectChanges();
    expect(fixture.componentInstance.state).not.toBeNull();

    press('Escape');
    fixture.detectChanges();

    expect(fixture.componentInstance.state).toBeNull();
    expect(card()).toBeNull();
    expect(TestBed.inject(HelpTourService).hasSeen('page')).toBe(true);
  });

  it('ends the tour when the current step\'s anchor leaves the DOM', () => {
    // Anchors are only validated at start(). If the dialog closes mid-tour the
    // overlay would go away while the tour stayed "running" — no card, no Skip,
    // and Task 9's "start if not seen" logic would see a live tour forever.
    const element = anchor('toolbar.dateRange');
    anchor('grid.openDay');
    const tour = TestBed.inject(HelpTourService);
    tour.start('page', { isAdmin: false });
    const fixture = mount();
    expect(card()).not.toBeNull();

    element.remove();
    fixture.detectChanges();

    expect(fixture.componentInstance.state).toBeNull();
    expect(fixture.componentInstance.origin).toBeNull();
    expect(card()).toBeNull();
    expect(tour.isRunning).toBe(false);
  });

  it('leaves an anchor-loss end unseen and still offerable, unlike a user skip', () => {
    // The dialog tour starts the instant the day-cell dialog opens, so a planner
    // who opens a row, glances and closes it may have seen one step of six.
    // Closing a dialog means "done with this row", not "done learning".
    const tour = TestBed.inject(HelpTourService);
    let element = anchor('dayCell.plannedTimes');
    tour.start('dialog', { isAdmin: false });
    const fixture = mount('dialog');
    expect(card()).not.toBeNull();

    element.remove();
    fixture.detectChanges();

    expect(tour.isRunning).toBe(false);
    expect(tour.hasSeen('dialog')).toBe(false);

    // Still offerable: the same tour runs again and renders.
    element = anchor('dayCell.plannedTimes');
    tour.start('dialog', { isAdmin: false });
    fixture.detectChanges();
    expect(card()).not.toBeNull();

    // The other direction: the user ending it does count.
    (card()!.querySelector('.tp-help-tour__skip') as HTMLButtonElement).click();
    fixture.detectChanges();

    expect(card()).toBeNull();
    expect(tour.hasSeen('dialog')).toBe(true);
  });

  it('re-points at a replaced anchor node rather than a detached one', () => {
    const element = anchor('toolbar.dateRange');
    TestBed.inject(HelpTourService).start('page', { isAdmin: false });
    const fixture = mount();

    element.remove();
    const replacement = anchor('toolbar.dateRange');
    fixture.detectChanges();

    expect(fixture.componentInstance.origin?.nativeElement).toBe(replacement);
    expect(card()).not.toBeNull();
  });

  it('closes when the last step is passed', () => {
    anchor('toolbar.dateRange');
    TestBed.inject(HelpTourService).start('page', { isAdmin: false });
    const fixture = mount();

    fixture.componentInstance.next();
    fixture.detectChanges();

    expect(fixture.componentInstance.state).toBeNull();
    expect(fixture.componentInstance.origin).toBeNull();
    expect(card()).toBeNull();
  });

  it('stops reflecting the tour once destroyed', () => {
    anchor('toolbar.dateRange');
    const tour = TestBed.inject(HelpTourService);
    const fixture = mount();
    fixture.destroy();

    tour.start('page', { isAdmin: false });

    expect(fixture.componentInstance.state).toBeNull();
  });
});

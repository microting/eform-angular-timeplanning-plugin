import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ElementRef } from '@angular/core';
import { OverlayModule } from '@angular/cdk/overlay';
import { TranslateService } from '@ngx-translate/core';
import { HelpTourComponent } from './help-tour.component';
import { HelpTourService } from '../../services/help-tour.service';
import { enUS, enUSUi } from '../../i18n/enUS';

describe('HelpTourComponent', () => {
  const anchor = (id: string): HTMLElement => {
    const element = document.createElement('div');
    element.setAttribute('data-tp-help', id);
    document.body.appendChild(element);
    return element;
  };

  const mount = (): ComponentFixture<HelpTourComponent> => {
    const fixture = TestBed.createComponent(HelpTourComponent);
    fixture.componentInstance.tour = 'page';
    fixture.detectChanges();
    return fixture;
  };

  const card = (): HTMLElement | null => document.querySelector('.tp-help-tour');

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

  it('renders the running step against its anchor', () => {
    const element = anchor('toolbar.dateRange');
    TestBed.inject(HelpTourService).start('page', { isAdmin: false });
    const fixture = mount();

    expect(fixture.componentInstance.state?.entry.id).toBe('toolbar.dateRange');
    expect(fixture.componentInstance.origin).toBeInstanceOf(ElementRef);
    expect(fixture.componentInstance.origin?.nativeElement).toBe(element);

    const rendered = card();
    expect(rendered).not.toBeNull();
    expect(rendered!.getAttribute('role')).toBe('dialog');
    expect(rendered!.querySelector('h5')!.textContent!.trim())
      .toBe(enUS['toolbar.dateRange'].title);
    expect(rendered!.querySelector('.tp-help-tour__body')!.textContent!.trim())
      .toBe(enUS['toolbar.dateRange'].short);
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

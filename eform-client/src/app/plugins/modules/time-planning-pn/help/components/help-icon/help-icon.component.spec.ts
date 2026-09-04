import { ComponentFixture, TestBed } from '@angular/core/testing';
import { CloseScrollStrategy, OverlayModule } from '@angular/cdk/overlay';
import { NoopAnimationsModule } from '@angular/platform-browser/animations';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';
import { TranslateService } from '@ngx-translate/core';
import { HelpIconComponent } from './help-icon.component';
import { enUS } from '../../i18n/enUS';

describe('HelpIconComponent', () => {
  let fixture: ComponentFixture<HelpIconComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      declarations: [HelpIconComponent],
      imports: [OverlayModule, NoopAnimationsModule, MatIconModule, MatButtonModule],
      providers: [{ provide: TranslateService, useValue: { currentLang: 'en-US' } }],
    }).compileComponents();

    fixture = TestBed.createComponent(HelpIconComponent);
    fixture.componentInstance.helpId = 'toolbar.dateRange';
    fixture.detectChanges();
  });

  it('labels the button with the entry title', () => {
    const button: HTMLButtonElement = fixture.nativeElement.querySelector('button');
    expect(button.getAttribute('aria-label')).toBe(enUS['toolbar.dateRange'].title);
  });

  it('starts closed and opens on click', () => {
    expect(fixture.componentInstance.isOpen).toBe(false);
    fixture.nativeElement.querySelector('button').click();
    fixture.detectChanges();
    expect(fixture.componentInstance.isOpen).toBe(true);
  });

  it('closes on Escape', () => {
    fixture.componentInstance.isOpen = true;
    fixture.componentInstance.onOverlayKeydown(new KeyboardEvent('keydown', { key: 'Escape' }));
    expect(fixture.componentInstance.isOpen).toBe(false);
  });

  it('ignores other keys', () => {
    fixture.componentInstance.isOpen = true;
    fixture.componentInstance.onOverlayKeydown(new KeyboardEvent('keydown', { key: 'a' }));
    expect(fixture.componentInstance.isOpen).toBe(true);
  });

  it('emits the id when More is used, and closes', () => {
    const seen: string[] = [];
    fixture.componentInstance.openInPanel.subscribe(id => seen.push(id));
    fixture.componentInstance.isOpen = true;
    fixture.componentInstance.onMore();
    expect(seen).toEqual(['toolbar.dateRange']);
    expect(fixture.componentInstance.isOpen).toBe(false);
  });

  it('closes when a click lands outside the popover, without a backdrop to swallow it', () => {
    fixture.componentInstance.isOpen = true;
    fixture.detectChanges();

    // No backdrop means the same click that dismisses the popover reaches the
    // control underneath - on this page every day cell is a click target.
    expect(document.querySelector('.cdk-overlay-backdrop')).toBeNull();

    const outside = document.createElement('button');
    document.body.appendChild(outside);
    outside.dispatchEvent(new MouseEvent('click', { bubbles: true }));
    fixture.detectChanges();
    outside.remove();

    expect(fixture.componentInstance.isOpen).toBe(false);
  });

  it('stays open when the click lands inside the popover', () => {
    fixture.componentInstance.isOpen = true;
    fixture.detectChanges();

    const body = document.querySelector('.tp-help-popover__body') as HTMLElement;
    body.dispatchEvent(new MouseEvent('click', { bubbles: true }));
    fixture.detectChanges();

    expect(fixture.componentInstance.isOpen).toBe(true);
  });

  it('describes the popover as a note, not a modal dialog it does not implement', () => {
    fixture.componentInstance.isOpen = true;
    fixture.detectChanges();

    const popover = document.querySelector('.tp-help-popover');
    expect(popover).not.toBeNull();
    expect(popover.getAttribute('role')).toBe('note');
    expect(popover.getAttribute('aria-label')).toBe(enUS['toolbar.dateRange'].title);
  });

  it('binds a close-on-scroll strategy, not the injected reposition default', () => {
    // CdkConnectedOverlay's injected default is createRepositionScrollStrategy,
    // which would leave the popover glued to a trigger scrolled out of view.
    expect(fixture.componentInstance.scrollStrategy).toBeInstanceOf(CloseScrollStrategy);
  });

  it('dismisses on scroll', () => {
    fixture.componentInstance.isOpen = true;
    fixture.detectChanges();
    expect(document.querySelector('.tp-help-popover')).not.toBeNull();

    // ScrollDispatcher listens for 'scroll' on the document and pushes through
    // its scrolled() stream, which the close strategy is subscribed to.
    document.dispatchEvent(new Event('scroll'));
    fixture.detectChanges();

    expect(fixture.componentInstance.isOpen).toBe(false);
    expect(document.querySelector('.tp-help-popover')).toBeNull();
  });

  it('renders nothing for an unknown id rather than throwing', () => {
    const other = TestBed.createComponent(HelpIconComponent);
    other.componentInstance.helpId = 'nope' as never;
    expect(() => other.detectChanges()).not.toThrow();
    expect(other.nativeElement.querySelector('button')).toBeNull();
  });
});

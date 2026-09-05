import { ComponentFixture, TestBed } from '@angular/core/testing';
import { CloseScrollStrategy, OverlayModule } from '@angular/cdk/overlay';
import { NoopAnimationsModule } from '@angular/platform-browser/animations';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';
import { TranslateService } from '@ngx-translate/core';
import { HelpIconComponent } from './help-icon.component';
import { of } from 'rxjs';
import { enUS } from '../../i18n/enUS';
import { HelpVisibilityService } from '../../services/help-visibility.service';

/**
 * The one dependency the help chrome gained when help became admin-only. A stub
 * rather than a mock store: HelpVisibilityService is the only thing the chrome
 * asks, so these specs do not need ngrx at all. It defaults to visible, so every
 * assertion below still covers the admin case it was written for.
 */
const helpVisibility = { isVisible: true, isVisible$: of(true) };
const provideHelpVisibility = { provide: HelpVisibilityService, useValue: helpVisibility };


describe('HelpIconComponent', () => {
  let fixture: ComponentFixture<HelpIconComponent>;

  beforeEach(async () => {
    // The stub is shared by every case here; the gate tests flip it.
    helpVisibility.isVisible = true;
    await TestBed.configureTestingModule({
      declarations: [HelpIconComponent],
      imports: [OverlayModule, NoopAnimationsModule, MatIconModule, MatButtonModule],
      providers: [
        { provide: TranslateService, useValue: { currentLang: 'en-US' } },
        provideHelpVisibility,
      ],
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

  // These three drive real events through the template bindings rather than
  // calling the handlers. Calling onOverlayKeydown() or onMore() directly proves
  // only that the methods work: delete (overlayKeydown) or (click)="onMore()"
  // from the template and such tests stay green while the control goes inert.
  const press = (key: string) =>
    // CDK's OverlayKeyboardDispatcher listens on document.body and routes to the
    // topmost open overlay, which is what (overlayKeydown) is fed from.
    document.body.dispatchEvent(new KeyboardEvent('keydown', { key, bubbles: true }));

  it('closes on Escape', () => {
    fixture.componentInstance.isOpen = true;
    fixture.detectChanges();
    expect(document.querySelector('.tp-help-popover')).not.toBeNull();

    press('Escape');
    fixture.detectChanges();

    expect(fixture.componentInstance.isOpen).toBe(false);
    expect(document.querySelector('.tp-help-popover')).toBeNull();
  });

  it('ignores other keys', () => {
    fixture.componentInstance.isOpen = true;
    fixture.detectChanges();

    press('a');
    fixture.detectChanges();

    expect(fixture.componentInstance.isOpen).toBe(true);
    expect(document.querySelector('.tp-help-popover')).not.toBeNull();
  });

  it('emits the id when More is used, and closes', () => {
    const seen: string[] = [];
    fixture.componentInstance.openInPanel.subscribe(id => seen.push(id));
    fixture.componentInstance.isOpen = true;
    fixture.detectChanges();

    const more = document.querySelector('.tp-help-popover__more') as HTMLButtonElement;
    expect(more).not.toBeNull();
    more.click();
    fixture.detectChanges();

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

  it('renders no icon and no popover at all when help is not visible', () => {
    // The gate lives in HelpEntryChromeBase.prose, which is what the whole
    // template hangs off, so a non-admin gets no trigger and — even if isOpen is
    // forced — no popover either.
    helpVisibility.isVisible = false;
    fixture.componentInstance.isOpen = true;
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('button')).toBeNull();
    expect(document.querySelector('.tp-help-popover')).toBeNull();

    // And an admin still gets all of it. Without this half, the assertions above
    // would pass just as well if the component had been deleted.
    helpVisibility.isVisible = true;
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('button')).not.toBeNull();
    expect(document.querySelector('.tp-help-popover')).not.toBeNull();
  });
});

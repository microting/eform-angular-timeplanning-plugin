import { ComponentFixture, TestBed } from '@angular/core/testing';
import { OverlayModule } from '@angular/cdk/overlay';
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

  it('renders nothing for an unknown id rather than throwing', () => {
    const other = TestBed.createComponent(HelpIconComponent);
    other.componentInstance.helpId = 'nope' as never;
    expect(() => other.detectChanges()).not.toThrow();
    expect(other.nativeElement.querySelector('button')).toBeNull();
  });
});

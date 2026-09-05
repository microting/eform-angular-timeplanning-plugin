import { ComponentFixture, TestBed } from '@angular/core/testing';
import { MatIconModule } from '@angular/material/icon';
import { TranslateService } from '@ngx-translate/core';
import { HelpHintComponent } from './help-hint.component';
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


describe('HelpHintComponent', () => {
  let fixture: ComponentFixture<HelpHintComponent>;

  beforeEach(async () => {
    // The stub is shared by every case here; the gate tests flip it.
    helpVisibility.isVisible = true;
    await TestBed.configureTestingModule({
      declarations: [HelpHintComponent],
      imports: [MatIconModule],
      providers: [
        { provide: TranslateService, useValue: { currentLang: 'en-US' } },
        provideHelpVisibility,
      ],
    }).compileComponents();
    fixture = TestBed.createComponent(HelpHintComponent);
  });

  it('renders the entry short text', () => {
    fixture.componentInstance.helpId = 'dayCell.futureDisabled';
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain(enUS['dayCell.futureDisabled'].short);
  });

  it('uses the info tone by default and warn when asked', () => {
    fixture.componentInstance.helpId = 'dayCell.futureDisabled';
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('.help-text')?.classList).not.toContain('help-text--warn');

    fixture.componentInstance.tone = 'warn';
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('.help-text')?.classList).toContain('help-text--warn');
  });

  it('renders nothing for an unknown id', () => {
    fixture.componentInstance.helpId = 'nope' as never;
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('.help-text')).toBeNull();
  });

  it('renders the correct icon based on tone', () => {
    fixture.componentInstance.helpId = 'dayCell.futureDisabled';
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('.mat-icon').textContent).toContain('info');

    fixture.componentInstance.tone = 'warn';
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('.mat-icon').textContent).toContain('warning');
  });

  it('renders nothing when help is not visible, and the hint again when it is', () => {
    fixture.componentInstance.helpId = 'dayCell.futureDisabled';
    helpVisibility.isVisible = false;
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('.help-text')).toBeNull();

    // The admin half: otherwise the assertion above is indistinguishable from
    // the component rendering nothing under any circumstances.
    helpVisibility.isVisible = true;
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('.help-text')).not.toBeNull();
    expect(fixture.nativeElement.textContent).toContain(enUS['dayCell.futureDisabled'].short);
  });
});

import { ComponentFixture, TestBed } from '@angular/core/testing';
import { MatIconModule } from '@angular/material/icon';
import { TranslateService } from '@ngx-translate/core';
import { HelpHintComponent } from './help-hint.component';
import { enUS } from '../../i18n/enUS';

describe('HelpHintComponent', () => {
  let fixture: ComponentFixture<HelpHintComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      declarations: [HelpHintComponent],
      imports: [MatIconModule],
      providers: [{ provide: TranslateService, useValue: { currentLang: 'en-US' } }],
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
});

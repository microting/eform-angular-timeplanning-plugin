import { TestBed } from '@angular/core/testing';
import { TranslateService } from '@ngx-translate/core';
import { HelpContentService } from './help-content.service';
import { enUS, enUSUi } from '../i18n/enUS';
import { da, daUi } from '../i18n/da';
import { HELP_LOCALES } from '../i18n';
import { HelpProseMap } from '../help.model';

describe('HelpContentService', () => {
  let translate: { currentLang: string };

  const make = (lang: string) => {
    translate = { currentLang: lang };
    TestBed.resetTestingModule();
    TestBed.configureTestingModule({
      providers: [
        HelpContentService,
        { provide: TranslateService, useValue: translate },
      ],
    });
    return TestBed.inject(HelpContentService);
  };

  // These compare against the content file rather than a hard-coded string, so a
  // copywriting choice made later in Task 1 cannot fail a resolution test.
  it('returns English prose for an English locale', () => {
    const service = make('en-US');
    expect(service.prose('toolbar.dateRange')).toEqual(enUS['toolbar.dateRange']);
  });

  it('falls back to English for a locale with no prose file', () => {
    const service = make('de-DE');
    expect(service.prose('toolbar.dateRange')).toEqual(enUS['toolbar.dateRange']);
  });

  it('resolves a bare language code to its locale file', () => {
    const service = make('da');
    expect(service.prose('toolbar.dateRange')).toEqual(da['toolbar.dateRange']);
  });

  // prose() falls back to English one entry at a time, not one locale at a time.
  // A locale map that is present but missing a single id must still serve its own
  // language for every other id.
  it('falls back to English only for the id a locale map is missing', () => {
    const partial: Partial<HelpProseMap> = { ...da };
    delete partial['toolbar.dateRange'];
    HELP_LOCALES['da-partial'] = partial;
    try {
      const service = make('da-partial');
      expect(service.prose('toolbar.dateRange')).toEqual(enUS['toolbar.dateRange']);
      expect(service.prose('toolbar.reload')).toEqual(da['toolbar.reload']);
      expect(service.prose('dayCell.flags')).toEqual(da['dayCell.flags']);
    } finally {
      delete HELP_LOCALES['da-partial'];
    }
  });

  // ui() resolves the chrome labels the same way prose() resolves content, and it
  // is the only source of help chrome: the components must never reach for the 25
  // shared ngx-translate locale files. Untested, a resolution bug here would show
  // up as an English panel inside a Danish page.
  it('returns English chrome labels for an English locale', () => {
    expect(make('en-US').ui()).toBe(enUSUi);
  });

  it('resolves a bare language code to that locale\'s chrome labels', () => {
    expect(make('da').ui()).toBe(daUi);
  });

  it('resolves a regional code to its bare language, before falling back', () => {
    // ngx-translate reports whatever the account is set to; 'da-DK' has no map of
    // its own and must land on Danish rather than on English.
    expect(make('da-DK').ui()).toBe(daUi);
  });

  it('falls back to English chrome labels for a locale with no map', () => {
    expect(make('de-DE').ui()).toBe(enUSUi);
  });

  it('falls back to English chrome labels when no locale is reported at all', () => {
    expect(make('').ui()).toBe(enUSUi);
  });

  it('hides admin-only entries from a non-admin', () => {
    const service = make('en-US');
    const ids = service.entries({ isAdmin: false }).map(e => e.id);
    expect(ids).not.toContain('toolbar.payrollExport');
    expect(service.entries({ isAdmin: true }).map(e => e.id))
      .toContain('toolbar.payrollExport');
  });

  it('orders tour entries by step and drops admin-only steps for a non-admin', () => {
    const service = make('en-US');
    const steps = service.tourEntries('page', { isAdmin: false });
    expect(steps.map(e => e.tourStep)).toEqual([...steps.map(e => e.tourStep)].sort((a, b) => (a ?? 0) - (b ?? 0)));
    expect(steps.map(e => e.id)).not.toContain('toolbar.payrollExport');
    expect(service.tourEntries('page', { isAdmin: true }).map(e => e.id))
      .toContain('toolbar.payrollExport');
  });

  it('never returns undefined prose for a registry id', () => {
    const service = make('da');
    for (const entry of service.entries({ isAdmin: true })) {
      expect(service.prose(entry.id).short.length).toBeGreaterThan(0);
    }
  });
});

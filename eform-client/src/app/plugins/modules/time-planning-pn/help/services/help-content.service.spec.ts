import { TestBed } from '@angular/core/testing';
import { TranslateService } from '@ngx-translate/core';
import { HelpContentService } from './help-content.service';
import { enUS } from '../i18n/enUS';
import { da } from '../i18n/da';
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

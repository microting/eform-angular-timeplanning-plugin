import { Injectable } from '@angular/core';
import { TranslateService } from '@ngx-translate/core';
import { HelpEntry, HelpEntryId, HelpProse, HelpTourName, HelpUiStrings } from '../help.model';
import { PLANNING_HELP_ENTRIES } from '../planning-help.registry';
import { HELP_FALLBACK, HELP_LOCALES, HELP_UI_FALLBACK, HELP_UI_LOCALES } from '../i18n';

@Injectable({ providedIn: 'root' })
export class HelpContentService {
  private readonly byId = new Map<HelpEntryId, HelpEntry>(
    PLANNING_HELP_ENTRIES.map(entry => [entry.id, entry]),
  );

  constructor(private translateService: TranslateService) {}

  entry(id: HelpEntryId): HelpEntry | undefined {
    return this.byId.get(id);
  }

  /** Active locale, falling back to English one entry at a time. */
  prose(id: HelpEntryId): HelpProse {
    return this.localeProse()[id] ?? HELP_FALLBACK[id];
  }

  entries(opts: { isAdmin: boolean }): HelpEntry[] {
    return PLANNING_HELP_ENTRIES.filter(entry => !entry.adminOnly || opts.isAdmin);
  }

  tourEntries(tour: HelpTourName, opts: { isAdmin: boolean }): HelpEntry[] {
    return this.entries(opts)
      .filter(entry => entry.tour === tour && entry.tourStep !== undefined)
      .sort((a, b) => (a.tourStep as number) - (b.tourStep as number));
  }

  /** Chrome labels for the help components, resolved the same way as prose. */
  ui(): HelpUiStrings {
    const lang = this.lang();
    return HELP_UI_LOCALES[lang] ?? HELP_UI_LOCALES[lang.split('-')[0]] ?? HELP_UI_FALLBACK;
  }

  private localeProse(): Partial<Record<HelpEntryId, HelpProse>> {
    const lang = this.lang();
    return HELP_LOCALES[lang] ?? HELP_LOCALES[lang.split('-')[0]] ?? HELP_FALLBACK;
  }

  private lang(): string {
    return this.translateService.currentLang || 'en-US';
  }
}

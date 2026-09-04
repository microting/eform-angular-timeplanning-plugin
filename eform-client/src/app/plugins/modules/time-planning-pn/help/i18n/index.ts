import { HelpProseMap, HelpUiStrings } from '../help.model';
import { enUS, enUSUi } from './enUS';

/** Locale code (as ngx-translate reports it) to prose. Partial maps fall back per entry. */
export const HELP_LOCALES: Record<string, Partial<HelpProseMap>> = {
  'en-US': enUS,
};

export const HELP_UI_LOCALES: Record<string, HelpUiStrings> = {
  'en-US': enUSUi,
};

export const HELP_FALLBACK: HelpProseMap = enUS;
export const HELP_UI_FALLBACK: HelpUiStrings = enUSUi;

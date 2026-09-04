import { HelpProseMap, HelpUiStrings } from '../help.model';
import { enUS, enUSUi } from './enUS';
import { da, daUi } from './da';

/** Locale code (as ngx-translate reports it) to prose. Partial maps fall back per entry. */
export const HELP_LOCALES: Record<string, Partial<HelpProseMap>> = {
  'en-US': enUS,
  'da': da,
};

export const HELP_UI_LOCALES: Record<string, HelpUiStrings> = {
  'en-US': enUSUi,
  'da': daUi,
};

export const HELP_FALLBACK: HelpProseMap = enUS;
export const HELP_UI_FALLBACK: HelpUiStrings = enUSUi;

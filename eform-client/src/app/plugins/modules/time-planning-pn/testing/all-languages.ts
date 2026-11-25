import {fiFI} from '../i18n/fiFI';
import {translates} from '../i18n/translates';

/**
 * Every language the plugin ships, for "each language has this text" checks:
 * the registered ones plus fiFI, which `translates` leaves out.
 */
export const ALL_LANGUAGES = [...Object.values(translates), fiFI] as Record<string, string>[];

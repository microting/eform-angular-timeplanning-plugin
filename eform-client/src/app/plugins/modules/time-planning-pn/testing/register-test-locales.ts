import {registerLocaleData} from '@angular/common';
import localeBg from '@angular/common/locales/bg';
import localeCz from '@angular/common/locales/cs';
import localeDa from '@angular/common/locales/da';
import localeDe from '@angular/common/locales/de';
import localeEl from '@angular/common/locales/el';
import localeEn from '@angular/common/locales/en';
import localeEs from '@angular/common/locales/es';
import localeEt from '@angular/common/locales/et';
import localeFi from '@angular/common/locales/fi';
import localeFr from '@angular/common/locales/fr';
import localeHr from '@angular/common/locales/hr';
import localeHu from '@angular/common/locales/hu';
import localeIs from '@angular/common/locales/is';
import localeIt from '@angular/common/locales/it';
import localeLt from '@angular/common/locales/lt';
import localeLv from '@angular/common/locales/lv';
import localeNl from '@angular/common/locales/nl';
import localeNo from '@angular/common/locales/no';
import localePl from '@angular/common/locales/pl';
import localePt from '@angular/common/locales/pt';
import localeRo from '@angular/common/locales/ro';
import localeSk from '@angular/common/locales/sk';
import localeSl from '@angular/common/locales/sl';
import localeSv from '@angular/common/locales/sv';
import localeTr from '@angular/common/locales/tr';
import localeUk from '@angular/common/locales/uk';

/**
 * The locale data the running app registers, for a test bed.
 *
 * In production `src/main.ts` registers exactly this set before bootstrap, so
 * every DatePipe call in the plugin can format in the user's language. A Jest
 * bed never runs main.ts, and the host's `src/setup-jest.ts` (which is the only
 * truly global hook, and lives in eform-angular-frontend, not here) does not
 * register any of it — so a pipe asked for a non-English locale throws
 * NG0701 "Missing locale data". Angular resolves plain `en` from its built-in
 * data, which is why only the beds that format in another language notice.
 *
 * Call this from any spec whose component formats dates. Keep the list the same
 * as main.ts's: a bed that registers less than production is a bed that fails on
 * a language the app supports.
 */
export function registerTestLocales(): void {
  registerLocaleData(localeBg);
  registerLocaleData(localeCz);
  registerLocaleData(localeDa);
  registerLocaleData(localeDe);
  registerLocaleData(localeEl);
  registerLocaleData(localeEn);
  registerLocaleData(localeEs);
  registerLocaleData(localeEt);
  registerLocaleData(localeFi);
  registerLocaleData(localeFr);
  registerLocaleData(localeHr);
  registerLocaleData(localeHu);
  registerLocaleData(localeIs);
  registerLocaleData(localeIt);
  registerLocaleData(localeLt);
  registerLocaleData(localeLv);
  registerLocaleData(localeNl);
  registerLocaleData(localeNo);
  registerLocaleData(localePl);
  registerLocaleData(localePt);
  registerLocaleData(localeRo);
  registerLocaleData(localeSk);
  registerLocaleData(localeSl);
  registerLocaleData(localeSv);
  registerLocaleData(localeTr);
  registerLocaleData(localeUk);
}

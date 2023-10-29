import { AfterContentInit, Component, OnInit } from '@angular/core';
import { TranslateService } from '@ngx-translate/core';
import { translates } from './../i18n/translates';
import { AuthStateService } from 'src/app/common/store';
import {Store} from '@ngrx/store';
import {selectCurrentUserLocale} from 'src/app/state/auth/auth.selector';

@Component({
  selector: 'app-time-planning-pn-layout',
  template: `<router-outlet></router-outlet>`,
})
export class TimePlanningPnLayoutComponent
  implements AfterContentInit, OnInit {
  private selectCurrentUserLocale$ = this.store.select(selectCurrentUserLocale);
  constructor(
    private store: Store,
    private translateService: TranslateService,
    private authStateService: AuthStateService
  ) {}

  ngOnInit() {}

  ngAfterContentInit() {
  //   TODO: Fix this
  //   this.selectCurrentUserLocale$.subscribe((locale) => {
  //     this.setLocale(locale);
  //   });
  //   const lang = this.authStateService.currentUserLocale;
  //   const i18n = translates[lang];
  //   this.translateService.setTranslation(lang, i18n, true);
  }
}

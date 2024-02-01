import {AfterContentInit, Component, OnInit} from '@angular/core';
import {TranslateService} from '@ngx-translate/core';
import {translates} from './../i18n/translates';

@Component({
  selector: 'app-time-planning-pn-layout',
  template: `
    <router-outlet></router-outlet>`,
})
export class TimePlanningPnLayoutComponent
  implements AfterContentInit, OnInit {

  constructor(
    private translateService: TranslateService,
  ) {
  }

  ngOnInit() {
    Object.keys(translates).forEach(locale => {
      this.translateService.setTranslation(locale, translates[locale], true);
    });
  }

  ngAfterContentInit() {
  }
}

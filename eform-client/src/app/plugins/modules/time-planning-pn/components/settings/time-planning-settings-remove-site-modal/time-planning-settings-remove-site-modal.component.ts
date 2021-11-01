import {
  Component,
  EventEmitter,
  OnDestroy,
  OnInit,
  Output,
  ViewChild,
} from '@angular/core';
import { SiteNameDto } from 'src/app/common/models';
import { AutoUnsubscribe } from 'ngx-auto-unsubscribe';
import { Subscription } from 'rxjs';
import {TimePlanningPnSettingsService} from '../../../services';

@AutoUnsubscribe()
@Component({
  selector: 'app-time-planning-settings-remove-site-modal',
  templateUrl: './time-planning-settings-remove-site-modal.component.html',
  styleUrls: ['./time-planning-settings-remove-site-modal.component.scss'],
})
export class TimePlanningSettingsRemoveSiteModalComponent implements OnInit, OnDestroy {
  @ViewChild('frame', { static: false }) frame;
  @Output() siteRemoved: EventEmitter<void> = new EventEmitter<void>();
  selectedSite: SiteNameDto = new SiteNameDto();
  removeSub$: Subscription;

  constructor(private settingsService: TimePlanningPnSettingsService) {}

  ngOnInit(): void {}

  show(site: SiteNameDto) {
    this.selectedSite = site;
    this.frame.show();
  }

  removeSite() {
    this.removeSub$ = this.settingsService
      .removeSiteFromSettings(this.selectedSite.siteUId)
      .subscribe((data) => {
        this.siteRemoved.emit();
        this.frame.hide();
        this.selectedSite = new SiteNameDto();
      });
  }

  ngOnDestroy() {}
}

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
// import { eqBy, prop, symmetricDifferenceWith } from 'ramda';
import { TimePlanningPnSettingsService } from '../../../services';

@AutoUnsubscribe()
@Component({
  selector: 'app-time-planning-settings-add-site-modal',
  templateUrl: './time-planning-settings-add-site-modal.component.html',
  styleUrls: ['./time-planning-settings-add-site-modal.component.scss'],
})
export class TimePlanningSettingsAddSiteModalComponent
  implements OnInit, OnDestroy {
  @ViewChild('frame', { static: false }) frame;
  @Output() siteAdded: EventEmitter<void> = new EventEmitter<void>();
  availableSites: SiteNameDto[] = [];
  selectedSiteId: number;
  addSiteSub$: Subscription;

  constructor(private settingsService: TimePlanningPnSettingsService) {}

  ngOnInit(): void {}

  show(sites: SiteNameDto[], assignedSites: number[]) {
    // Removing assigned sites from all sites by id
    this.availableSites = sites.filter(
      (x) => !assignedSites.some((y) => y === x.siteUId)
    );
    // const propEqual = eqBy(prop('siteUId'));
    // this.availableSites = symmetricDifferenceWith(
    //   propEqual,
    //   sites.map((x) => x.siteUId),
    //   assignedSites
    // );
    this.frame.show();
  }

  assignSite() {
    this.addSiteSub$ = this.settingsService
      .addSiteToSettings(this.selectedSiteId)
      .subscribe((data) => {
        if (data && data.success) {
          this.frame.hide();
          this.selectedSiteId = null;
          this.availableSites = [];
          this.siteAdded.emit();
        }
      });
  }

  ngOnDestroy(): void {}
}

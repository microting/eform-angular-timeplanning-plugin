import {
  ChangeDetectorRef,
  Component,
  EventEmitter,
  OnDestroy,
  OnInit,
  ViewChild,
} from '@angular/core';
import { Subscription } from 'rxjs';
import { AutoUnsubscribe } from 'ngx-auto-unsubscribe';
import {
  SitesService,
  FoldersService,
  EFormService,
} from 'src/app/common/services';
import {
  FolderDto,
  SiteNameDto,
  TemplateListModel,
  TemplateRequestModel,
} from 'src/app/common/models';
import { composeFolderName } from 'src/app/common/helpers';
import { TimePlanningSettingsModel } from '../../../models';
import { TimePlanningPnSettingsService } from '../../../services';
import {
  TimePlanningSettingsFoldersModalComponent,
  TimePlanningSettingsAddSiteModalComponent,
  TimePlanningSettingsRemoveSiteModalComponent,
} from '../../../components';
import { debounceTime, switchMap } from 'rxjs/operators';

@AutoUnsubscribe()
@Component({
  selector: 'app-time-planning-settings',
  templateUrl: './time-planning-settings.component.html',
  styleUrls: ['./time-planning-settings.component.scss'],
})
export class TimePlanningSettingsComponent implements OnInit, OnDestroy {
  @ViewChild('removeSiteModal')
  removeSiteModal: TimePlanningSettingsRemoveSiteModalComponent;
  @ViewChild('addSiteModal')
  addSiteModal: TimePlanningSettingsAddSiteModalComponent;
  @ViewChild('foldersModal', { static: false })
  foldersModal: TimePlanningSettingsFoldersModalComponent;
  timePlanningSettingsModel: TimePlanningSettingsModel = new TimePlanningSettingsModel();
  sites: SiteNameDto[] = [];
  foldersTreeDto: FolderDto[] = [];
  foldersDto: FolderDto[] = [];
  templateRequestModel: TemplateRequestModel = new TemplateRequestModel();
  typeahead = new EventEmitter<string>();
  templatesModel: TemplateListModel = new TemplateListModel();

  settingsSub$: Subscription;
  sitesSub$: Subscription;
  foldersSubTree$: Subscription;
  foldersSub$: Subscription;
  folderUpdateSub$: Subscription;

  constructor(
    private settingsService: TimePlanningPnSettingsService,
    private sitesService: SitesService,
    private eFormService: EFormService,
    private cd: ChangeDetectorRef,
    private foldersService: FoldersService
  ) {
    this.typeahead
      .pipe(
        debounceTime(200),
        switchMap((term) => {
          this.templateRequestModel.nameFilter = term;
          return this.eFormService.getAll(this.templateRequestModel);
        })
      )
      .subscribe((items) => {
        this.templatesModel = items.model;
        this.cd.markForCheck();
      });
  }

  ngOnInit(): void {
    this.getSettings();
    this.getAllEforms();
  }

  getAllEforms() {
    this.eFormService.getAll(this.templateRequestModel).subscribe((items) => {
      this.templatesModel = items.model;
    });
  }

  getSettings() {
    this.settingsSub$ = this.settingsService
      .getAllSettings()
      .subscribe((data) => {
        if (data && data.success) {
          this.timePlanningSettingsModel = data.model;
          this.loadAllFoldersTree();
        }
      });
  }

  getSites() {
    this.sitesSub$ = this.sitesService.getAllSites().subscribe((data) => {
      if (data && data.success) {
        this.sites = data.model;
        this.loadFlatFolders();
      }
    });
  }

  loadAllFoldersTree() {
    this.foldersSubTree$ = this.foldersService
      .getAllFolders()
      .subscribe((operation) => {
        if (operation && operation.success) {
          this.foldersTreeDto = operation.model;
          this.getSites();
        }
      });
  }

  loadFlatFolders() {
    this.foldersSub$ = this.foldersService
      .getAllFoldersList()
      .subscribe((operation) => {
        if (operation && operation.success) {
          this.foldersDto = operation.model;
        }
      });
  }

  showAddNewSiteModal() {
    this.addSiteModal.show(
      this.sites,
      this.timePlanningSettingsModel.assignedSites
    );
  }

  showRemoveSiteModal(selectedSiteId: number) {
    this.removeSiteModal.show(
      this.sites.find((x) => x.siteUId === selectedSiteId)
    );
  }

  openFoldersModal() {
    this.foldersModal.show(this.timePlanningSettingsModel.folderId);
  }

  onFolderSelected(folderDto: FolderDto) {
    if (folderDto) {
      this.updateFolder(folderDto.id);
    }
  }

  getFolderName(): string {
    return this.timePlanningSettingsModel.folderId === null
      ? ''
      : composeFolderName(
          this.timePlanningSettingsModel.folderId,
          this.foldersDto
        );
  }

  updateFolder(folderDtoId: number) {
    this.folderUpdateSub$ = this.settingsService
      .updateSettingsFolder(folderDtoId)
      .subscribe((operation) => {
        if (operation && operation.success) {
          this.getSettings();
        }
      });
  }

  getNameSite(id: number) {
    const index = this.sites.findIndex((x) => x.siteUId === id);
    if (index !== -1) {
      return this.sites[index].siteName;
    }
  }

  updateEform(eformId: number) {
    this.settingsService.updateSettingsEform(eformId).subscribe((operation) => {
      if (operation && operation.success) {
        this.getSettings();
      }
    });
  }

  ngOnDestroy(): void {}
}

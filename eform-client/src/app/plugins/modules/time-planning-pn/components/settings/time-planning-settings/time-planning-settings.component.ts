import { Component, OnDestroy, OnInit, ViewChild } from '@angular/core';
import { Subscription } from 'rxjs';
import { AutoUnsubscribe } from 'ngx-auto-unsubscribe';
import { SitesService, FoldersService } from 'src/app/common/services';
import { FolderDto, SiteNameDto } from 'src/app/common/models';
import { composeFolderName } from 'src/app/common/helpers';
import { TimePlanningSettingsModel } from 'src/app/plugins/modules/time-planning-pn/models';
import { TimePlanningPnSettingsService } from 'src/app/plugins/modules/time-planning-pn/services';
import { TimePlanningSettingsFoldersModalComponent } from '../time-planning-settings-folders-modal/time-planning-settings-folders-modal.component';
import { TimePlanningSettingsAddSiteModalComponent } from '../time-planning-settings-add-site-modal/time-planning-settings-add-site-modal.component';
import { TimePlanningSettingsRemoveSiteModalComponent } from '../time-planning-settings-remove-site-modal/time-planning-settings-remove-site-modal.component';

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
  timePlanningSettingsModel: TimePlanningSettingsModel =
    new TimePlanningSettingsModel();
  sites: SiteNameDto[] = [];
  foldersTreeDto: FolderDto[] = [];
  foldersDto: FolderDto[] = [];
  settingsSub$: Subscription;
  sitesSub$: Subscription;
  foldersSubTree$: Subscription;
  foldersSub$: Subscription;
  folderUpdateSub$: Subscription;
  tasksFolder: boolean;

  constructor(
    private settingsService: TimePlanningPnSettingsService,
    private sitesService: SitesService,
    private foldersService: FoldersService
  ) {}

  ngOnInit(): void {
    this.getSettings();
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
          this.setFolderName();
          this.setFolderTaskName();
        }
      });
  }

  showAddNewSiteModal() {
    this.addSiteModal.show(
      this.sites,
      this.timePlanningSettingsModel.assignedSites
    );
  }

  showRemoveSiteModal(selectedSite: SiteNameDto) {
    this.removeSiteModal.show(selectedSite);
  }

  openFoldersModal() {
    this.tasksFolder = false;
    this.foldersModal.show(this.timePlanningSettingsModel.folderId);
  }

  onFolderSelected(folderDto: FolderDto) {
    this.updateFolder(folderDto.id);
  }

  setFolderTaskName() {
    this.timePlanningSettingsModel.folderTasksId === null
      ? (this.timePlanningSettingsModel.folderTasksName = null)
      : (this.timePlanningSettingsModel.folderTasksName = composeFolderName(
          this.timePlanningSettingsModel.folderTasksId,
          this.foldersDto
        ));
  }

  setFolderName() {
    this.timePlanningSettingsModel.folderId === null
      ? (this.timePlanningSettingsModel.folderName = null)
      : (this.timePlanningSettingsModel.folderName = composeFolderName(
          this.timePlanningSettingsModel.folderId,
          this.foldersDto
        ));
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

  ngOnDestroy(): void {}
}

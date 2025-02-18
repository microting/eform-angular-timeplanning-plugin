import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import {
  OperationDataResult,
  OperationResult,
  SiteDto,
} from 'src/app/common/models';
import { ApiBaseService } from 'src/app/common/services';
import { TimePlanningSettingsModel,
  AssignedSiteModel,
  AssignedSiteUpdateModel } from '../models';

export let TimePlanningSettingsMethods = {
  Settings: 'api/time-planning-pn/settings',
  SettingsSites: 'api/time-planning-pn/settings/sites',
  SettingsFolder: 'api/time-planning-pn/settings/folder',
  SettingsEform: 'api/time-planning-pn/settings/eform',
  GetAssignedSites: 'api/time-planning-pn/settings/assigned-sites',
  UpdateAssignedSites: 'api/time-planning-pn/settings/assigned-sites',
};

@Injectable()
export class TimePlanningPnSettingsService {
  constructor(private apiBaseService: ApiBaseService) {}

  getAllSettings(): Observable<OperationDataResult<TimePlanningSettingsModel>> {
    return this.apiBaseService.get(TimePlanningSettingsMethods.Settings);
  }

  updateSettings(model: TimePlanningSettingsModel): Observable<OperationResult> {
    return this.apiBaseService.put(
      TimePlanningSettingsMethods.Settings,
      model
    );
  }

  getAvailableSites(): Observable<OperationDataResult<SiteDto[]>> {
    return this.apiBaseService.get(TimePlanningSettingsMethods.SettingsSites);
  }

  addSiteToSettings(siteId: number): Observable<OperationResult> {
    return this.apiBaseService.put(
      TimePlanningSettingsMethods.SettingsSites,
      siteId
    );
  }

  removeSiteFromSettings(id: number): Observable<OperationResult> {
    return this.apiBaseService.delete(
      TimePlanningSettingsMethods.SettingsSites,
      { siteId: id }
    );
  }

  updateSettingsFolder(folderId: number): Observable<OperationResult> {
    return this.apiBaseService.put(
      TimePlanningSettingsMethods.SettingsFolder,
      folderId
    );
  }

  updateSettingsEform(eformId: number): Observable<OperationResult> {
    return this.apiBaseService.put(
      TimePlanningSettingsMethods.SettingsEform,
      eformId
    );
  }

  getAssignedSite(siteId: number): Observable<OperationDataResult<AssignedSiteModel[]>> {
    return this.apiBaseService.get(TimePlanningSettingsMethods.GetAssignedSites + '?siteId=' + siteId);
  }

  updateAssignedSites(model: AssignedSiteUpdateModel[]): Observable<OperationResult> {
    return this.apiBaseService.put(
      TimePlanningSettingsMethods.UpdateAssignedSites,
      model
    );
  }
}

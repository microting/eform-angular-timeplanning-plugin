import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { OperationDataResult, OperationResult } from 'src/app/common/models';
import { ApiBaseService } from 'src/app/common/services';
import {TimePlanningSettingsModel} from 'src/app/plugins/modules/time-planning-pn/models';

export let TimePlanningSettingsMethods = {
  Settings: 'api/time-planning-pn/settings',
  SettingsSites: 'api/time-planning-pn/settings/sites',
  SettingsFolder: 'api/time-planning-pn/settings/folder'
};

@Injectable()
export class TimePlanningPnSettingsService {
  constructor(private apiBaseService: ApiBaseService) {}

  getAllSettings(): Observable<OperationDataResult<TimePlanningSettingsModel>> {
    return this.apiBaseService.get(TimePlanningSettingsMethods.Settings);
  }

  addSiteToSettings(siteId: number): Observable<OperationResult> {
    return this.apiBaseService.post(
      TimePlanningSettingsMethods.SettingsSites,
      siteId
    );
  }

  removeSiteFromSettings(id: number): Observable<OperationResult> {
    return this.apiBaseService.delete(
      TimePlanningSettingsMethods.SettingsSites + '/' + id
    );
  }

  updateSettingsFolder(folderId: number): Observable<OperationResult> {
    return this.apiBaseService.post(
      TimePlanningSettingsMethods.SettingsFolder,
      folderId
    );
  }
}

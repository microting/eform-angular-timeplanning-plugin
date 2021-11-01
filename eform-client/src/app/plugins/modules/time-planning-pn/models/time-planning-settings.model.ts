import { SiteNameDto } from 'src/app/common/models';

export class TimePlanningSettingsModel {
  folderId?: number;
  folderTasksId?: number;
  folderName: string;
  folderTasksName: string;
  assignedSites: SiteNameDto[] = [];
}

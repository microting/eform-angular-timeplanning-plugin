export class TimePlanningSettingsModel {
  folderId?: number;
  eformId?: number;
  folderTasksId?: number;
  folderName: string;
  folderTasksName: string;
  assignedSites: number[] = [];
}

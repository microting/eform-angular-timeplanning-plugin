export class TimePlanningsRequestModel {
  dateTo: string;
  dateFrom: string;
  sort?: string;
  isSortDsc?: boolean;
  siteId: number;
  showResignedSites?: boolean;
  tagIds?: number[];

  constructor(data?: any) {
    if (data) {
      this.dateTo = data.dateTo;
      this.dateFrom = data.dateFrom;
      this.sort = data.sort;
      this.isSortDsc = data.isSortDsc;
      this.siteId = data.siteId;
      this.showResignedSites = data.showResignedSites;
      this.tagIds = data.tagIds;
    }
  }
}

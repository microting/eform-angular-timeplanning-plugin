export class TimePlanningsRequestModel {
  dateTo: string;
  dateFrom: string;
  sort?: string;
  isSortDsc?: boolean;

  constructor(data?: any) {
    if (data) {
      this.dateTo = data.dateTo;
      this.dateFrom = data.dateFrom;
      this.sort = data.sort;
      this.isSortDsc = data.isSortDsc;
    }
  }
}

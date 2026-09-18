export interface TimePlanningsReportAllWorkersDownloadRequestModel {
  dateFrom: string;
  dateTo: string;
  /** Any-of, like the dashboard's own tag filter. Left out entirely when nothing is picked. */
  tagIds?: number[];
}

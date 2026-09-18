/**
 * One worker's tag membership, as the dashboard sees it. Fetched whole so a caller can
 * answer "how many workers do these tags cover" without a request per filter change.
 */
export interface SiteTagsModel {
  siteId: number;
  tagIds: number[];
  resigned: boolean;
}

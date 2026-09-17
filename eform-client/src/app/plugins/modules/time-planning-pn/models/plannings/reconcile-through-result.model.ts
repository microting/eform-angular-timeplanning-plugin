/**
 * Mirrors the C# ReconcileThroughResultModel (plan Task 4) field for field.
 *
 * Per-worker landing, because the boundary is a staircase: the mark falls on each
 * worker's own latest registered day at or before the requested date, so one shared
 * "landedOn" would name the wrong date for most of them.
 */
export class ReconcileThroughResultModel {
  /** siteId -> the day the mark landed on. JSON object keys arrive as strings. */
  landedOnBySiteId: { [siteId: number]: string } = {};
  /** Workers whose boundary actually moved. Excludes no-ops. */
  applied: number;
  /** Already reconciled at or past the target. Moving them back would be an unlock. */
  skippedAlreadyFurtherForward: number[] = [];
  /** No registration at or before the target, so there was nothing to mark. */
  skippedNoRegistration: number[] = [];
  /** Already marked on exactly the landing day; nothing changed. */
  alreadyReconciledSiteIds: number[] = [];
}

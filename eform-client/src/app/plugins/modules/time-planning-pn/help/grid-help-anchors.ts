import { isDevMode } from '@angular/core';
import { PLANNING_HELP_ENTRIES } from './planning-help.registry';

/**
 * mtx-grid renders its own header row, and MtxGridColumn has no per-column
 * header template, so the Name column's sort header cannot carry a
 * `data-tp-help` attribute from a template the way every other anchor does.
 * Stamp it after the grid has rendered instead.
 */
export const GRID_NAME_HEADER_SELECTOR = 'th.mat-column-siteName';

/** Read from the registry rather than repeated, so the stamp cannot drift from it. */
const SORT_NAME_ANCHOR = PLANNING_HELP_ENTRIES.find(entry => entry.id === 'grid.sortName')?.anchor;

let warnedAboutMissingAnchor = false;

export function applyGridHelpAnchors(root: ParentNode | null | undefined): void {
  if (!SORT_NAME_ANCHOR) {
    // A stamp that quietly stops happening is worse than one that complains:
    // the registry would still advertise an anchor nothing produces.
    if (isDevMode() && !warnedAboutMissingAnchor) {
      warnedAboutMissingAnchor = true;
      console.warn(
        '[tp-help] grid.sortName has no anchor in PLANNING_HELP_ENTRIES; '
        + 'the Name column header will not be stamped.',
      );
    }
    return;
  }
  const header = root?.querySelector(GRID_NAME_HEADER_SELECTOR);
  if (header && !header.hasAttribute('data-tp-help')) {
    header.setAttribute('data-tp-help', SORT_NAME_ANCHOR);
  }
}

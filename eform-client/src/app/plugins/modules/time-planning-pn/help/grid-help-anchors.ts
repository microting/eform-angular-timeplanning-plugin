import { isDevMode } from '@angular/core';
import { HelpEntry } from './help.model';
import { PLANNING_HELP_ENTRIES } from './planning-help.registry';

/**
 * mtx-grid renders its own header row, and MtxGridColumn has no per-column
 * header template, so the Name column's sort header cannot carry a
 * `data-tp-help` attribute from a template the way every other anchor does.
 * Stamp it after the grid has rendered instead.
 */
export const GRID_NAME_HEADER_SELECTOR = 'th.mat-column-siteName';

/** Read from the registry rather than repeated, so the stamp cannot drift from it. */
const findSortNameAnchor = (entries: readonly HelpEntry[]) =>
  entries.find(entry => entry.id === 'grid.sortName')?.anchor;
const SORT_NAME_ANCHOR = findSortNameAnchor(PLANNING_HELP_ENTRIES);

let warnedAboutMissingAnchor = false;

/**
 * `entries` exists for specs: the app always stamps from PLANNING_HELP_ENTRIES.
 */
export function applyGridHelpAnchors(
  root: ParentNode | null | undefined,
  entries: readonly HelpEntry[] = PLANNING_HELP_ENTRIES,
): void {
  const sortNameAnchor = entries === PLANNING_HELP_ENTRIES ? SORT_NAME_ANCHOR : findSortNameAnchor(entries);
  if (!sortNameAnchor) {
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
    header.setAttribute('data-tp-help', sortNameAnchor);
  }
}

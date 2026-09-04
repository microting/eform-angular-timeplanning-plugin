import { PLANNING_HELP_ENTRIES } from './planning-help.registry';

/**
 * mtx-grid renders its own header row, and MtxGridColumn has no per-column
 * header template, so the Name column's sort header cannot carry a
 * `data-tp-help` attribute from a template the way every other anchor does.
 * Stamp it after the grid has rendered instead.
 */
export const GRID_NAME_HEADER_SELECTOR = 'th.mat-column-siteName';

/** Taken from the registry rather than repeated, so the two cannot drift. */
const SORT_NAME_ANCHOR = PLANNING_HELP_ENTRIES.find(entry => entry.id === 'grid.sortName')?.anchor;

export function applyGridHelpAnchors(root: ParentNode | null | undefined): void {
  if (!SORT_NAME_ANCHOR) {
    return;
  }
  const header = root?.querySelector(GRID_NAME_HEADER_SELECTOR);
  if (header && !header.hasAttribute('data-tp-help')) {
    header.setAttribute('data-tp-help', SORT_NAME_ANCHOR);
  }
}

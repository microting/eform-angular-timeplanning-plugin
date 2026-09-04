/**
 * mtx-grid renders its own header row, and MtxGridColumn has no per-column
 * header template, so the Name column's sort header cannot carry a
 * `data-tp-help` attribute from a template the way every other anchor does.
 * Stamp it after the grid has rendered instead.
 */
export const GRID_NAME_HEADER_SELECTOR = 'th.mat-column-siteName';

export function applyGridHelpAnchors(root: ParentNode | null | undefined): void {
  const header = root?.querySelector(GRID_NAME_HEADER_SELECTOR);
  if (header && !header.hasAttribute('data-tp-help')) {
    header.setAttribute('data-tp-help', 'grid.sortName');
  }
}

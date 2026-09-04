import { Injectable } from '@angular/core';
import { BehaviorSubject, Observable } from 'rxjs';
import { HelpEntryId, HelpTourName } from '../help.model';

/**
 * Open/close state for the help side panel. It is a service rather than component
 * state so that anything on the page — a help icon deep in the grid, a toolbar
 * button, the tour — can open the panel without owning it.
 */
@Injectable({ providedIn: 'root' })
export class HelpPanelService {
  private readonly openState = new BehaviorSubject<boolean>(false);
  private readonly targetState = new BehaviorSubject<HelpEntryId | null>(null);
  private readonly surfaceState = new BehaviorSubject<HelpTourName>('page');

  readonly isOpen$: Observable<boolean> = this.openState.asObservable();
  readonly target$: Observable<HelpEntryId | null> = this.targetState.asObservable();

  /**
   * Which tour belongs to the surface the panel was last opened from. There is one
   * panel for the whole page, and it can be opened from the toolbar or from inside
   * the day-cell dialog; "Take the tour" has to replay the tour of the surface the
   * planner is actually looking at, or it points at anchors behind the dialog
   * backdrop and leaves a card nobody can reach.
   */
  readonly surface$: Observable<HelpTourName> = this.surfaceState.asObservable();

  /** Opens the panel, optionally scrolled to and expanded on one entry. */
  open(target?: HelpEntryId, surface: HelpTourName = 'page'): void {
    this.surfaceState.next(surface);
    this.targetState.next(target ?? null);
    this.openState.next(true);
  }

  close(): void {
    this.openState.next(false);
    this.targetState.next(null);
    this.surfaceState.next('page');
  }
}

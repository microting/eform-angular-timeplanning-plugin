import { Injectable } from '@angular/core';
import { BehaviorSubject, Observable } from 'rxjs';
import { HelpEntryId } from '../help.model';

/**
 * Open/close state for the help side panel. It is a service rather than component
 * state so that anything on the page — a help icon deep in the grid, a toolbar
 * button, the tour — can open the panel without owning it.
 */
@Injectable({ providedIn: 'root' })
export class HelpPanelService {
  private readonly openState = new BehaviorSubject<boolean>(false);
  private readonly targetState = new BehaviorSubject<HelpEntryId | null>(null);

  readonly isOpen$: Observable<boolean> = this.openState.asObservable();
  readonly target$: Observable<HelpEntryId | null> = this.targetState.asObservable();

  /** Opens the panel, optionally scrolled to and expanded on one entry. */
  open(target?: HelpEntryId): void {
    this.targetState.next(target ?? null);
    this.openState.next(true);
  }

  close(): void {
    this.openState.next(false);
    this.targetState.next(null);
  }
}

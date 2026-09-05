import { inject, Injectable, OnDestroy } from '@angular/core';
import { Store } from '@ngrx/store';
import { BehaviorSubject, Observable, Subscription } from 'rxjs';
import { selectCurrentUserIsAdmin } from 'src/app/state';

/**
 * The single switch that decides whether any help chrome exists at all.
 *
 * The whole help system is admin-only for now — not the ? button, not the panel,
 * not either tour, not one ⓘ, not one inline hint. Gating that at the eighteen
 * call sites spread over three templates would be eighteen chances to miss one,
 * so every surface asks this service instead: HelpEntryChromeBase for the icons
 * and hints, HelpPanelComponent for the panel, HelpTourService for both tours.
 *
 * `selectCurrentUserIsAdmin` is the selector the planning container, the day-cell
 * dialog and the rest of this plugin family already standardise on.
 *
 * Subscribed live, deliberately, and never with take(1): the admin flag is not
 * necessarily in the store when the planning page is constructed, and a one-shot
 * read that happens to land first would latch `false` and hide help from an
 * admin for the rest of the session — a silent, unreproducible disappearance.
 */
@Injectable({ providedIn: 'root' })
export class HelpVisibilityService implements OnDestroy {
  private readonly store = inject(Store);
  private readonly visible = new BehaviorSubject<boolean>(false);
  private readonly subscription: Subscription;

  constructor() {
    this.subscription = this.store.select(selectCurrentUserIsAdmin)
      .subscribe(isAdmin => this.visible.next(!!isAdmin));
  }

  /** For templates and anything that wants to react to the flag arriving. */
  readonly isVisible$: Observable<boolean> = this.visible.asObservable();

  /** For the synchronous guards — a prose getter, a start() that must refuse now. */
  get isVisible(): boolean {
    return this.visible.value;
  }

  ngOnDestroy(): void {
    this.subscription.unsubscribe();
  }
}

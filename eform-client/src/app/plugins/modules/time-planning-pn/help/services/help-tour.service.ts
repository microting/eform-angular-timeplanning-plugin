import { Injectable } from '@angular/core';
import { BehaviorSubject, Observable } from 'rxjs';
import { HelpEntry, HelpTourName } from '../help.model';
import { HelpContentService } from './help-content.service';

export const TOUR_STORAGE_KEY = 'tp.planning.tour.v1';

export interface HelpTourState {
  entry: HelpEntry;
  index: number;
  total: number;
}

/**
 * Sequences the guided tour. The steps come from the registry in tourStep order;
 * this service decides which of them can actually be shown and where it is up to.
 */
@Injectable({ providedIn: 'root' })
export class HelpTourService {
  private readonly stateSubject = new BehaviorSubject<HelpTourState | null>(null);
  private steps: HelpEntry[] = [];
  private index = 0;
  private current: HelpTourName | null = null;

  readonly state$: Observable<HelpTourState | null> = this.stateSubject.asObservable();

  constructor(private helpContent: HelpContentService) {}

  /**
   * Steps whose anchor is absent are dropped, never treated as an error: the
   * payroll-export control only renders for Microting staff, and the worker
   * filter only renders when the account has more than one site.
   */
  start(tour: HelpTourName, opts: { isAdmin: boolean }): void {
    this.current = tour;
    this.steps = this.helpContent
      .tourEntries(tour, opts)
      .filter(entry => !!this.anchorElement(entry));
    this.index = 0;
    this.emit();
  }

  next(): void {
    this.index += 1;
    this.emit();
  }

  /** Skipping counts as having seen it — but only if a step was actually shown. */
  stop(): void {
    if (this.current && this.steps.length > 0) {
      this.markSeen(this.current);
    }
    this.current = null;
    this.steps = [];
    this.index = 0;
    this.stateSubject.next(null);
  }

  /** True while a tour is on screen. */
  get isRunning(): boolean {
    return this.stateSubject.value !== null;
  }

  anchorElement(entry: HelpEntry): HTMLElement | null {
    return entry.anchor
      ? document.querySelector<HTMLElement>(`[data-tp-help="${entry.anchor}"]`)
      : null;
  }

  hasSeen(tour: HelpTourName): boolean {
    return this.readSeen().includes(tour);
  }

  markSeen(tour: HelpTourName): void {
    const seen = this.readSeen();
    if (!seen.includes(tour)) {
      this.writeSeen([...seen, tour]);
    }
  }

  private emit(): void {
    const entry = this.steps[this.index];
    if (entry) {
      this.stateSubject.next({ entry, index: this.index, total: this.steps.length });
      return;
    }
    // Ran to the end. Record it here, in the service, rather than in the component:
    // state$ is a BehaviorSubject seeded null, so a component that marks "seen"
    // whenever it observes null would do so on its very first subscription — before
    // any tour has run — and the automatic first-run tour would never appear.
    // `steps.length` guards the other direction: a tour that could not start because
    // none of its anchors were in the DOM has not been seen, and must be offered again.
    if (this.current && this.steps.length > 0) {
      this.markSeen(this.current);
    }
    this.current = null;
    this.stateSubject.next(null);
  }

  private readSeen(): string[] {
    try {
      return JSON.parse(localStorage.getItem(TOUR_STORAGE_KEY) ?? '[]') as string[];
    } catch {
      return [];
    }
  }

  private writeSeen(seen: string[]): void {
    try {
      localStorage.setItem(TOUR_STORAGE_KEY, JSON.stringify(seen));
    } catch {
      // Storage unavailable (private mode, blocked cookies) — the tour simply reruns.
    }
  }
}

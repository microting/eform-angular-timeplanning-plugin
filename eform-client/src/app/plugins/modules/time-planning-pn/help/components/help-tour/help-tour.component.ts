import {
  AfterViewChecked,
  Component,
  DoCheck,
  ElementRef,
  Input,
  OnDestroy,
  OnInit,
  ViewChild,
} from '@angular/core';
import { CdkConnectedOverlay, ConnectedPosition } from '@angular/cdk/overlay';
import { Subscription } from 'rxjs';
import { HelpProse, HelpTourName } from '../../help.model';
import { HelpTourService, HelpTourState } from '../../services/help-tour.service';
import { HelpChromeBase } from '../help-chrome.base';

/** Distinguishes the aria-labelledby target of one mounted tour from another's. */
let nextTourCardId = 0;

@Component({
  selector: 'tp-help-tour',
  templateUrl: './help-tour.component.html',
  styleUrls: ['./help-tour.component.scss'],
  standalone: false,
})
export class HelpTourComponent extends HelpChromeBase
  implements OnInit, DoCheck, AfterViewChecked, OnDestroy {
  /**
   * Which tour this instance renders. The service is a singleton and one page can
   * mount this component twice — once for the page tour, once inside the day-cell
   * dialog — so an instance must ignore state belonging to the other tour.
   */
  @Input() tour: HelpTourName = 'page';

  state: HelpTourState | null = null;

  /**
   * The anchor is a raw element found by querySelector, so it is wrapped:
   * cdkConnectedOverlayOrigin also accepts an Element, but an ElementRef keeps
   * the binding's intent explicit and matches the sibling help components.
   */
  origin: ElementRef<HTMLElement> | null = null;

  readonly titleId = `tp-help-tour-title-${nextTourCardId++}`;

  readonly positions: ConnectedPosition[] = [
    { originX: 'center', originY: 'bottom', overlayX: 'start', overlayY: 'top', offsetY: 10 },
    { originX: 'center', originY: 'top', overlayX: 'start', overlayY: 'bottom', offsetY: -10 },
  ];

  @ViewChild(CdkConnectedOverlay) private connectedOverlay?: CdkConnectedOverlay;

  private readonly subscriptions = new Subscription();
  private pendingFocus = false;

  /**
   * The overlay uses CDK's reposition strategy, so an anchor below the fold
   * yields a card pushed on screen pointing at nothing. Bring each step's anchor
   * into view as it becomes current — once per step, so a re-rendered anchor node
   * does not yank the page back mid-read.
   */
  private scrolledFor: string | null = null;

  constructor(private helpTour: HelpTourService) {
    super();
  }

  get prose(): HelpProse | null {
    return this.state ? this.helpContent.prose(this.state.entry.id) : null;
  }

  ngOnInit(): void {
    // Deliberately does NOT mark the tour seen here. state$ is a BehaviorSubject
    // seeded null, so this fires once at mount with state === null; marking seen
    // there would suppress the automatic first run. The service records it instead,
    // when a tour actually ends or is skipped.
    this.subscriptions.add(this.helpTour.state$.subscribe(state => {
      const mine = state && state.entry.tour === this.tour ? state : null;
      this.pendingFocus = this.pendingFocus || (!!mine && !this.state);
      this.state = mine;
      this.setOrigin(mine ? this.helpTour.anchorElement(mine.entry) : null);
      if (!mine) {
        this.scrolledFor = null;
      } else if (this.scrolledFor !== mine.entry.id) {
        this.scrolledFor = mine.entry.id;
        // Optional call: jsdom and other non-layout hosts do not implement it.
        this.origin?.nativeElement.scrollIntoView?.({ block: 'center', inline: 'center' });
      }
    }));
  }

  /**
   * Anchors are only validated when the tour starts. If the current step's anchor
   * leaves the DOM afterwards — the day-cell dialog closes mid-tour, a filter hides
   * the worker select — the overlay would close while the tour stayed "running",
   * leaving no card and no way to skip. End the tour instead. A replaced (rather
   * than removed) anchor node just re-points the overlay.
   *
   * abort(), not stop(): the page changed underneath the tour, which says nothing
   * about whether the user is done with it, so it stays offerable.
   */
  ngDoCheck(): void {
    if (!this.state) {
      return;
    }
    const element = this.helpTour.anchorElement(this.state.entry);
    if (element) {
      this.setOrigin(element);
    } else {
      this.helpTour.abort();
    }
  }

  ngAfterViewChecked(): void {
    if (!this.pendingFocus) {
      return;
    }
    // The overlay is appended at the end of <body>, far from the anchor in tab
    // order, so a keyboard user cannot otherwise reach Skip or Next. No focus
    // trap: the tour is non-modal and must not lock the page it is explaining.
    const primaryAction = this.connectedOverlay?.overlayRef?.overlayElement
      ?.querySelector<HTMLElement>('.tp-help-tour__next');
    if (primaryAction) {
      this.pendingFocus = false;
      primaryAction.focus();
    }
  }

  ngOnDestroy(): void {
    this.subscriptions.unsubscribe();
  }

  next(): void {
    this.helpTour.next();
  }

  skip(): void {
    this.helpTour.stop();
  }

  onOverlayKeydown(event: KeyboardEvent): void {
    if (this.state && event.key === 'Escape') {
      this.skip();
    }
  }

  private setOrigin(element: HTMLElement | null): void {
    if (this.origin?.nativeElement === element) {
      return;
    }
    this.origin = element ? new ElementRef(element) : null;
  }
}

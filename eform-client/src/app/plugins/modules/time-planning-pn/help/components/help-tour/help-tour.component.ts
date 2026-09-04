import { Component, ElementRef, Input, OnDestroy, OnInit } from '@angular/core';
import { ConnectedPosition } from '@angular/cdk/overlay';
import { Subscription } from 'rxjs';
import { HelpProse, HelpTourName, HelpUiStrings } from '../../help.model';
import { HelpContentService } from '../../services/help-content.service';
import { HelpTourService, HelpTourState } from '../../services/help-tour.service';

@Component({
  selector: 'tp-help-tour',
  templateUrl: './help-tour.component.html',
  styleUrls: ['./help-tour.component.scss'],
  standalone: false,
})
export class HelpTourComponent implements OnInit, OnDestroy {
  @Input() tour: HelpTourName = 'page';
  @Input() isAdmin = false;

  state: HelpTourState | null = null;

  /**
   * The anchor is a raw element found by querySelector, so it is wrapped:
   * cdkConnectedOverlayOrigin takes an ElementRef or a CdkOverlayOrigin.
   */
  origin: ElementRef<HTMLElement> | null = null;

  readonly positions: ConnectedPosition[] = [
    { originX: 'center', originY: 'bottom', overlayX: 'start', overlayY: 'top', offsetY: 10 },
    { originX: 'center', originY: 'top', overlayX: 'start', overlayY: 'bottom', offsetY: -10 },
  ];

  private readonly subscriptions = new Subscription();

  constructor(
    private helpContent: HelpContentService,
    private helpTour: HelpTourService,
  ) {}

  get prose(): HelpProse | null {
    return this.state ? this.helpContent.prose(this.state.entry.id) : null;
  }

  /** Help chrome labels. Never the shared ngx-translate catalogue. */
  get ui(): HelpUiStrings {
    return this.helpContent.ui();
  }

  ngOnInit(): void {
    // Deliberately does NOT mark the tour seen here. state$ is a BehaviorSubject
    // seeded null, so this fires once at mount with state === null; marking seen
    // there would suppress the automatic first run. The service records it instead,
    // when a tour actually ends or is skipped.
    this.subscriptions.add(this.helpTour.state$.subscribe(state => {
      this.state = state;
      const element = state ? this.helpTour.anchorElement(state.entry) : null;
      this.origin = element ? new ElementRef(element) : null;
    }));
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
}

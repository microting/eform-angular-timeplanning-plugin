import { Component, EventEmitter, Output } from '@angular/core';
import { ConnectedPosition, Overlay, ScrollStrategy } from '@angular/cdk/overlay';
import { HelpEntryId } from '../../help.model';
import { HelpEntryChromeBase } from '../help-chrome.base';

@Component({
  selector: 'tp-help-icon',
  templateUrl: './help-icon.component.html',
  styleUrls: ['./help-icon.component.scss'],
  standalone: false,
})
export class HelpIconComponent extends HelpEntryChromeBase {
  @Output() openInPanel = new EventEmitter<HelpEntryId>();

  isOpen = false;

  readonly positions: ConnectedPosition[] = [
    { originX: 'center', originY: 'bottom', overlayX: 'start', overlayY: 'top', offsetY: 6 },
    { originX: 'center', originY: 'top', overlayX: 'start', overlayY: 'bottom', offsetY: -6 },
    { originX: 'center', originY: 'bottom', overlayX: 'end', overlayY: 'top', offsetY: 6 },
    { originX: 'center', originY: 'top', overlayX: 'end', overlayY: 'bottom', offsetY: -6 },
  ];

  /**
   * Dismiss on scroll rather than following the trigger. This popover means
   * "this explains the control next to me"; in a horizontally scrolling grid,
   * CDK's default reposition strategy would leave it floating over unrelated
   * columns, or trailing a trigger the planner has already scrolled past.
   */
  readonly scrollStrategy: ScrollStrategy;

  constructor(overlay: Overlay) {
    super();
    this.scrollStrategy = overlay.scrollStrategies.close();
  }

  toggle(): void {
    this.isOpen = !this.isOpen;
  }

  close(): void {
    this.isOpen = false;
  }

  onOverlayKeydown(event: KeyboardEvent): void {
    if (event.key === 'Escape') {
      this.close();
    }
  }

  onMore(): void {
    this.openInPanel.emit(this.helpId);
    this.close();
  }
}

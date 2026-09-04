import { Component, EventEmitter, Input, Output } from '@angular/core';
import { ConnectedPosition } from '@angular/cdk/overlay';
import { HelpEntryId, HelpProse, HelpUiStrings } from '../../help.model';
import { HelpContentService } from '../../services/help-content.service';

@Component({
  selector: 'tp-help-icon',
  templateUrl: './help-icon.component.html',
  styleUrls: ['./help-icon.component.scss'],
  standalone: false,
})
export class HelpIconComponent {
  @Input() helpId!: HelpEntryId;
  @Output() openInPanel = new EventEmitter<HelpEntryId>();

  isOpen = false;

  readonly positions: ConnectedPosition[] = [
    { originX: 'center', originY: 'bottom', overlayX: 'start', overlayY: 'top', offsetY: 6 },
    { originX: 'center', originY: 'top', overlayX: 'start', overlayY: 'bottom', offsetY: -6 },
    { originX: 'center', originY: 'bottom', overlayX: 'end', overlayY: 'top', offsetY: 6 },
    { originX: 'center', originY: 'top', overlayX: 'end', overlayY: 'bottom', offsetY: -6 },
  ];

  constructor(private helpContent: HelpContentService) {}

  /** Undefined for an id the registry does not know, so the template renders nothing. */
  get prose(): HelpProse | undefined {
    return this.helpContent.entry(this.helpId) ? this.helpContent.prose(this.helpId) : undefined;
  }

  /** Help chrome labels. Never the shared ngx-translate catalogue. */
  get ui(): HelpUiStrings {
    return this.helpContent.ui();
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

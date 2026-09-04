import { Component, Input } from '@angular/core';
import { HelpEntryId, HelpProse } from '../../help.model';
import { HelpContentService } from '../../services/help-content.service';

@Component({
  selector: 'tp-help-hint',
  templateUrl: './help-hint.component.html',
  styleUrls: ['./help-hint.component.scss'],
  standalone: false,
})
export class HelpHintComponent {
  @Input() helpId!: HelpEntryId;
  @Input() tone: 'info' | 'warn' = 'info';

  constructor(private helpContent: HelpContentService) {}

  get prose(): HelpProse | undefined {
    return this.helpContent.entry(this.helpId) ? this.helpContent.prose(this.helpId) : undefined;
  }
}

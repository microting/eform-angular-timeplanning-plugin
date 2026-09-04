import { Component, Input } from '@angular/core';
import { HelpEntryChromeBase } from '../help-chrome.base';

@Component({
  selector: 'tp-help-hint',
  templateUrl: './help-hint.component.html',
  styleUrls: ['./help-hint.component.scss'],
  standalone: false,
})
export class HelpHintComponent extends HelpEntryChromeBase {
  @Input() tone: 'info' | 'warn' = 'info';
}

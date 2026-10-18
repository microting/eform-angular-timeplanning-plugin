import { Component, Input } from '@angular/core';
import { HelpEntryChromeBase } from '../help-chrome.base';
import {NgIf} from '@angular/common';
import {MatIcon} from '@angular/material/icon';

@Component({
  selector: 'tp-help-hint',
  imports: [NgIf, MatIcon],
  templateUrl: './help-hint.component.html',
  styleUrls: ['./help-hint.component.scss'],
})
export class HelpHintComponent extends HelpEntryChromeBase {
  @Input() tone: 'info' | 'warn' = 'info';
}

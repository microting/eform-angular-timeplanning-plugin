import { Component, Input, Output, EventEmitter } from '@angular/core';
import { FormGroup } from '@angular/forms';

@Component({
  selector: 'app-assigned-site-auto-break-tab',
  templateUrl: './auto-break-tab.component.html',
  standalone: false
})
export class AutoBreakTabComponent {
  @Input() autoBreakSettingsForm!: FormGroup;
  @Output() autoBreakValueSet = new EventEmitter<{ day: string, control: string, value: string }>();
  @Output() breakSettingsCopied = new EventEmitter<string>();
  
  days = ['Monday', 'Tuesday', 'Wednesday', 'Thursday', 'Friday', 'Saturday', 'Sunday'];
  
  setAutoBreakValue(day: string, control: string, value: string) {
    this.autoBreakValueSet.emit({ day, control, value });
  }
  
  copyBreakSettings(day: string) {
    this.breakSettingsCopied.emit(day);
  }
}

import { Component, Input, Output, EventEmitter } from '@angular/core';
import { FormGroup } from '@angular/forms';
import { Observable } from 'rxjs';
import { AssignedSiteModel } from '../../../../../models';

@Component({
  selector: 'app-assigned-site-shift-tab',
  templateUrl: './shift-tab.component.html',
  standalone: false
})
export class ShiftTabComponent {
  @Input() shiftForm!: FormGroup;
  @Input() data!: AssignedSiteModel;
  @Input() shiftSuffix: string = ''; // e.g., '', '2NdShift', '3RdShift', etc.
  @Input() selectCurrentUserIsAdmin$!: Observable<boolean>;
  @Output() minutesSet = new EventEmitter<{ event: any, field: string }>();
  
  days = ['Monday', 'Tuesday', 'Wednesday', 'Thursday', 'Friday', 'Saturday', 'Sunday'];
  
  setMinutes(event: any, field: string) {
    this.minutesSet.emit({ event, field });
  }
}

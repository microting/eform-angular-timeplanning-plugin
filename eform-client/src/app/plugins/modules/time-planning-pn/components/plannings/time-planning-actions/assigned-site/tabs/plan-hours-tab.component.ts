import { Component, Input } from '@angular/core';
import { FormGroup } from '@angular/forms';

@Component({
  selector: 'app-assigned-site-plan-hours-tab',
  templateUrl: './plan-hours-tab.component.html',
  standalone: false
})
export class PlanHoursTabComponent {
  @Input() planHoursForm!: FormGroup;
  
  days = ['Monday', 'Tuesday', 'Wednesday', 'Thursday', 'Friday', 'Saturday', 'Sunday'];
}

import { Component, Input } from '@angular/core';
import { FormGroup } from '@angular/forms';
import { AssignedSiteModel } from '../../../../../models';

@Component({
  selector: 'app-assigned-site-general-tab',
  templateUrl: './general-tab.component.html',
  standalone: false
})
export class GeneralTabComponent {
  @Input() data!: AssignedSiteModel;
  @Input() assignedSiteForm!: FormGroup;
  @Input() isFirstUser: boolean = false;
  @Input() isAdmin: boolean = false;
}

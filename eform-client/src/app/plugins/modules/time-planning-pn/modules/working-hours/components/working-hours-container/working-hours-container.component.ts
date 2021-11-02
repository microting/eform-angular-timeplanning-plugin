import { Component, OnInit } from '@angular/core';
import { FormArray } from '@angular/forms';

@Component({
  selector: 'app-working-hours-container',
  templateUrl: './working-hours-container.component.html',
  styleUrls: ['./working-hours-container.component.scss'],
})
export class WorkingHoursContainerComponent implements OnInit {
  workingHoursFormArray: FormArray;

  constructor() {}

  ngOnInit(): void {}
}

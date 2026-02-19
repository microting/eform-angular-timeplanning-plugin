# PayRuleSet Module - Complete Technical Specification

**Phase 2 Implementation Guide**
**Estimated Effort**: 20-25 hours
**Pattern**: Based on BreakPolicy successful implementation

---

## Table of Contents

1. [Overview](#overview)
2. [Architecture](#architecture)
3. [Module Structure](#module-structure)
4. [Component Specifications](#component-specifications)
5. [Form Management](#form-management)
6. [Business Logic](#business-logic)
7. [Integration](#integration)
8. [Testing](#testing)

---

## Overview

### Purpose

Implement complete CRUD functionality for PayRuleSet with nested PayDayRule management. PayRuleSet is the top-level container for pay rules in the system.

### Key Features

- **PayRuleSet CRUD**: Create, Read, Update, Delete PayRuleSets
- **Nested PayDayRule Management**: Manage 7 days of week rules
- **PayTierRule Integration**: Each PayDayRule can have multiple tiers
- **Validation**: Unique days, percentage sums, required fields

### Entity Relationships

```
PayRuleSet (1)
  ↓ one-to-many
PayDayRule (nested, 0-7 days)
  ↓ one-to-many
PayTierRule (nested, 1-N tiers per day)
```

---

## Architecture

### Component Hierarchy

```
PayRuleSetsModule
├── PayRuleSetsContainerComponent (smart)
│   ├── PayRuleSetsTableComponent (presentational)
│   ├── PayRuleSetsCreateModalComponent (modal)
│   │   ├── PayDayRuleListComponent
│   │   └── PayDayRuleDialogComponent
│   │       └── PayDayRuleFormComponent
│   ├── PayRuleSetsEditModalComponent (modal)
│   │   ├── PayDayRuleListComponent
│   │   └── PayDayRuleDialogComponent
│   │       └── PayDayRuleFormComponent
│   └── PayRuleSetsDeleteModalComponent (modal)
```

### Data Flow

1. **Load**: Container → Service → Table
2. **Create**: Table → Container → CreateModal → Service
3. **Edit**: Table → Container → EditModal → Service
4. **Delete**: Table → Container → DeleteModal → Service
5. **Nested**: Modal → Dialog → Form → Modal

---

## Module Structure

### File Organization

```
modules/pay-rule-sets/
├── pay-rule-sets.module.ts
├── pay-rule-sets.routing.ts
└── components/
    ├── pay-rule-sets-container/
    │   ├── pay-rule-sets-container.component.ts
    │   ├── pay-rule-sets-container.component.html
    │   └── pay-rule-sets-container.component.scss
    ├── pay-rule-sets-table/
    │   ├── pay-rule-sets-table.component.ts
    │   ├── pay-rule-sets-table.component.html
    │   └── pay-rule-sets-table.component.scss
    ├── pay-rule-sets-create-modal/
    │   ├── pay-rule-sets-create-modal.component.ts
    │   ├── pay-rule-sets-create-modal.component.html
    │   └── pay-rule-sets-create-modal.component.scss
    ├── pay-rule-sets-edit-modal/
    │   ├── pay-rule-sets-edit-modal.component.ts
    │   ├── pay-rule-sets-edit-modal.component.html
    │   └── pay-rule-sets-edit-modal.component.scss
    ├── pay-rule-sets-delete-modal/
    │   ├── pay-rule-sets-delete-modal.component.ts
    │   ├── pay-rule-sets-delete-modal.component.html
    │   └── pay-rule-sets-delete-modal.component.scss
    ├── pay-day-rule-form/
    │   ├── pay-day-rule-form.component.ts
    │   ├── pay-day-rule-form.component.html
    │   └── pay-day-rule-form.component.scss
    ├── pay-day-rule-list/
    │   ├── pay-day-rule-list.component.ts
    │   ├── pay-day-rule-list.component.html
    │   └── pay-day-rule-list.component.scss
    ├── pay-day-rule-dialog/
    │   ├── pay-day-rule-dialog.component.ts
    │   ├── pay-day-rule-dialog.component.html
    │   └── pay-day-rule-dialog.component.scss
    └── index.ts
```

### Module Configuration

```typescript
// pay-rule-sets.module.ts
import { NgModule } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule } from '@angular/forms';
import { MatDialogModule } from '@angular/material/dialog';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatIconModule } from '@angular/material/icon';
import { MatTableModule } from '@angular/material/table';
import { MatMenuModule } from '@angular/material/menu';
import { MatSelectModule } from '@angular/material/select';
import { MatTooltipModule } from '@angular/material/tooltip';
import { TranslateModule } from '@ngx-translate/core';
import { SharedPnModule } from 'src/app/plugins/modules/shared/shared-pn.module';

import { PayRuleSetsRoutingModule } from './pay-rule-sets.routing';
import {
  PayRuleSetsContainerComponent,
  PayRuleSetsTableComponent,
  PayRuleSetsCreateModalComponent,
  PayRuleSetsEditModalComponent,
  PayRuleSetsDeleteModalComponent,
  PayDayRuleFormComponent,
  PayDayRuleListComponent,
  PayDayRuleDialogComponent,
} from './components';

import { TimePlanningPnPayRuleSetsService } from '../../services';

@NgModule({
  imports: [
    CommonModule,
    ReactiveFormsModule,
    MatDialogModule,
    MatButtonModule,
    MatFormFieldModule,
    MatInputModule,
    MatIconModule,
    MatTableModule,
    MatMenuModule,
    MatSelectModule,
    MatTooltipModule,
    TranslateModule,
    SharedPnModule,
    PayRuleSetsRoutingModule,
  ],
  declarations: [
    PayRuleSetsContainerComponent,
    PayRuleSetsTableComponent,
    PayRuleSetsCreateModalComponent,
    PayRuleSetsEditModalComponent,
    PayRuleSetsDeleteModalComponent,
    PayDayRuleFormComponent,
    PayDayRuleListComponent,
    PayDayRuleDialogComponent,
  ],
  providers: [
    TimePlanningPnPayRuleSetsService,
  ],
})
export class PayRuleSetsModule {}
```

### Routing Configuration

```typescript
// pay-rule-sets.routing.ts
import { NgModule } from '@angular/core';
import { RouterModule, Routes } from '@angular/router';
import { PayRuleSetsContainerComponent } from './components';

const routes: Routes = [
  {
    path: '',
    component: PayRuleSetsContainerComponent,
  },
];

@NgModule({
  imports: [RouterModule.forChild(routes)],
  exports: [RouterModule],
})
export class PayRuleSetsRoutingModule {}
```

---

## Component Specifications

### 1. PayRuleSetsContainerComponent (Smart Component)

**Purpose**: Manage state and coordinate child components

**TypeScript**:

```typescript
import { Component, OnInit } from '@angular/core';
import { MatDialog } from '@angular/material/dialog';
import { ToastrService } from 'ngx-toastr';
import { TimePlanningPnPayRuleSetsService } from '../../../../services';
import { PayRuleSetSimpleModel, PayRuleSetsRequestModel } from '../../../../models';
import {
  PayRuleSetsCreateModalComponent,
  PayRuleSetsEditModalComponent,
  PayRuleSetsDeleteModalComponent,
} from '../index';

@Component({
  selector: 'app-pay-rule-sets-container',
  standalone: false,
  templateUrl: './pay-rule-sets-container.component.html',
  styleUrls: ['./pay-rule-sets-container.component.scss']
})
export class PayRuleSetsContainerComponent implements OnInit {
  payRuleSets: PayRuleSetSimpleModel[] = [];
  totalPayRuleSets: number = 0;
  
  constructor(
    private payRuleSetsService: TimePlanningPnPayRuleSetsService,
    private dialog: MatDialog,
    private toastr: ToastrService
  ) {}

  ngOnInit(): void {
    this.loadPayRuleSets();
  }

  loadPayRuleSets(): void {
    const request: PayRuleSetsRequestModel = {
      offset: 0,
      pageSize: 100, // or use pagination
    };

    this.payRuleSetsService.getPayRuleSets(request).subscribe({
      next: (result) => {
        if (result.success) {
          this.payRuleSets = result.model.entities;
          this.totalPayRuleSets = result.model.total;
        }
      },
      error: (error) => {
        this.toastr.error('Error loading pay rule sets');
      }
    });
  }

  onCreateClicked(): void {
    const dialogRef = this.dialog.open(PayRuleSetsCreateModalComponent, {
      width: '600px',
      disableClose: true,
    });

    dialogRef.afterClosed().subscribe((result) => {
      if (result) {
        this.loadPayRuleSets();
      }
    });
  }

  onEditClicked(payRuleSet: PayRuleSetSimpleModel): void {
    const dialogRef = this.dialog.open(PayRuleSetsEditModalComponent, {
      width: '600px',
      disableClose: true,
      data: payRuleSet.id,
    });

    dialogRef.afterClosed().subscribe((result) => {
      if (result) {
        this.loadPayRuleSets();
      }
    });
  }

  onDeleteClicked(payRuleSet: PayRuleSetSimpleModel): void {
    const dialogRef = this.dialog.open(PayRuleSetsDeleteModalComponent, {
      width: '400px',
      data: payRuleSet,
    });

    dialogRef.afterClosed().subscribe((result) => {
      if (result) {
        this.loadPayRuleSets();
      }
    });
  }
}
```

**HTML**:

```html
<div class="pay-rule-sets-container">
  <app-pay-rule-sets-table
    [payRuleSets]="payRuleSets"
    [totalPayRuleSets]="totalPayRuleSets"
    (createClicked)="onCreateClicked()"
    (editClicked)="onEditClicked($event)"
    (deleteClicked)="onDeleteClicked($event)">
  </app-pay-rule-sets-table>
</div>
```

---

### 2. PayRuleSetsTableComponent (Presentational)

**Purpose**: Display PayRuleSets list with actions

**TypeScript**:

```typescript
import { Component, Input, Output, EventEmitter } from '@angular/core';
import { PayRuleSetSimpleModel } from '../../../../models';

@Component({
  selector: 'app-pay-rule-sets-table',
  standalone: false,
  templateUrl: './pay-rule-sets-table.component.html',
  styleUrls: ['./pay-rule-sets-table.component.scss']
})
export class PayRuleSetsTableComponent {
  @Input() payRuleSets: PayRuleSetSimpleModel[] = [];
  @Input() totalPayRuleSets: number = 0;
  @Output() createClicked = new EventEmitter<void>();
  @Output() editClicked = new EventEmitter<PayRuleSetSimpleModel>();
  @Output() deleteClicked = new EventEmitter<PayRuleSetSimpleModel>();

  displayedColumns: string[] = ['name', 'actions'];

  onCreateClick(): void {
    this.createClicked.emit();
  }

  onEditClick(payRuleSet: PayRuleSetSimpleModel): void {
    this.editClicked.emit(payRuleSet);
  }

  onDeleteClick(payRuleSet: PayRuleSetSimpleModel): void {
    this.deleteClicked.emit(payRuleSet);
  }
}
```

**HTML**:

```html
<div class="table-container">
  <div class="header">
    <h2>{{ 'Pay Rule Sets' | translate }}</h2>
    <button
      mat-raised-button
      color="primary"
      (click)="onCreateClick()"
      id="createPayRuleSetBtn">
      <mat-icon>add</mat-icon>
      {{ 'Create Pay Rule Set' | translate }}
    </button>
  </div>

  <table mat-table [dataSource]="payRuleSets" class="pay-rule-sets-table">
    <!-- Name Column -->
    <ng-container matColumnDef="name">
      <th mat-header-cell *matHeaderCellDef>{{ 'Name' | translate }}</th>
      <td mat-cell *matCellDef="let payRuleSet">{{ payRuleSet.name }}</td>
    </ng-container>

    <!-- Actions Column -->
    <ng-container matColumnDef="actions">
      <th mat-header-cell *matHeaderCellDef class="actions-column">{{ 'Actions' | translate }}</th>
      <td mat-cell *matCellDef="let payRuleSet" class="actions-column">
        <ng-template #actionsTpl>
          <button mat-icon-button [matMenuTriggerFor]="menu" [id]="'payRuleSetActionsBtn_' + payRuleSet.id">
            <mat-icon>more_vert</mat-icon>
          </button>
          <mat-menu #menu="matMenu">
            <button mat-menu-item (click)="onEditClick(payRuleSet)" [id]="'editPayRuleSetBtn_' + payRuleSet.id">
              <mat-icon>edit</mat-icon>
              <span>{{ 'Edit' | translate }}</span>
            </button>
            <button mat-menu-item (click)="onDeleteClick(payRuleSet)" [id]="'deletePayRuleSetBtn_' + payRuleSet.id">
              <mat-icon>delete</mat-icon>
              <span>{{ 'Delete' | translate }}</span>
            </button>
          </mat-menu>
        </ng-template>
        <ng-container *ngTemplateOutlet="actionsTpl"></ng-container>
      </td>
    </ng-container>

    <tr mat-header-row *matHeaderRowDef="displayedColumns"></tr>
    <tr mat-row *matRowDef="let row; columns: displayedColumns;"></tr>
  </table>

  <div class="no-data" *ngIf="payRuleSets.length === 0">
    <mat-icon>info</mat-icon>
    <p>{{ 'No pay rule sets found' | translate }}</p>
  </div>
</div>
```

---

### 3. PayRuleSetsCreateModalComponent

**Purpose**: Create new PayRuleSet with nested PayDayRules

**TypeScript**:

```typescript
import { Component, OnInit } from '@angular/core';
import { FormBuilder, FormGroup, FormArray, Validators } from '@angular/forms';
import { MatDialogRef, MatDialog } from '@angular/material/dialog';
import { ToastrService } from 'ngx-toastr';
import { TimePlanningPnPayRuleSetsService } from '../../../../services';
import { PayRuleSetCreateModel, PayDayRuleModel } from '../../../../models';
import { PayDayRuleDialogComponent } from '../pay-day-rule-dialog/pay-day-rule-dialog.component';

@Component({
  selector: 'app-pay-rule-sets-create-modal',
  standalone: false,
  templateUrl: './pay-rule-sets-create-modal.component.html',
  styleUrls: ['./pay-rule-sets-create-modal.component.scss']
})
export class PayRuleSetsCreateModalComponent implements OnInit {
  form: FormGroup;

  constructor(
    private fb: FormBuilder,
    private dialogRef: MatDialogRef<PayRuleSetsCreateModalComponent>,
    private dialog: MatDialog,
    private payRuleSetsService: TimePlanningPnPayRuleSetsService,
    private toastr: ToastrService
  ) {
    this.form = this.fb.group({
      name: ['', [Validators.required, Validators.minLength(2)]],
      payDayRules: this.fb.array([]),
    });
  }

  ngOnInit(): void {}

  get payDayRules(): FormArray {
    return this.form.get('payDayRules') as FormArray;
  }

  onAddPayDayRule(): void {
    const dialogRef = this.dialog.open(PayDayRuleDialogComponent, {
      width: '500px',
      data: { mode: 'create' },
    });

    dialogRef.afterClosed().subscribe((result: PayDayRuleModel) => {
      if (result) {
        this.payDayRules.push(this.createPayDayRuleFormGroup(result));
        this.toastr.success('Pay day rule added');
      }
    });
  }

  onEditPayDayRule(index: number): void {
    const payDayRule = this.payDayRules.at(index).value;
    const dialogRef = this.dialog.open(PayDayRuleDialogComponent, {
      width: '500px',
      data: { mode: 'edit', payDayRule },
    });

    dialogRef.afterClosed().subscribe((result: PayDayRuleModel) => {
      if (result) {
        this.payDayRules.at(index).patchValue(result);
        this.toastr.success('Pay day rule updated');
      }
    });
  }

  onDeletePayDayRule(index: number): void {
    this.payDayRules.removeAt(index);
    this.toastr.success('Pay day rule removed');
  }

  createPayDayRuleFormGroup(rule: PayDayRuleModel): FormGroup {
    return this.fb.group({
      id: [rule.id || null],
      dayOfWeek: [rule.dayOfWeek, Validators.required],
      payTierRules: [rule.payTierRules || []],
    });
  }

  onSave(): void {
    if (this.form.invalid) {
      this.toastr.error('Please fill all required fields');
      return;
    }

    const model: PayRuleSetCreateModel = {
      name: this.form.value.name,
      payDayRules: this.payDayRules.value,
    };

    this.payRuleSetsService.createPayRuleSet(model).subscribe({
      next: (result) => {
        if (result.success) {
          this.toastr.success('Pay rule set created successfully');
          this.dialogRef.close(true);
        } else {
          this.toastr.error('Error creating pay rule set');
        }
      },
      error: (error) => {
        this.toastr.error('Error creating pay rule set');
      }
    });
  }

  onCancel(): void {
    this.dialogRef.close(false);
  }
}
```

**HTML**:

```html
<h2 mat-dialog-title>{{ 'Create Pay Rule Set' | translate }}</h2>

<mat-dialog-content>
  <form [formGroup]="form">
    <mat-form-field appearance="outline" class="full-width">
      <mat-label>{{ 'Name' | translate }}</mat-label>
      <input
        matInput
        formControlName="name"
        [placeholder]="'Enter pay rule set name' | translate"
        id="payRuleSetNameInput">
      <mat-error *ngIf="form.get('name')?.hasError('required')">
        {{ 'Name is required' | translate }}
      </mat-error>
      <mat-error *ngIf="form.get('name')?.hasError('minlength')">
        {{ 'Name must be at least 2 characters' | translate }}
      </mat-error>
    </mat-form-field>

    <div class="pay-day-rules-section">
      <app-pay-day-rule-list
        [payDayRulesFormArray]="payDayRules"
        (addRule)="onAddPayDayRule()"
        (editRule)="onEditPayDayRule($event)"
        (deleteRule)="onDeletePayDayRule($event)">
      </app-pay-day-rule-list>
    </div>
  </form>
</mat-dialog-content>

<mat-dialog-actions align="end">
  <button
    mat-button
    (click)="onCancel()"
    id="cancelBtn">
    {{ 'Cancel' | translate }}
  </button>
  <button
    mat-raised-button
    color="primary"
    (click)="onSave()"
    [disabled]="form.invalid"
    id="saveBtn">
    {{ 'Create' | translate }}
  </button>
</mat-dialog-actions>
```

---

### 4. PayDayRuleListComponent

**Purpose**: Display nested PayDayRules with add/edit/delete

**TypeScript**:

```typescript
import { Component, Input, Output, EventEmitter } from '@angular/core';
import { FormArray } from '@angular/forms';

@Component({
  selector: 'app-pay-day-rule-list',
  standalone: false,
  templateUrl: './pay-day-rule-list.component.html',
  styleUrls: ['./pay-day-rule-list.component.scss']
})
export class PayDayRuleListComponent {
  @Input() payDayRulesFormArray!: FormArray;
  @Output() addRule = new EventEmitter<void>();
  @Output() editRule = new EventEmitter<number>();
  @Output() deleteRule = new EventEmitter<number>();

  dayNames: string[] = ['Sunday', 'Monday', 'Tuesday', 'Wednesday', 'Thursday', 'Friday', 'Saturday'];

  getDayName(dayOfWeek: number): string {
    return this.dayNames[dayOfWeek] || 'Unknown';
  }

  getTiersSummary(payTierRules: any[]): string {
    if (!payTierRules || payTierRules.length === 0) {
      return 'No tiers';
    }
    return `${payTierRules.length} tier(s)`;
  }

  onAddClick(): void {
    this.addRule.emit();
  }

  onEditClick(index: number): void {
    this.editRule.emit(index);
  }

  onDeleteClick(index: number): void {
    this.deleteRule.emit(index);
  }
}
```

**HTML**:

```html
<div class="pay-day-rules-list">
  <div class="header">
    <h3>{{ 'Pay Day Rules' | translate }}</h3>
    <button
      mat-raised-button
      color="accent"
      (click)="onAddClick()"
      id="addPayDayRuleBtn">
      <mat-icon>add</mat-icon>
      {{ 'Add Day Rule' | translate }}
    </button>
  </div>

  <div class="rules-container" *ngIf="payDayRulesFormArray.length > 0">
    <table class="rules-table">
      <thead>
        <tr>
          <th>{{ 'Day of Week' | translate }}</th>
          <th>{{ 'Tiers' | translate }}</th>
          <th>{{ 'Actions' | translate }}</th>
        </tr>
      </thead>
      <tbody>
        <tr *ngFor="let rule of payDayRulesFormArray.controls; let i = index">
          <td>{{ getDayName(rule.value.dayOfWeek) | translate }}</td>
          <td>{{ getTiersSummary(rule.value.payTierRules) }}</td>
          <td class="actions-cell">
            <button
              mat-icon-button
              (click)="onEditClick(i)"
              matTooltip="{{ 'Edit' | translate }}"
              [id]="'editPayDayRuleBtn_' + i">
              <mat-icon>edit</mat-icon>
            </button>
            <button
              mat-icon-button
              (click)="onDeleteClick(i)"
              matTooltip="{{ 'Delete' | translate }}"
              [id]="'deletePayDayRuleBtn_' + i">
              <mat-icon>delete</mat-icon>
            </button>
          </td>
        </tr>
      </tbody>
    </table>
  </div>

  <div class="empty-state" *ngIf="payDayRules FormArray.length === 0">
    <mat-icon>info</mat-icon>
    <p>{{ 'No pay day rules added yet' | translate }}</p>
    <p class="hint">{{ 'Click "Add Day Rule" to add rules for different days of the week' | translate }}</p>
  </div>
</div>
```

---

## Form Management

### PayRuleSet Form Structure

```typescript
{
  name: string,              // Required, min 2 chars
  payDayRules: [            // FormArray
    {
      id: number | null,
      dayOfWeek: number,    // 0-6 (Sun-Sat), unique
      payTierRules: [       // Array of PayTierRuleModel
        {
          id: number | null,
          tierNumber: number,
          tierPercent: number,  // Must sum to 100% per day
          payCodeId: number
        }
      ]
    }
  ]
}
```

### Validation Rules

1. **PayRuleSet Level**:
   - name required (min 2 chars)
   - payDayRules can be empty (0-7 days)

2. **PayDayRule Level**:
   - dayOfWeek required (0-6)
   - dayOfWeek must be unique within PayRuleSet
   - payTierRules required (at least 1 tier)

3. **PayTierRule Level**:
   - tierNumber required (unique per day)
   - tierPercent required (0-100)
   - Sum of tierPercent must equal 100% per day
   - payCodeId required

---

## Business Logic

### Day of Week Validation

```typescript
function validateUniqueDayOfWeek(payDayRules: PayDayRuleModel[]): boolean {
  const days = payDayRules.map(r => r.dayOfWeek);
  return days.length === new Set(days).size;
}
```

### Tier Percentage Validation

```typescript
function validateTierPercentages(payTierRules: PayTierRuleModel[]): boolean {
  const sum = payTierRules.reduce((total, tier) => total + tier.tierPercent, 0);
  return Math.abs(sum - 100) < 0.01; // Allow small float precision errors
}
```

---

## Integration

### Add to Main Module

```typescript
// time-planning-pn.module.ts
{
  path: 'pay-rule-sets',
  loadChildren: () =>
    import('./modules/pay-rule-sets/pay-rule-sets.module').then(
      (m) => m.PayRuleSetsModule
    ),
}
```

### Add Menu Entry

```csharp
// EformTimePlanningPlugin.cs
new()
{
    Name = "Pay Rule Sets",
    E2EId = "time-planning-pn-pay-rule-sets",
    Link = "/plugins/time-planning-pn/pay-rule-sets",
    Type = MenuItemTypeEnum.Link,
    Position = 6,
    MenuTemplate = new()
    {
        Translations = [
            new() { LocaleName = LocaleNames.English, Name = "Pay Rule Sets" },
            new() { LocaleName = LocaleNames.Danish, Name = "Lønregler sæt" },
            new() { LocaleName = LocaleNames.German, Name = "Lohnregelsätze" }
        ]
    }
}
```

---

## Testing

### Unit Testing Focus

- Form validation (unique days, tier percentages)
- Component logic (add/edit/delete rules)
- Service integration (API calls)

### E2E Testing Scenarios

1. Create PayRuleSet with no days
2. Create PayRuleSet with 1-7 days
3. Add tiers to each day (sum to 100%)
4. Edit PayRuleSet and modify days
5. Delete PayRuleSet
6. Validate unique day restriction
7. Validate tier percentage sum

---

## Estimated Implementation Time

- **Module setup**: 1-2 hours
- **Container & Table**: 2-3 hours
- **Delete Modal**: 1 hour
- **PayDayRule components**: 4-5 hours
- **Create Modal**: 3-4 hours
- **Edit Modal**: 3-4 hours
- **Integration**: 2-3 hours
- **Testing & Polish**: 3-4 hours

**Total: 20-25 hours**

---

## Success Criteria

- ✅ Can create PayRuleSet with nested days
- ✅ Can edit PayRuleSet and modify days
- ✅ Can delete PayRuleSet
- ✅ Validation prevents duplicate days
- ✅ Validation ensures tier percentages sum to 100%
- ✅ UI follows Material Design
- ✅ Follows BreakPolicy pattern
- ✅ All CRUD operations work
- ✅ No console errors

---

**Status**: Ready for implementation
**Pattern**: Proven (based on BreakPolicy)
**Complexity**: High (nested entities, multiple validations)
**Priority**: High (foundational module for others)

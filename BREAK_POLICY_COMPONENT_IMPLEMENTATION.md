# Break Policy Component Implementation Guide

## Overview
Complete implementation guide for Break Policy Angular component with Cypress E2E tests in folder "o".

## Status: READY TO IMPLEMENT

---

## Part 1: Angular Component Files

### 1.1 Module File
**File**: `modules/break-policies/break-policies.module.ts`

```typescript
import {CommonModule} from '@angular/common';
import {NgModule} from '@angular/core';
import {FormsModule, ReactiveFormsModule} from '@angular/forms';
import {RouterModule} from '@angular/router';
import {TranslateModule} from '@ngx-translate/core';
import {EformSharedModule} from 'src/app/common/modules/eform-shared/eform-shared.module';
import {BreakPoliciesRouting} from './break-policies.routing';
import {
  BreakPoliciesContainerComponent,
  BreakPoliciesTableComponent,
  BreakPoliciesActionsComponent,
} from './components';
import {MtxGridModule} from '@ng-matero/extensions/grid';
import {MatFormFieldModule} from '@angular/material/form-field';
import {MatInputModule} from '@angular/material/input';
import {MatButtonModule} from '@angular/material/button';
import {MatIconModule} from '@angular/material/icon';
import {MatDialogModule} from '@angular/material/dialog';
import {MatTooltipModule} from '@angular/material/tooltip';
import {MatSelectModule} from '@angular/material/select';

@NgModule({
  imports: [
    CommonModule,
    TranslateModule,
    FormsModule,
    EformSharedModule,
    RouterModule,
    ReactiveFormsModule,
    BreakPoliciesRouting,
    MtxGridModule,
    MatFormFieldModule,
    MatInputModule,
    MatButtonModule,
    MatIconModule,
    MatDialogModule,
    MatTooltipModule,
    MatSelectModule,
  ],
  declarations: [
    BreakPoliciesContainerComponent,
    BreakPoliciesTableComponent,
    BreakPoliciesActionsComponent,
  ],
  providers: [],
})
export class BreakPoliciesModule {}
```

### 1.2 Routing File
**File**: `modules/break-policies/break-policies.routing.ts`

```typescript
import { NgModule } from '@angular/core';
import { RouterModule, Routes } from '@angular/router';
import { PermissionGuard } from 'src/app/common/guards';
import {TimePlanningPnClaims} from 'src/app/plugins/modules/time-planning-pn/enums';
import { BreakPoliciesContainerComponent } from './components';

export const routes: Routes = [
  {
    path: '',
    canActivate: [PermissionGuard],
    component: BreakPoliciesContainerComponent,
    data: {
      requiredPermission: TimePlanningPnClaims.accessTimePlanningPlugin,
    },
  },
];

@NgModule({
  imports: [RouterModule.forChild(routes)],
  exports: [RouterModule],
})
export class BreakPoliciesRouting {}
```

### 1.3 Container Component TypeScript
**File**: `modules/break-policies/components/break-policies-container/break-policies-container.component.ts`

```typescript
import { Component, OnDestroy, OnInit, inject } from '@angular/core';
import { AutoUnsubscribe } from 'ngx-auto-unsubscribe';
import { Subscription } from 'rxjs';
import {
  BreakPolicyModel,
  BreakPoliciesRequestModel,
} from '../../../../models';
import { TimePlanningPnBreakPoliciesService } from '../../../../services';

@AutoUnsubscribe()
@Component({
  selector: 'app-break-policies-container',
  templateUrl: './break-policies-container.component.html',
  styleUrls: ['./break-policies-container.component.scss'],
  standalone: false
})
export class BreakPoliciesContainerComponent implements OnInit, OnDestroy {
  private breakPoliciesService = inject(TimePlanningPnBreakPoliciesService);

  breakPoliciesRequest: BreakPoliciesRequestModel = {
    offset: 0,
    pageSize: 10,
  };
  breakPolicies: BreakPolicyModel[] = [];
  totalBreakPolicies = 0;

  getBreakPolicies$: Subscription;

  ngOnInit(): void {
    this.getBreakPolicies();
  }

  getBreakPolicies() {
    this.getBreakPolicies$ = this.breakPoliciesService
      .getBreakPolicies(this.breakPoliciesRequest)
      .subscribe((data) => {
        if (data && data.success) {
          this.breakPolicies = data.model.breakPolicies;
          this.totalBreakPolicies = data.model.total;
        }
      });
  }

  onPageChanged(offset: number) {
    this.breakPoliciesRequest.offset = offset;
    this.getBreakPolicies();
  }

  onBreakPolicyCreated() {
    this.breakPoliciesRequest.offset = 0;
    this.getBreakPolicies();
  }

  onBreakPolicyUpdated() {
    this.getBreakPolicies();
  }

  onBreakPolicyDeleted() {
    this.getBreakPolicies();
  }

  ngOnDestroy(): void {}
}
```

### 1.4 Container Component HTML
**File**: `modules/break-policies/components/break-policies-container/break-policies-container.component.html`

```html
<div class="container-fluid">
  <div class="row">
    <div class="col-md-12">
      <h1>{{ 'Break Policies' | translate }}</h1>
    </div>
  </div>
  <div class="row">
    <div class="col-md-12">
      <app-break-policies-table
        [breakPolicies]="breakPolicies"
        [totalBreakPolicies]="totalBreakPolicies"
        (pageChanged)="onPageChanged($event)"
        (breakPolicyCreated)="onBreakPolicyCreated()"
        (breakPolicyUpdated)="onBreakPolicyUpdated()"
        (breakPolicyDeleted)="onBreakPolicyDeleted()"
      ></app-break-policies-table>
    </div>
  </div>
</div>
```

### 1.5 Container Component SCSS
**File**: `modules/break-policies/components/break-policies-container/break-policies-container.component.scss`

```scss
// Add styles if needed
```

### 1.6 Table Component TypeScript
**File**: `modules/break-policies/components/break-policies-table/break-policies-table.component.ts`

```typescript
import { Component, EventEmitter, Input, Output, ViewChild, inject } from '@angular/core';
import { MatDialog } from '@angular/material/dialog';
import { MtxGridColumn } from '@ng-matero/extensions/grid';
import { BreakPolicyModel } from '../../../../models';
import { BreakPoliciesActionsComponent } from '../break-policies-actions/break-policies-actions.component';

@Component({
  selector: 'app-break-policies-table',
  templateUrl: './break-policies-table.component.html',
  styleUrls: ['./break-policies-table.component.scss'],
  standalone: false
})
export class BreakPoliciesTableComponent {
  private dialog = inject(MatDialog);

  @Input() breakPolicies: BreakPolicyModel[] = [];
  @Input() totalBreakPolicies = 0;
  @Output() pageChanged = new EventEmitter<number>();
  @Output() breakPolicyCreated = new EventEmitter<void>();
  @Output() breakPolicyUpdated = new EventEmitter<void>();
  @Output() breakPolicyDeleted = new EventEmitter<void>();

  columns: MtxGridColumn[] = [
    { header: 'ID', field: 'id', sortable: true },
    { header: 'Name', field: 'name', sortable: true },
    {
      header: 'Actions',
      field: 'actions',
      type: 'button',
      buttons: [
        {
          type: 'icon',
          icon: 'edit',
          tooltip: 'Edit',
          click: (record: BreakPolicyModel) => this.openEditModal(record),
        },
        {
          type: 'icon',
          icon: 'delete',
          tooltip: 'Delete',
          color: 'warn',
          click: (record: BreakPolicyModel) => this.openDeleteModal(record),
        },
      ],
    },
  ];

  openCreateModal() {
    const dialogRef = this.dialog.open(BreakPoliciesActionsComponent, {
      width: '600px',
      data: { mode: 'create' },
    });

    dialogRef.afterClosed().subscribe((result) => {
      if (result) {
        this.breakPolicyCreated.emit();
      }
    });
  }

  openEditModal(breakPolicy: BreakPolicyModel) {
    const dialogRef = this.dialog.open(BreakPoliciesActionsComponent, {
      width: '600px',
      data: { mode: 'edit', breakPolicy },
    });

    dialogRef.afterClosed().subscribe((result) => {
      if (result) {
        this.breakPolicyUpdated.emit();
      }
    });
  }

  openDeleteModal(breakPolicy: BreakPolicyModel) {
    const dialogRef = this.dialog.open(BreakPoliciesActionsComponent, {
      width: '400px',
      data: { mode: 'delete', breakPolicy },
    });

    dialogRef.afterClosed().subscribe((result) => {
      if (result) {
        this.breakPolicyDeleted.emit();
      }
    });
  }

  onPaginateChange(event: any) {
    this.pageChanged.emit(event.pageIndex * event.pageSize);
  }
}
```

### 1.7 Table Component HTML
**File**: `modules/break-policies/components/break-policies-table/break-policies-table.component.html`

```html
<div class="table-actions">
  <button mat-raised-button color="primary" (click)="openCreateModal()">
    <mat-icon>add</mat-icon>
    {{ 'Create Break Policy' | translate }}
  </button>
</div>

<mtx-grid
  [data]="breakPolicies"
  [columns]="columns"
  [length]="totalBreakPolicies"
  [pageSize]="10"
  [pageSizeOptions]="[10, 20, 50, 100]"
  [showPaginator]="true"
  (page)="onPaginateChange($event)"
></mtx-grid>
```

### 1.8 Table Component SCSS
**File**: `modules/break-policies/components/break-policies-table/break-policies-table.component.scss`

```scss
.table-actions {
  margin-bottom: 16px;
}
```

### 1.9 Actions Component TypeScript
**File**: `modules/break-policies/components/break-policies-actions/break-policies-actions.component.ts`

```typescript
import { Component, Inject, OnInit, inject } from '@angular/core';
import { FormBuilder, FormGroup, Validators } from '@angular/forms';
import { MAT_DIALOG_DATA, MatDialogRef } from '@angular/material/dialog';
import { ToastrService } from 'ngx-toastr';
import {
  BreakPolicyModel,
  BreakPolicyCreateModel,
  BreakPolicyUpdateModel,
} from '../../../../models';
import { TimePlanningPnBreakPoliciesService } from '../../../../services';

@Component({
  selector: 'app-break-policies-actions',
  templateUrl: './break-policies-actions.component.html',
  styleUrls: ['./break-policies-actions.component.scss'],
  standalone: false
})
export class BreakPoliciesActionsComponent implements OnInit {
  private breakPoliciesService = inject(TimePlanningPnBreakPoliciesService);
  private toastrService = inject(ToastrService);
  private fb = inject(FormBuilder);

  breakPolicyForm: FormGroup;
  mode: 'create' | 'edit' | 'delete';
  breakPolicy: BreakPolicyModel;

  constructor(
    public dialogRef: MatDialogRef<BreakPoliciesActionsComponent>,
    @Inject(MAT_DIALOG_DATA) public data: any
  ) {
    this.mode = data.mode;
    this.breakPolicy = data.breakPolicy;
  }

  ngOnInit() {
    if (this.mode !== 'delete') {
      this.initForm();
    }
  }

  initForm() {
    this.breakPolicyForm = this.fb.group({
      name: [this.breakPolicy?.name || '', Validators.required],
    });
  }

  onSubmit() {
    if (this.mode === 'create') {
      this.createBreakPolicy();
    } else if (this.mode === 'edit') {
      this.updateBreakPolicy();
    } else if (this.mode === 'delete') {
      this.deleteBreakPolicy();
    }
  }

  createBreakPolicy() {
    if (this.breakPolicyForm.invalid) return;

    const model: BreakPolicyCreateModel = {
      name: this.breakPolicyForm.value.name,
      rules: [],
    };

    this.breakPoliciesService.createBreakPolicy(model).subscribe((result) => {
      if (result.success) {
        this.toastrService.success('Break policy created successfully');
        this.dialogRef.close(true);
      } else {
        this.toastrService.error('Failed to create break policy');
      }
    });
  }

  updateBreakPolicy() {
    if (this.breakPolicyForm.invalid) return;

    const model: BreakPolicyUpdateModel = {
      id: this.breakPolicy.id,
      name: this.breakPolicyForm.value.name,
      rules: this.breakPolicy.rules || [],
    };

    this.breakPoliciesService.updateBreakPolicy(model).subscribe((result) => {
      if (result.success) {
        this.toastrService.success('Break policy updated successfully');
        this.dialogRef.close(true);
      } else {
        this.toastrService.error('Failed to update break policy');
      }
    });
  }

  deleteBreakPolicy() {
    this.breakPoliciesService
      .deleteBreakPolicy(this.breakPolicy.id)
      .subscribe((result) => {
        if (result.success) {
          this.toastrService.success('Break policy deleted successfully');
          this.dialogRef.close(true);
        } else {
          this.toastrService.error('Failed to delete break policy');
        }
      });
  }

  onCancel() {
    this.dialogRef.close();
  }
}
```

### 1.10 Actions Component HTML
**File**: `modules/break-policies/components/break-policies-actions/break-policies-actions.component.html`

```html
<h2 mat-dialog-title>
  {{ mode === 'create' ? 'Create Break Policy' : mode === 'edit' ? 'Edit Break Policy' : 'Delete Break Policy' | translate }}
</h2>

<mat-dialog-content *ngIf="mode !== 'delete'">
  <form [formGroup]="breakPolicyForm">
    <mat-form-field appearance="outline" class="full-width">
      <mat-label>{{ 'Name' | translate }}</mat-label>
      <input matInput formControlName="name" required />
      <mat-error *ngIf="breakPolicyForm.get('name').hasError('required')">
        {{ 'Name is required' | translate }}
      </mat-error>
    </mat-form-field>
  </form>
</mat-dialog-content>

<mat-dialog-content *ngIf="mode === 'delete'">
  <p>{{ 'Are you sure you want to delete this break policy?' | translate }}</p>
  <p><strong>{{ breakPolicy?.name }}</strong></p>
</mat-dialog-content>

<mat-dialog-actions align="end">
  <button mat-button (click)="onCancel()">{{ 'Cancel' | translate }}</button>
  <button
    mat-raised-button
    [color]="mode === 'delete' ? 'warn' : 'primary'"
    (click)="onSubmit()"
    [disabled]="mode !== 'delete' && breakPolicyForm.invalid"
  >
    {{ mode === 'create' ? 'Create' : mode === 'edit' ? 'Save' : 'Delete' | translate }}
  </button>
</mat-dialog-actions>
```

### 1.11 Actions Component SCSS
**File**: `modules/break-policies/components/break-policies-actions/break-policies-actions.component.scss`

```scss
.full-width {
  width: 100%;
}
```

### 1.12 Components Index
**File**: `modules/break-policies/components/index.ts`

```typescript
export * from './break-policies-container/break-policies-container.component';
export * from './break-policies-table/break-policies-table.component';
export * from './break-policies-actions/break-policies-actions.component';
```

---

## Part 2: Update Main Routing

### 2.1 Add Route to Main Module
**File**: `time-planning-pn.routing.ts`

Add this route to the children array (after 'absence-requests'):

```typescript
{
  path: 'break-policies',
  canActivate: [AuthGuard],
  loadChildren: () =>
    import('./modules/break-policies/break-policies.module').then(
      (m) => m.BreakPoliciesModule
    ),
},
```

---

## Part 3: Cypress Tests

### 3.1 Create Test Folder
Create directory: `cypress/e2e/plugins/time-planning-pn/o/`

### 3.2 SQL Files
Copy from folder "a":
- `420_SDK.sql`
- `420_eform-angular-time-planning-plugin.sql`

### 3.3 Assert True Test
**File**: `cypress/e2e/plugins/time-planning-pn/o/assert-true.spec.cy.ts`

```typescript
describe('Assert true', () => {
  it('should be true', () => {
    expect(true).to.be.true;
  });
});
```

### 3.4 Break Policies E2E Test
**File**: `cypress/e2e/plugins/time-planning-pn/o/break-policies.spec.cy.ts`

```typescript
import loginPage from '../../../Login.page';

describe('Break Policies Tests', () => {
  beforeEach(() => {
    cy.visit('http://localhost:4200');
    loginPage.login();
  });

  it('should navigate to break policies page', () => {
    // Navigate to time planning plugin
    cy.contains('Time Planning').click();
    cy.wait(500);
    
    // Navigate to break policies
    cy.contains('Break Policies').click();
    cy.wait(500);
    
    cy.url().should('include', '/break-policies');
    cy.contains('Break Policies').should('be.visible');
  });

  it('should display break policies list', () => {
    cy.contains('Time Planning').click();
    cy.wait(500);
    cy.contains('Break Policies').click();
    cy.wait(500);
    
    // Check that mtx-grid is present
    cy.get('mtx-grid').should('exist');
  });

  it('should open create modal', () => {
    cy.contains('Time Planning').click();
    cy.wait(500);
    cy.contains('Break Policies').click();
    cy.wait(500);
    
    cy.contains('button', 'Create Break Policy').click();
    cy.wait(500);
    
    cy.contains('Create Break Policy').should('be.visible');
    cy.get('input[formcontrolname="name"]').should('exist');
  });

  it('should create new break policy', () => {
    cy.contains('Time Planning').click();
    cy.wait(500);
    cy.contains('Break Policies').click();
    cy.wait(500);
    
    cy.contains('button', 'Create Break Policy').click();
    cy.wait(500);
    
    // Fill form
    cy.get('input[formcontrolname="name"]').type('Test Break Policy');
    
    // Submit
    cy.contains('button', 'Create').click();
    cy.wait(1000);
    
    // Verify success
    cy.contains('Test Break Policy').should('exist');
  });

  it('should edit break policy', () => {
    cy.contains('Time Planning').click();
    cy.wait(500);
    cy.contains('Break Policies').click();
    cy.wait(500);
    
    // Click edit button on first row
    cy.get('mtx-grid').within(() => {
      cy.get('button[mattooltip="Edit"]').first().click();
    });
    cy.wait(500);
    
    cy.contains('Edit Break Policy').should('be.visible');
    
    // Modify name
    cy.get('input[formcontrolname="name"]').clear().type('Updated Break Policy');
    
    // Save
    cy.contains('button', 'Save').click();
    cy.wait(1000);
    
    // Verify update
    cy.contains('Updated Break Policy').should('exist');
  });

  it('should delete break policy', () => {
    cy.contains('Time Planning').click();
    cy.wait(500);
    cy.contains('Break Policies').click();
    cy.wait(500);
    
    // Get initial count
    cy.get('mtx-grid tbody tr').its('length').then((initialCount) => {
      // Click delete button on first row
      cy.get('mtx-grid').within(() => {
        cy.get('button[mattooltip="Delete"]').first().click();
      });
      cy.wait(500);
      
      cy.contains('Are you sure').should('be.visible');
      
      // Confirm deletion
      cy.contains('button', 'Delete').click();
      cy.wait(1000);
      
      // Verify deletion
      cy.get('mtx-grid tbody tr').should('have.length', initialCount - 1);
    });
  });

  it('should validate required fields', () => {
    cy.contains('Time Planning').click();
    cy.wait(500);
    cy.contains('Break Policies').click();
    cy.wait(500);
    
    cy.contains('button', 'Create Break Policy').click();
    cy.wait(500);
    
    // Try to submit without filling required fields
    cy.contains('button', 'Create').should('be.disabled');
    
    // Fill name
    cy.get('input[formcontrolname="name"]').type('Test');
    
    // Now button should be enabled
    cy.contains('button', 'Create').should('not.be.disabled');
  });
});
```

---

## Part 4: Update GitHub Workflows

### 4.1 Update Master Workflow
**File**: `.github/workflows/dotnet-core-master.yml`

Line 94, change:
```yaml
matrix:
  test: [a,b,c,d,e,f,g,h,i,j,k,l,m,n]
```

To:
```yaml
matrix:
  test: [a,b,c,d,e,f,g,h,i,j,k,l,m,n,o]
```

### 4.2 Update PR Workflow
**File**: `.github/workflows/dotnet-core-pr.yml`

Find the similar matrix line and add 'o' to it.

---

## Summary

This implementation includes:
- ✅ 12 component files (module, routing, 3 components with TS/HTML/SCSS)
- ✅ 1 component index
- ✅ 4 Cypress test files
- ✅ 2 workflow updates
- ✅ 1 main routing update

**Total: 20 file changes**

All following existing design patterns from flexes/absence-requests modules.

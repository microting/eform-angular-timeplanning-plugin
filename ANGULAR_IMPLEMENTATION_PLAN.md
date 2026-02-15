# Angular Implementation Plan for Rule Engine UI

## Executive Summary

This document provides a complete implementation plan for Angular frontend components to support the 5 new API controllers:

1. **BreakPolicy** - Break policy management
2. **PayRuleSet** - Pay rule set configuration
3. **PayDayTypeRule** - Day type rules
4. **PayTierRule** - Tier-based pay rules
5. **PayTimeBandRule** - Time band rules

**Estimated Effort**: 80-100 hours total  
**Prerequisites**: All backend APIs implemented and tested ✅  
**Status**: Ready for implementation

---

## Architecture Analysis

### Existing Patterns Identified

After analyzing the `eform-client` folder, the following patterns are established:

#### 1. Module Structure

```
src/app/plugins/modules/time-planning-pn/
├── components/          # Shared components
├── modules/            # Feature modules
│   ├── {entity}/
│   │   ├── components/
│   │   │   ├── {entity}-container/
│   │   │   ├── {entity}-table/
│   │   │   └── {entity}-actions/
│   │   ├── {entity}.module.ts
│   │   └── {entity}.routing.ts
├── models/             # TypeScript models
├── services/           # API services
├── i18n/              # Translations
├── enums/             # Enums
└── consts/            # Constants
```

#### 2. Service Pattern

**Location**: `src/app/plugins/modules/time-planning-pn/services/`

**Pattern**:
```typescript
@Injectable({ providedIn: 'root' })
export class TimePlanningPn{Entity}Service {
  constructor(private apiBaseService: ApiBaseService) {}
  
  // CRUD methods returning Observable<OperationDataResult<T>>
  getAll(model: RequestModel): Observable<OperationDataResult<Model[]>>
  getById(id: number): Observable<OperationDataResult<Model>>
  create(model: CreateModel): Observable<OperationResult>
  update(model: UpdateModel): Observable<OperationResult>
  delete(id: number): Observable<OperationResult>
}
```

**API Method Constants**:
```typescript
export let TimePlanningPn{Entity}Methods = {
  Index: 'api/time-planning-pn/{entity}/index',
  Get: 'api/time-planning-pn/{entity}',
  // etc.
};
```

#### 3. Component Pattern

**Container Component** (Smart):
- Service injection
- Data fetching
- State management
- Event handling

**Table Component** (Presentational):
- Uses `mtx-grid` from `@ng-matero/extensions`
- Input: data array
- Output: events (edit, delete, etc.)
- Column definitions with templates

**Modal Components**:
- Material Dialog (`MatDialog`)
- Form validation
- Reactive forms
- Data binding

#### 4. Model Pattern

**Location**: `src/app/plugins/modules/time-planning-pn/models/{entity}/`

**Files per entity**:
- `{entity}.model.ts` - Full model
- `{entity}-create.model.ts` - Create DTO
- `{entity}-update.model.ts` - Update DTO
- `{entity}-request.model.ts` - List request with paging
- `{entity}-list.model.ts` - List response with pagination
- `index.ts` - Barrel export

---

## Implementation Plan by Entity

### 1. Break Policy Module

#### 1.1 Models (`models/break-policy/`)

Create 6 model files:

**break-policy.model.ts**:
```typescript
export interface BreakPolicyModel {
  id: number;
  name: string;
  planningWorkdayCode: string;
  rules: BreakPolicyRuleModel[];
  createdAt: Date;
  updatedAt: Date;
}

export interface BreakPolicyRuleModel {
  id: number;
  breakPolicyId: number;
  dayOfWeek: number; // 0-6
  paidBreakSeconds: number;
  unpaidBreakSeconds: number;
}
```

**break-policy-create.model.ts**:
```typescript
export interface BreakPolicyCreateModel {
  name: string;
  planningWorkdayCode: string;
  rules: BreakPolicyRuleCreateModel[];
}
```

**break-policy-update.model.ts**:
```typescript
export interface BreakPolicyUpdateModel {
  id: number;
  name: string;
  planningWorkdayCode: string;
  rules: BreakPolicyRuleUpdateModel[];
}
```

**break-policy-request.model.ts**:
```typescript
export interface BreakPolicyRequestModel {
  offset: number;
  pageSize: number;
  sort?: string;
  isSortDsc?: boolean;
}
```

**break-policy-list.model.ts**:
```typescript
export interface BreakPolicyListModel {
  total: number;
  breakPolicies: BreakPolicySimpleModel[];
}

export interface BreakPolicySimpleModel {
  id: number;
  name: string;
}
```

**index.ts**: Export all models

#### 1.2 Service (`services/time-planning-pn-break-policies.service.ts`)

```typescript
export let TimePlanningPnBreakPoliciesMethods = {
  Index: 'api/time-planning-pn/break-policies',
  BreakPolicies: 'api/time-planning-pn/break-policies',
};

@Injectable({ providedIn: 'root' })
export class TimePlanningPnBreakPoliciesService {
  constructor(private apiBaseService: ApiBaseService) {}

  getAll(model: BreakPolicyRequestModel): Observable<OperationDataResult<BreakPolicyListModel>> {
    return this.apiBaseService.get(TimePlanningPnBreakPoliciesMethods.Index, model);
  }

  getById(id: number): Observable<OperationDataResult<BreakPolicyModel>> {
    return this.apiBaseService.get(`${TimePlanningPnBreakPoliciesMethods.BreakPolicies}/${id}`);
  }

  create(model: BreakPolicyCreateModel): Observable<OperationResult> {
    return this.apiBaseService.post(TimePlanningPnBreakPoliciesMethods.BreakPolicies, model);
  }

  update(model: BreakPolicyUpdateModel): Observable<OperationResult> {
    return this.apiBaseService.put(`${TimePlanningPnBreakPoliciesMethods.BreakPolicies}/${model.id}`, model);
  }

  delete(id: number): Observable<OperationResult> {
    return this.apiBaseService.delete(`${TimePlanningPnBreakPoliciesMethods.BreakPolicies}/${id}`);
  }
}
```

#### 1.3 Components

**Container Component** (`break-policies-container.component.ts`):
- Manages state
- Fetches data
- Handles CRUD operations
- Pagination logic

**Table Component** (`break-policies-table.component.ts`):
- Displays data with mtx-grid
- Column definitions
- Action buttons (edit, delete)
- Event emitters

**Modal Components**:
1. `break-policy-create-modal.component` - Create dialog
2. `break-policy-edit-modal.component` - Edit dialog
3. `break-policy-delete-modal.component` - Delete confirmation
4. `break-policy-rules-edit.component` - Weekday rules editor (nested)

#### 1.4 Module Configuration

**break-policies.module.ts**:
```typescript
@NgModule({
  imports: [
    CommonModule,
    TranslateModule,
    FormsModule,
    ReactiveFormsModule,
    EformSharedModule,
    RouterModule,
    BreakPoliciesRouting,
    MtxGridModule,
    MatFormFieldModule,
    MatInputModule,
    MatButtonModule,
    MatIconModule,
    MatDialogModule,
    MatSelectModule,
    MatTooltipModule,
  ],
  declarations: [
    BreakPoliciesContainerComponent,
    BreakPoliciesTableComponent,
    BreakPolicyCreateModalComponent,
    BreakPolicyEditModalComponent,
    BreakPolicyDeleteModalComponent,
    BreakPolicyRulesEditComponent,
  ],
})
export class BreakPoliciesModule {}
```

**break-policies.routing.ts**:
```typescript
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
```

#### 1.5 Add Route to Main Module

**time-planning-pn.routing.ts**:
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

### 2. Pay Rule Set Module

#### 2.1 Models (`models/pay-rule-set/`)

**pay-rule-set.model.ts**:
```typescript
export interface PayRuleSetModel {
  id: number;
  name: string;
  payDayRules: PayDayRuleModel[];
  createdAt: Date;
  updatedAt: Date;
}

export interface PayDayRuleModel {
  id: number;
  payRuleSetId: number;
  dayTypeCode: string; // SUNDAY, SATURDAY, WEEKDAY, HOLIDAY, etc.
  payTiers: PayTierRuleModel[];
}

export interface PayTierRuleModel {
  id: number;
  payDayRuleId: number;
  order: number;
  upToSeconds: number | null;
  payCode: string;
}
```

**pay-rule-set-create.model.ts**, **pay-rule-set-update.model.ts**, etc.

#### 2.2 Service

Similar to BreakPolicy service pattern.

#### 2.3 Components

**Key complexity**: Nested editing of PayDayRules with PayTierRules

Components needed:
1. `pay-rule-sets-container.component`
2. `pay-rule-sets-table.component`
3. `pay-rule-set-create-modal.component`
4. `pay-rule-set-edit-modal.component`
5. `pay-rule-set-delete-modal.component`
6. `pay-day-rules-editor.component` - Nested editor for day rules
7. `pay-tier-rules-editor.component` - Nested editor for tier rules

---

### 3. Pay Day Type Rule Module

#### 3.1 Models (`models/pay-day-type-rule/`)

**pay-day-type-rule.model.ts**:
```typescript
export interface PayDayTypeRuleModel {
  id: number;
  payRuleSetId: number;
  dayType: string; // "Weekday", "Weekend", "Holiday"
  timeBands: PayTimeBandRuleModel[];
}

export interface PayTimeBandRuleModel {
  id: number;
  payDayTypeRuleId: number;
  startSecondOfDay: number;
  endSecondOfDay: number;
  payCode: string;
}
```

#### 3.2 Service

Follow standard pattern.

#### 3.3 Components

1. `pay-day-type-rules-container.component`
2. `pay-day-type-rules-table.component`
3. `pay-day-type-rule-create-modal.component`
4. `pay-day-type-rule-edit-modal.component`
5. `pay-day-type-rule-delete-modal.component`
6. `pay-time-bands-editor.component` - Time band editor (with time pickers)

**Special UI consideration**: Time band editor needs time-of-day pickers

---

### 4. Pay Tier Rule Module

**Note**: May be integrated into PayRuleSet module as nested component rather than standalone module.

#### 4.1 Models

Already defined in PayRuleSet models.

#### 4.2 Standalone Management (Optional)

If separate CRUD UI needed:
1. `pay-tier-rules-container.component`
2. `pay-tier-rules-table.component` (filtered by PayDayRuleId)
3. CRUD modals

---

### 5. Pay Time Band Rule Module

**Note**: May be integrated into PayDayTypeRule module as nested component.

#### 5.1 Models

Already defined in PayDayTypeRule models.

#### 5.2 Standalone Management (Optional)

If separate CRUD UI needed:
1. `pay-time-band-rules-container.component`
2. `pay-time-band-rules-table.component` (filtered by PayDayTypeRuleId)
3. CRUD modals with time pickers

---

## Internationalization (i18n)

### Translation Keys Needed

Add to all language files (27 total):

```typescript
// Break Policies
'Break Policies': 'Break Policies',
'Break Policy': 'Break Policy',
'Create Break Policy': 'Create Break Policy',
'Edit Break Policy': 'Edit Break Policy',
'Delete Break Policy': 'Delete Break Policy',
'Break Policy Name': 'Break Policy Name',
'Planning Workday Code': 'Planning Workday Code',
'Break Rules': 'Break Rules',
'Day of Week': 'Day of Week',
'Paid Break (seconds)': 'Paid Break (seconds)',
'Unpaid Break (seconds)': 'Unpaid Break (seconds)',

// Pay Rule Sets
'Pay Rule Sets': 'Pay Rule Sets',
'Pay Rule Set': 'Pay Rule Set',
'Create Pay Rule Set': 'Create Pay Rule Set',
'Edit Pay Rule Set': 'Edit Pay Rule Set',
'Delete Pay Rule Set': 'Delete Pay Rule Set',
'Rule Set Name': 'Rule Set Name',
'Day Rules': 'Day Rules',
'Day Type Code': 'Day Type Code',
'Pay Tiers': 'Pay Tiers',
'Tier Order': 'Tier Order',
'Up To (seconds)': 'Up To (seconds)',
'Pay Code': 'Pay Code',

// Pay Day Type Rules
'Pay Day Type Rules': 'Pay Day Type Rules',
'Day Type': 'Day Type',
'Weekday': 'Weekday',
'Weekend': 'Weekend',
'Holiday': 'Holiday',
'Time Bands': 'Time Bands',
'Start Time': 'Start Time',
'End Time': 'End Time',

// Pay Tier Rules
'Pay Tier Rules': 'Pay Tier Rules',
'Tier': 'Tier',
'Time Boundary': 'Time Boundary',

// Pay Time Band Rules
'Pay Time Band Rules': 'Pay Time Band Rules',
'Time Band': 'Time Band',
'Time Range': 'Time Range',

// Common
'Name': 'Name',
'Description': 'Description',
'Actions': 'Actions',
'Create': 'Create',
'Edit': 'Edit',
'Delete': 'Delete',
'Save': 'Save',
'Cancel': 'Cancel',
'Confirm': 'Confirm',
'Yes': 'Yes',
'No': 'No',
'Are you sure?': 'Are you sure?',
'Delete confirmation': 'Are you sure you want to delete this item?',
```

### Files to Update

- `enUS.ts`
- `da.ts`
- `deDE.ts`
- ... (all 27 language files)

---

## Cypress Test Plan

### Test Structure

```
e2e/
├── Page objects/
│   └── TimePlanning/
│       ├── BreakPoliciesPage.ts
│       ├── PayRuleSetsPage.ts
│       ├── PayDayTypeRulesPage.ts
│       ├── PayTierRulesPage.ts
│       └── PayTimeBandRulesPage.ts
└── Tests/
    └── time-planning-rules/
        ├── break-policies.spec.ts
        ├── pay-rule-sets.spec.ts
        ├── pay-day-type-rules.spec.ts
        ├── pay-tier-rules.spec.ts
        └── pay-time-band-rules.spec.ts
```

### Page Object Pattern

**Example: BreakPoliciesPage.ts**

```typescript
class BreakPoliciesPage {
  // Selectors
  get createButton() { return $('#create-break-policy-btn'); }
  get tableRows() { return $$('.break-policy-row'); }
  get nameInput() { return $('#break-policy-name'); }
  get saveButton() { return $('#save-break-policy-btn'); }
  get cancelButton() { return $('#cancel-break-policy-btn'); }
  
  // Actions
  async navigateTo() {
    await browser.url('/time-planning/break-policies');
    await this.createButton.waitForDisplayed({ timeout: 10000 });
  }
  
  async openCreateModal() {
    await this.createButton.click();
    await this.nameInput.waitForDisplayed({ timeout: 5000 });
  }
  
  async createBreakPolicy(name: string, workdayCode: string) {
    await this.openCreateModal();
    await this.nameInput.setValue(name);
    // ... set other fields
    await this.saveButton.click();
  }
  
  async getBreakPolicyByName(name: string) {
    const rows = await this.tableRows;
    for (const row of rows) {
      const rowName = await row.$('.name-cell').getText();
      if (rowName === name) return row;
    }
    return null;
  }
  
  async editBreakPolicy(name: string, newName: string) {
    const row = await this.getBreakPolicyByName(name);
    const editBtn = await row.$('.edit-btn');
    await editBtn.click();
    await this.nameInput.setValue(newName);
    await this.saveButton.click();
  }
  
  async deleteBreakPolicy(name: string) {
    const row = await this.getBreakPolicyByName(name);
    const deleteBtn = await row.$('.delete-btn');
    await deleteBtn.click();
    const confirmBtn = await $('#confirm-delete-btn');
    await confirmBtn.click();
  }
}

export default new BreakPoliciesPage();
```

### Test Scenarios

#### 1. Break Policies Tests (`break-policies.spec.ts`)

```typescript
describe('Break Policies Management', () => {
  before(async () => {
    await loginPage.open('/auth');
    await loginPage.login();
  });
  
  it('should navigate to break policies page', async () => {
    await breakPoliciesPage.navigateTo();
    expect(await breakPoliciesPage.createButton.isDisplayed()).toBe(true);
  });
  
  it('should create a new break policy', async () => {
    const name = `Test Policy ${Date.now()}`;
    await breakPoliciesPage.createBreakPolicy(name, 'WORKDAY');
    
    const row = await breakPoliciesPage.getBreakPolicyByName(name);
    expect(row).not.toBeNull();
  });
  
  it('should create break policy with rules for all weekdays', async () => {
    const name = `Policy With Rules ${Date.now()}`;
    await breakPoliciesPage.openCreateModal();
    await breakPoliciesPage.nameInput.setValue(name);
    
    // Add rules for each day
    for (let day = 0; day < 7; day++) {
      await breakPoliciesPage.addRule(day, 1800, 1800); // 30min paid, 30min unpaid
    }
    
    await breakPoliciesPage.saveButton.click();
    
    const row = await breakPoliciesPage.getBreakPolicyByName(name);
    expect(row).not.toBeNull();
  });
  
  it('should edit an existing break policy', async () => {
    const oldName = `Edit Test ${Date.now()}`;
    const newName = `${oldName} - Updated`;
    
    await breakPoliciesPage.createBreakPolicy(oldName, 'WORKDAY');
    await breakPoliciesPage.editBreakPolicy(oldName, newName);
    
    const row = await breakPoliciesPage.getBreakPolicyByName(newName);
    expect(row).not.toBeNull();
  });
  
  it('should delete a break policy', async () => {
    const name = `Delete Test ${Date.now()}`;
    
    await breakPoliciesPage.createBreakPolicy(name, 'WORKDAY');
    await breakPoliciesPage.deleteBreakPolicy(name);
    
    const row = await breakPoliciesPage.getBreakPolicyByName(name);
    expect(row).toBeNull();
  });
  
  it('should validate required fields', async () => {
    await breakPoliciesPage.openCreateModal();
    await breakPoliciesPage.saveButton.click();
    
    const errorMessage = await $('#name-error');
    expect(await errorMessage.isDisplayed()).toBe(true);
    expect(await errorMessage.getText()).toContain('required');
  });
  
  it('should paginate results', async () => {
    // Create 50+ policies
    for (let i = 0; i < 55; i++) {
      await breakPoliciesPage.createBreakPolicy(`Pagination Test ${i}`, 'WORKDAY');
    }
    
    await breakPoliciesPage.navigateTo();
    const rows = await breakPoliciesPage.tableRows;
    expect(rows.length).toBeLessThanOrEqual(50); // Default page size
    
    const nextPageBtn = await $('#next-page-btn');
    expect(await nextPageBtn.isDisplayed()).toBe(true);
    
    await nextPageBtn.click();
    await browser.pause(500);
    
    const rowsPage2 = await breakPoliciesPage.tableRows;
    expect(rowsPage2.length).toBeGreaterThan(0);
  });
  
  it('should sort by name', async () => {
    await breakPoliciesPage.navigateTo();
    
    const nameHeader = await $('#name-header');
    await nameHeader.click();
    await browser.pause(500);
    
    const rows = await breakPoliciesPage.tableRows;
    const names = await Promise.all(rows.map(r => r.$('.name-cell').getText()));
    
    const sorted = [...names].sort();
    expect(names).toEqual(sorted);
  });
});
```

#### 2. Pay Rule Sets Tests (`pay-rule-sets.spec.ts`)

```typescript
describe('Pay Rule Sets Management', () => {
  it('should create pay rule set with day rules', async () => {
    const name = `Rule Set ${Date.now()}`;
    await payRuleSetsPage.openCreateModal();
    await payRuleSetsPage.nameInput.setValue(name);
    
    // Add SUNDAY rule
    await payRuleSetsPage.addDayRule('SUNDAY');
    await payRuleSetsPage.addTierToDay('SUNDAY', 1, 39600, 'SUN_80'); // First 11h at 80%
    await payRuleSetsPage.addTierToDay('SUNDAY', 2, null, 'SUN_100'); // Rest at 100%
    
    // Add SATURDAY rule
    await payRuleSetsPage.addDayRule('SATURDAY');
    await payRuleSetsPage.addTierToDay('SATURDAY', 1, null, 'SAT_50');
    
    await payRuleSetsPage.saveButton.click();
    
    const row = await payRuleSetsPage.getByName(name);
    expect(row).not.toBeNull();
  });
  
  it('should edit day rules in existing rule set', async () => {
    // Test editing nested day rules
  });
  
  it('should validate tier order', async () => {
    // Test tier order validation
  });
  
  it('should validate tier boundaries', async () => {
    // Test upToSeconds validation (order, non-overlapping)
  });
});
```

#### 3. Pay Day Type Rules Tests (`pay-day-type-rules.spec.ts`)

```typescript
describe('Pay Day Type Rules Management', () => {
  it('should create day type rule with time bands', async () => {
    const name = `Day Type Rule ${Date.now()}`;
    await payDayTypeRulesPage.openCreateModal();
    await payDayTypeRulesPage.selectDayType('Weekend');
    
    // Add time band: 00:00-18:00
    await payDayTypeRulesPage.addTimeBand(0, 64800, 'WEEKEND_DAY'); // 0 to 18:00
    
    // Add time band: 18:00-23:59
    await payDayTypeRulesPage.addTimeBand(64800, 86399, 'WEEKEND_EVENING'); // 18:00 to 23:59
    
    await payDayTypeRulesPage.saveButton.click();
    
    const row = await payDayTypeRulesPage.getByDayType('Weekend');
    expect(row).not.toBeNull();
  });
  
  it('should validate time band overlaps', async () => {
    // Test that overlapping time bands are rejected
  });
  
  it('should use time pickers for band boundaries', async () => {
    // Test time picker UI
  });
});
```

#### 4. Integration Tests

**Cross-entity relationships**:

```typescript
describe('Rule Engine Integration', () => {
  it('should link break policy to workday planning', async () => {
    // Create break policy
    // Use it in planning
    // Verify application
  });
  
  it('should link pay rule set to pay calculation', async () => {
    // Create pay rule set with tiers
    // Apply to work registration
    // Verify pay lines generated correctly
  });
  
  it('should apply day type rules correctly', async () => {
    // Create day type rule for Sunday
    // Register Sunday work
    // Verify correct pay code applied
  });
});
```

### Validation Tests

Each entity needs validation tests for:

1. **Required fields**
2. **Unique constraints** (e.g., unique names)
3. **Range validations** (e.g., seconds must be positive)
4. **Relationship validations** (e.g., can't delete referenced entity)
5. **Business rule validations** (e.g., tier order, time band overlaps)

---

## Dependencies

### Angular Material Components

- `MatFormFieldModule` - Form fields
- `MatInputModule` - Input controls
- `MatButtonModule` - Buttons
- `MatIconModule` - Icons
- `MatDialogModule` - Dialogs/modals
- `MatSelectModule` - Dropdowns
- `MatTooltipModule` - Tooltips
- `MatDatepickerModule` - Date pickers (if needed)
- `MatCheckboxModule` - Checkboxes
- `MatTableModule` - Tables (alternative to mtx-grid)

### Third-Party

- `@ng-matero/extensions` - mtx-grid for data tables
- `ngx-auto-unsubscribe` - Memory leak prevention
- `@ngx-translate/core` - i18n

### Shared Modules

- `EformSharedModule` - Shared components/directives
- `CommonModule` - Angular common
- `FormsModule` - Template forms
- `ReactiveFormsModule` - Reactive forms
- `RouterModule` - Routing

---

## Implementation Order

### Phase 1: Foundation (Week 1)

1. **Models** - All model files for all 5 entities
2. **Services** - All API services
3. **i18n** - Translation keys (at least enUS)

### Phase 2: Core Modules (Weeks 2-3)

1. **BreakPolicy** - Complete module (simplest, no deep nesting)
2. **PayDayTypeRule** - Medium complexity (time bands)

### Phase 3: Complex Modules (Weeks 3-4)

1. **PayRuleSet** - Most complex (nested day rules + tier rules)
2. **PayTierRule** - If standalone (otherwise part of PayRuleSet)
3. **PayTimeBandRule** - If standalone (otherwise part of PayDayTypeRule)

### Phase 4: Integration & Testing (Week 5)

1. **Routing** - Integrate all modules into main routing
2. **Cypress Tests** - Complete E2E test suite
3. **Manual Testing** - End-to-end workflows
4. **Bug Fixes** - Address issues found

### Phase 5: Polish (Week 6)

1. **UI/UX** - Refine user interface
2. **Translations** - Complete all 27 languages
3. **Documentation** - User guide
4. **Performance** - Optimize loading/rendering

---

## File Count Estimate

### Per Entity (Average)

- Models: 6 files
- Service: 1 file
- Components: 5-8 files (.ts, .html, .scss)
- Tests: 3-5 files
- Module/Routing: 2 files

**Total per entity**: ~20-25 files

### For 5 Entities

- Total: **100-125 files**

### Additional

- i18n: 27 files (updated)
- Cypress tests: 10+ files
- Shared components: 5-10 files

**Grand Total**: **~150 files**

---

## Acceptance Criteria

### Functional

- ✅ All CRUD operations work for all 5 entities
- ✅ Pagination works correctly
- ✅ Sorting works on all columns
- ✅ Search/filter works where applicable
- ✅ Nested editing works (day rules, tier rules, time bands)
- ✅ Validation prevents invalid data entry
- ✅ Error messages are user-friendly
- ✅ Success messages confirm actions

### Non-Functional

- ✅ All pages load in < 2 seconds
- ✅ No memory leaks (Auto-unsubscribe working)
- ✅ Responsive design (mobile-friendly)
- ✅ Accessible (WCAG 2.1 AA)
- ✅ All translations complete
- ✅ Consistent UI/UX with existing modules

### Testing

- ✅ All Cypress tests pass
- ✅ E2E coverage for all workflows
- ✅ Validation tests pass
- ✅ Integration tests pass
- ✅ Manual testing complete

---

## Risks & Mitigations

### Risk 1: Nested Editing Complexity

**Risk**: PayRuleSet with nested day rules and tier rules is complex

**Mitigation**:
- Build BreakPolicy first (simpler nested structure)
- Use learnings for PayRuleSet
- Consider wizard-style UI for complex creation

### Risk 2: Time Band UI Complexity

**Risk**: Time-of-day editing (seconds since midnight) is not user-friendly

**Mitigation**:
- Use Material time pickers
- Convert seconds to HH:MM format in UI
- Provide helper text and validation

### Risk 3: Translation Workload

**Risk**: 27 languages × 100+ strings = 2700+ translation entries

**Mitigation**:
- Start with enUS (English) only
- Use translation service for others
- Crowdsource from community
- Phase translations (core languages first)

### Risk 4: Performance

**Risk**: Large rule sets may have performance issues

**Mitigation**:
- Server-side pagination
- Virtual scrolling for large lists
- Lazy loading of nested data
- Caching strategies

---

## Success Metrics

1. **Functionality**: 100% of API endpoints have UI coverage
2. **Quality**: 0 critical bugs at launch
3. **Performance**: < 2s page load time
4. **Test Coverage**: > 80% Cypress test coverage
5. **User Satisfaction**: Positive feedback from QA/users

---

## Next Steps

1. **Review this plan** with development team
2. **Estimate resources** (1-2 developers × 5-6 weeks)
3. **Prioritize entities** (if phased rollout desired)
4. **Set up project tracking** (Jira/GitHub issues)
5. **Begin Phase 1** (Foundation - models, services, i18n)

---

## Questions for Team

1. Should PayTierRule and PayTimeBandRule be standalone modules or integrated into parent modules?
2. What is the priority order of the 5 entities?
3. Is there existing UI design/mockups?
4. What languages are priority for translations?
5. Are there performance requirements (users, data volume)?

---

## Conclusion

This plan provides a complete roadmap for implementing Angular frontend components for the rule engine API. The estimated effort is **80-100 hours** (2-2.5 months for 1 developer, or 1-1.5 months for 2 developers working in parallel).

The implementation follows established patterns in the codebase, minimizing architectural risk. The phased approach allows for incremental delivery and testing.

**Status**: ✅ Ready for implementation

# Break Policy Complete Configuration - Implementation Plan

## Executive Summary

This document outlines a comprehensive plan to implement full Break Policy configuration, including management of nested BreakPolicyRule entities. The goal is to enable users to create, edit, and delete break policies with their associated rules through intuitive modal dialogs.

## Current State

### Entity Structure

**BreakPolicy** (Main Entity)
- `id`: number
- `name`: string
- `rules`: BreakPolicyRuleModel[] (nested collection)

**BreakPolicyRule** (Nested Entity)
- `id`: number
- `breakAfterMinutes`: number - When the break applies
- `breakDurationMinutes`: number - Total break duration
- `paidBreakMinutes`: number - Paid portion of break
- `unpaidBreakMinutes`: number - Unpaid portion of break

### Current Implementation Status

✅ **Complete**:
- Backend API endpoints (CRUD)
- Basic Angular models
- Basic services
- Simple modals (name only)
- Table display
- Module structure

❌ **Missing**:
- Rule configuration in create modal
- Rule management in edit modal
- Rule CRUD operations UI
- Rule validation
- Nested form handling

## Business Requirements

### Break Policy Configuration

A break policy defines break rules for employees based on work duration:
- **Name**: Identifies the policy (e.g., "Standard 8-hour shift")
- **Rules**: Collection of break rules applied in sequence

### Break Rule Logic

Each rule defines:
1. **Break After**: Minutes worked before this break applies
2. **Duration**: Total break duration in minutes
3. **Paid Minutes**: Portion of break that is paid
4. **Unpaid Minutes**: Portion of break that is unpaid

**Validation Rules**:
- `paidBreakMinutes + unpaidBreakMinutes = breakDurationMinutes`
- All values must be >= 0
- `breakAfterMinutes` must be > 0
- Rules typically ordered by `breakAfterMinutes` ascending

### Example Break Policy

**Name**: "Standard 8-hour shift"

**Rules**:
1. After 60 minutes → 15 minute break (15 paid, 0 unpaid)
2. After 240 minutes → 30 minute lunch (0 paid, 30 unpaid)
3. After 420 minutes → 15 minute break (15 paid, 0 unpaid)

## Implementation Phases

### Phase 1: Rule Form Component (Reusable)

**Component**: `BreakPolicyRuleFormComponent`

**Purpose**: Reusable form for creating/editing a single rule

**Inputs**:
- `rule`: BreakPolicyRuleModel (optional, for edit mode)
- `mode`: 'create' | 'edit'

**Outputs**:
- `ruleChange`: EventEmitter<BreakPolicyRuleModel>

**Features**:
- Reactive form with 4 fields
- Real-time validation
- Calculate unpaid from duration - paid (helper)
- Display validation errors
- Disabled state support

**Form Fields**:
```typescript
breakAfterMinutes: FormControl<number>     // Required, min: 1
breakDurationMinutes: FormControl<number>  // Required, min: 1
paidBreakMinutes: FormControl<number>      // Required, min: 0
unpaidBreakMinutes: FormControl<number>    // Required, min: 0
```

**Validation**:
- Custom validator: paid + unpaid = duration
- All fields required
- Min value constraints

**Files**:
- `break-policy-rule-form.component.ts`
- `break-policy-rule-form.component.html`
- `break-policy-rule-form.component.scss`

### Phase 2: Rules List Component (Display)

**Component**: `BreakPolicyRulesListComponent`

**Purpose**: Display list of rules with actions

**Inputs**:
- `rules`: BreakPolicyRuleModel[]
- `editable`: boolean (default: true)

**Outputs**:
- `addRule`: EventEmitter<void>
- `editRule`: EventEmitter<BreakPolicyRuleModel>
- `deleteRule`: EventEmitter<BreakPolicyRuleModel>

**Features**:
- Display rules in table
- Sort by breakAfter ascending
- Actions: Edit, Delete per rule
- Add Rule button
- Empty state message
- Summary row (total paid/unpaid)

**Table Columns**:
1. Break After (minutes)
2. Duration (minutes)
3. Paid (minutes)
4. Unpaid (minutes)
5. Actions (Edit/Delete)

**Files**:
- `break-policy-rules-list.component.ts`
- `break-policy-rules-list.component.html`
- `break-policy-rules-list.component.scss`

### Phase 3: Rule Dialog Component (Add/Edit Rule)

**Component**: `BreakPolicyRuleDialogComponent`

**Purpose**: Modal dialog for adding or editing a single rule

**Dialog Data**:
```typescript
{
  mode: 'create' | 'edit',
  rule?: BreakPolicyRuleModel
}
```

**Dialog Result**:
```typescript
BreakPolicyRuleModel | null
```

**Features**:
- Uses BreakPolicyRuleFormComponent
- Material Dialog
- Save/Cancel buttons
- Validation before save
- Error display

**Layout**:
```
┌────────────────────────────────┐
│ Add/Edit Break Rule            │
├────────────────────────────────┤
│ Break After (minutes): [___]   │
│ Duration (minutes):    [___]   │
│ Paid (minutes):        [___]   │
│ Unpaid (minutes):      [___]   │
│                                │
│ ℹ Paid + Unpaid must equal     │
│   Duration                      │
│                                │
│ [Cancel]            [Save]     │
└────────────────────────────────┘
```

**Files**:
- `break-policy-rule-dialog.component.ts`
- `break-policy-rule-dialog.component.html`
- `break-policy-rule-dialog.component.scss`

### Phase 4: Enhanced Create Modal

**Component**: `BreakPoliciesCreateModalComponent` (existing, enhanced)

**Current**: Only captures name
**Enhanced**: Captures name + manages rules

**Form Structure**:
```typescript
form = FormGroup({
  name: FormControl<string>(),
  rules: FormArray<FormGroup<RuleForm>>()
})
```

**Features**:
- Policy name field (required)
- BreakPolicyRulesListComponent integration
- Add Rule button → opens BreakPolicyRuleDialogComponent
- Edit Rule → opens dialog with rule data
- Delete Rule → removes from FormArray
- Save → POST with name + rules array
- Validation before save

**Layout**:
```
┌─────────────────────────────────────┐
│ Create Break Policy                 │
├─────────────────────────────────────┤
│ Name: [_________________________]   │
│                                     │
│ Break Rules:                        │
│ ┌─────────────────────────────────┐ │
│ │ After│Duration│Paid│Unpaid│     │ │
│ │  60m │  15m   │ 15 │  0   │[✎][🗑]│ │
│ │ 240m │  30m   │  0 │ 30   │[✎][🗑]│ │
│ └─────────────────────────────────┘ │
│ [+ Add Rule]                        │
│                                     │
│ [Cancel]                  [Create]  │
└─────────────────────────────────────┘
```

**Changes Needed**:
- Import BreakPolicyRulesListComponent
- Add FormArray for rules
- Implement add/edit/delete rule methods
- Open BreakPolicyRuleDialogComponent
- Update save to include rules
- Update validation

### Phase 5: Enhanced Edit Modal

**Component**: `BreakPoliciesEditModalComponent` (existing, enhanced)

**Current**: Only edits name
**Enhanced**: Edits name + manages rules

**Features**:
- Load policy with existing rules
- Policy name field (required)
- BreakPolicyRulesListComponent with rules
- Add new rules
- Edit existing rules (preserves rule.id)
- Delete rules (marks for deletion or removes)
- Save → PUT with name + complete rules array

**Data Loading**:
1. Receive policy ID
2. Fetch full BreakPolicyModel (includes rules)
3. Populate form with name + rules
4. Display rules in list component

**Changes Needed**:
- Load full policy model (not just simple model)
- Import BreakPolicyRulesListComponent
- Add FormArray for rules
- Implement add/edit/delete rule methods
- Track rule IDs for backend updates
- Update save to include rules

### Phase 6: Module Configuration

**File**: `break-policies.module.ts`

**Updates**:
- Declare 3 new components:
  - `BreakPolicyRuleFormComponent`
  - `BreakPolicyRulesListComponent`
  - `BreakPolicyRuleDialogComponent`
- Add to exports if needed
- Ensure MatDialogModule imported

**File**: `components/index.ts`

**Updates**:
- Export 3 new components

### Phase 7: Validation Implementation

**Client-Side Validation**:
1. Policy name required (min 2 chars)
2. Rules validation:
   - Each rule valid individually
   - Paid + Unpaid = Duration
   - All values >= 0 (except breakAfter >= 1)
3. Optional: No duplicate breakAfter values
4. Optional: Rules ordered by breakAfter

**Server-Side Validation** (already exists):
- Backend API validates on save
- Returns validation errors
- Display errors in modal

**Error Display**:
- Field-level errors (red text under field)
- Form-level errors (alert box at top)
- Toast notifications on save success/failure

### Phase 8: User Experience Enhancements

**Loading States**:
- Spinner while loading policy
- Disabled buttons during save
- Loading indicator in dialog

**Error Handling**:
- Display API errors
- Validation error messages
- Network error handling
- Retry mechanisms

**Confirmation Dialogs**:
- Confirm delete rule
- Confirm cancel with unsaved changes
- Confirm overwrite

**Helper Text**:
- Tooltips on fields
- Example rules section
- Validation hints
- Calculation helpers

**Success Feedback**:
- Toast on successful save
- Close modal on success
- Refresh table data
- Success animation (optional)

## Technical Implementation Details

### Form Management Strategy

**Use FormArray for Rules**:
```typescript
// In create/edit modal
form = this.fb.group({
  name: ['', [Validators.required, Validators.minLength(2)]],
  rules: this.fb.array([])  // FormArray of rule FormGroups
});

get rulesFormArray() {
  return this.form.get('rules') as FormArray;
}

addRule(rule: BreakPolicyRuleModel) {
  this.rulesFormArray.push(this.createRuleFormGroup(rule));
}

createRuleFormGroup(rule?: BreakPolicyRuleModel): FormGroup {
  return this.fb.group({
    id: [rule?.id || null],
    breakAfterMinutes: [rule?.breakAfterMinutes || '', [Validators.required, Validators.min(1)]],
    breakDurationMinutes: [rule?.breakDurationMinutes || '', [Validators.required, Validators.min(1)]],
    paidBreakMinutes: [rule?.paidBreakMinutes || 0, [Validators.required, Validators.min(0)]],
    unpaidBreakMinutes: [rule?.unpaidBreakMinutes || 0, [Validators.required, Validators.min(0)]]
  }, { validators: this.ruleValidator });
}

ruleValidator(group: FormGroup): ValidationErrors | null {
  const duration = group.get('breakDurationMinutes')?.value || 0;
  const paid = group.get('paidBreakMinutes')?.value || 0;
  const unpaid = group.get('unpaidBreakMinutes')?.value || 0;
  
  return paid + unpaid === duration ? null : { sumMismatch: true };
}
```

### Data Flow

**Create Flow**:
1. User clicks "Create Break Policy"
2. Modal opens with empty form
3. User enters name
4. User clicks "Add Rule" → Rule dialog opens
5. User fills rule form → Saves
6. Rule added to rules list
7. Repeat for more rules
8. User clicks "Create"
9. POST to API with `{ name, rules: [...] }`
10. API saves policy and rules
11. Modal closes, table refreshes

**Edit Flow**:
1. User clicks "Edit" on policy row
2. Fetch full policy (GET /break-policies/{id})
3. Modal opens, form populated with name + rules
4. User can edit name
5. User can add/edit/delete rules
6. User clicks "Save"
7. PUT to API with `{ id, name, rules: [...] }`
8. API updates policy and rules
9. Modal closes, table refreshes

**Delete Rule Flow**:
1. User clicks delete on rule in list
2. Confirmation dialog (optional)
3. Remove from FormArray
4. Rule removed from UI
5. On save, backend handles deletion (missing IDs = deleted)

### API Integration

**Create Policy**:
```typescript
// POST /api/time-planning-pn/break-policies
{
  name: "Standard 8-hour",
  rules: [
    { breakAfterMinutes: 60, breakDurationMinutes: 15, paidBreakMinutes: 15, unpaidBreakMinutes: 0 },
    { breakAfterMinutes: 240, breakDurationMinutes: 30, paidBreakMinutes: 0, unpaidBreakMinutes: 30 }
  ]
}
```

**Update Policy**:
```typescript
// PUT /api/time-planning-pn/break-policies/{id}
{
  id: 1,
  name: "Standard 8-hour",
  rules: [
    { id: 10, breakAfterMinutes: 60, breakDurationMinutes: 15, paidBreakMinutes: 15, unpaidBreakMinutes: 0 },
    { breakAfterMinutes: 480, breakDurationMinutes: 15, paidBreakMinutes: 15, unpaidBreakMinutes: 0 }  // New rule, no ID
    // Note: Rule with id:11 missing = deleted
  ]
}
```

**Backend Handling**:
- Rules with ID: Update
- Rules without ID: Create new
- Missing rule IDs: Delete

## File Structure

```
break-policies/
├── components/
│   ├── break-policies-container/
│   ├── break-policies-table/
│   ├── break-policies-create-modal/      # Enhanced
│   ├── break-policies-edit-modal/        # Enhanced
│   ├── break-policies-delete-modal/
│   ├── break-policy-rule-form/           # NEW
│   │   ├── break-policy-rule-form.component.ts
│   │   ├── break-policy-rule-form.component.html
│   │   └── break-policy-rule-form.component.scss
│   ├── break-policy-rules-list/          # NEW
│   │   ├── break-policy-rules-list.component.ts
│   │   ├── break-policy-rules-list.component.html
│   │   └── break-policy-rules-list.component.scss
│   ├── break-policy-rule-dialog/         # NEW
│   │   ├── break-policy-rule-dialog.component.ts
│   │   ├── break-policy-rule-dialog.component.html
│   │   └── break-policy-rule-dialog.component.scss
│   └── index.ts
├── break-policies.module.ts
└── break-policies.routing.ts
```

## Implementation Checklist

### Phase 1: Rule Form Component
- [ ] Create component files
- [ ] Implement reactive form
- [ ] Add field validation
- [ ] Add custom validator (sum check)
- [ ] Add real-time calculation
- [ ] Style form layout
- [ ] Add error display
- [ ] Test standalone

### Phase 2: Rules List Component
- [ ] Create component files
- [ ] Implement table display
- [ ] Add sort by breakAfter
- [ ] Add edit/delete buttons
- [ ] Add "Add Rule" button
- [ ] Handle empty state
- [ ] Add summary row (optional)
- [ ] Style table
- [ ] Test with sample data

### Phase 3: Rule Dialog Component
- [ ] Create component files
- [ ] Set up Material Dialog
- [ ] Integrate rule form component
- [ ] Add save/cancel buttons
- [ ] Implement validation
- [ ] Handle dialog result
- [ ] Style dialog
- [ ] Test create mode
- [ ] Test edit mode

### Phase 4: Enhanced Create Modal
- [ ] Add rules FormArray
- [ ] Integrate rules list component
- [ ] Implement add rule (opens dialog)
- [ ] Implement edit rule (opens dialog)
- [ ] Implement delete rule
- [ ] Update save method
- [ ] Add validation
- [ ] Test full workflow

### Phase 5: Enhanced Edit Modal
- [ ] Load full policy model
- [ ] Populate rules FormArray
- [ ] Integrate rules list component
- [ ] Implement add rule
- [ ] Implement edit rule
- [ ] Implement delete rule
- [ ] Track rule IDs
- [ ] Update save method
- [ ] Test full workflow

### Phase 6: Module Configuration
- [ ] Declare new components
- [ ] Add to exports
- [ ] Verify imports
- [ ] Test module loading

### Phase 7: Validation
- [ ] Client-side validation
- [ ] Custom validators
- [ ] Error messages
- [ ] Server-side error display
- [ ] Test all validation scenarios

### Phase 8: UX Polish
- [ ] Loading states
- [ ] Error handling
- [ ] Confirmation dialogs
- [ ] Helper text
- [ ] Success feedback
- [ ] Responsive design
- [ ] Accessibility
- [ ] Final testing

## Testing Strategy

### Unit Tests
- Rule form validation
- Sum validator
- Form array management
- Data transformation

### Integration Tests
- Create policy with rules
- Edit policy rules
- Delete rules
- API integration

### E2E Tests (Cypress)
- Create complete policy workflow
- Edit policy and rules
- Delete individual rules
- Validation errors
- Success scenarios

## Success Criteria

- ✅ Can create break policy with multiple rules
- ✅ Can edit policy name and rules
- ✅ Can add new rules to existing policy
- ✅ Can edit existing rules
- ✅ Can delete rules
- ✅ All validation works correctly
- ✅ UI is intuitive and matches patterns
- ✅ All tests pass
- ✅ No console errors
- ✅ Responsive and accessible

## Timeline Estimate

| Phase | Effort | Notes |
|-------|--------|-------|
| Phase 1: Rule Form | 2-3 hours | Reusable component |
| Phase 2: Rules List | 2-3 hours | Display + actions |
| Phase 3: Rule Dialog | 2-3 hours | Modal wrapper |
| Phase 4: Create Modal | 3-4 hours | Integration |
| Phase 5: Edit Modal | 3-4 hours | Load + integrate |
| Phase 6: Module Config | 1 hour | Registration |
| Phase 7: Validation | 2-3 hours | All scenarios |
| Phase 8: UX Polish | 2-3 hours | Final touches |
| **Total** | **17-26 hours** | Full implementation |

## Next Steps

1. Review and approve plan
2. Begin Phase 1 implementation
3. Iterative development and testing
4. Code review after each phase
5. Final integration testing
6. Deploy and monitor

## References

- Backend API: `/api/time-planning-pn/break-policies`
- Entity: `Microting.TimePlanningBase.Infrastructure.Data.Entities.BreakPolicy`
- Models: `eform-client/src/app/plugins/modules/time-planning-pn/models/break-policies/`
- Existing patterns: Registration devices actions components

---

**Status**: Plan Complete - Ready for Implementation
**Author**: GitHub Copilot
**Date**: 2026-02-17

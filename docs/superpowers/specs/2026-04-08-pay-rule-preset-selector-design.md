# Pay Rule Set Preset Selector Design

## Context

Creating a Pay Rule Set manually requires adding each day rule, tier, and time band one by one through nested dialogs. For legally fixed agreements like GLS-A / 3F Jordbrugsoverenskomsten, the rules are not adjustable - users just need to select the correct variant and the system should create the complete rule set. This feature adds a preset dropdown to the create modal that pre-fills (and locks) the entire rule configuration.

## Requirements

1. Dropdown at the top of the create modal listing available presets grouped by agreement
2. Locked presets (e.g., GLS-A / 3F) are fully non-editable - no name field, no rule editing, just select and create
3. Read-only summary of rules shown when a locked preset is selected
4. Singleton behavior - already-created presets disappear from the dropdown
5. Locked presets cannot be deleted in the current implementation. A future enhancement may add conditional deletion when no workers are assigned.
6. "Blank (custom rules)" option preserves the current full-editing behavior
7. Future extensibility for editable presets (base template + local adjustments) - not in scope now but the data model should not preclude it

## Design

### Preset Data Model (Frontend Constants)

A new file `pay-rule-set-presets.ts` in the models directory defines all presets:

```typescript
export interface PayRuleSetPreset {
  key: string;               // unique identifier, e.g. "glsa-jordbrug-standard"
  group: string;             // dropdown optgroup label, e.g. "GLS-A / 3F"
  label: string;             // display name, e.g. "Jordbrug - Standard"
  name: string;              // the PayRuleSet.Name to save, e.g. "GLS-A / 3F - Jordbrug Standard"
  locked: boolean;           // true = non-editable, false = editable template (future)
  payDayRules: Array<{
    dayCode: string;
    payTierRules: Array<{
      order: number;
      upToSeconds: number | null;
      payCode: string;
    }>;
  }>;
  payDayTypeRules: Array<{
    dayType: string;          // "Monday" | "Tuesday" | ... | "Holiday"
    defaultPayCode: string;
    priority: number;
    timeBandRules: Array<{
      startSecondOfDay: number;
      endSecondOfDay: number;
      payCode: string;
      priority: number;
    }>;
  }>;
}
```

### Initial Presets

Five GLS-A / 3F presets, all with `locked: true`:

| Key | Group | Label | Rules |
|-----|-------|-------|-------|
| `glsa-jordbrug-standard` | GLS-A / 3F | Jordbrug - Standard | WEEKDAY 3 tiers (NORMAL/OT30/OT80), SATURDAY 2 tiers, SUNDAY/HOLIDAY/GRUNDLOVSDAG flat + weekday/Saturday time bands |
| `glsa-jordbrug-dyrehold` | GLS-A / 3F | Jordbrug - Dyrehold | Same OT tiers + animal care pay codes (ANIMAL_NIGHT, SAT_ANIMAL_AFTERNOON, ANIMAL_SUN_HOLIDAY) + full 24h time bands |
| `glsa-jordbrug-elev-u18` | GLS-A / 3F | Jordbrug - Elev (under 18) | 8h cap, ELEV_ pay codes, 2h Sun/Holiday tier |
| `glsa-jordbrug-elev-o18` | GLS-A / 3F | Jordbrug - Elev (over 18) | 7.4h norm, ELEV_ pay codes, 2h Sun/Holiday tier |
| `glsa-jordbrug-elev-u18-dyrehold` | GLS-A / 3F | Jordbrug - Elev u18 Dyrehold | Under-18 tiers + animal care pay codes |

Rule values match the existing `GlsAFixtureHelper.cs` backend fixtures exactly.

### Create Modal Behavior

**When modal opens:**
1. Fetch existing PayRuleSets from the API (already done for the table)
2. Filter presets: remove any preset whose `name` matches an existing PayRuleSet name
3. Build dropdown options: "-- Blank (custom rules) --" + grouped presets

**When user selects a locked preset:**
1. Hide the Name input field (name is fixed from preset)
2. Hide "Add Day" / "Add Day Type" buttons
3. Hide edit/delete icons on rules
4. Show read-only summary tables:
   - Pay Day Rules table: DayCode | Tier chain (e.g., "NORMAL (7.4h) -> OVERTIME_30 (2h) -> OVERTIME_80")
   - Day Type Rules table: Day | Time bands (e.g., "04:00-06:00 SHIFTED_MORNING | 06:00-18:00 NORMAL")
5. Show a lock indicator: "This is a fixed overenskomst. Rules cannot be edited."
6. Enable the Create button

**When user selects "Blank":**
1. Show full editing UI as today (name field, add buttons, editable rules)
2. Clear any pre-filled rules from a previous preset selection

**When user clicks Create (locked preset):**
1. Build `PayRuleSetCreateModel` from the preset definition
2. Call the existing `createPayRuleSet` API
3. Close modal, refresh table

### Delete Guard on Locked Presets

The current implementation blocks all deletes on locked presets. In the pay-rule-sets-table component, when delete is clicked for a rule set:
1. Check if the rule set name matches a known locked preset name
2. If yes, block the delete and show an error message indicating locked presets cannot be deleted
3. A future enhancement may add conditional deletion when no workers are assigned, allowing the preset to reappear in the create dropdown

Note: The "is this a locked preset?" check uses name matching against the preset constants. This is simpler than adding a `isLocked` DB field and sufficient since locked preset names are deterministic.

### Component Changes

**`pay-rule-sets-create-modal.component.ts`:**
- Import `PAY_RULE_SET_PRESETS` constant
- Add `selectedPreset: PayRuleSetPreset | null` property
- Add `availablePresets` computed from presets minus already-created
- Add `isLocked` getter: `this.selectedPreset?.locked ?? false`
- On preset change: if locked, populate form arrays from preset data; if blank, clear form arrays
- Override `createPayRuleSet()`: if locked, use preset's fixed name instead of form name

**`pay-rule-sets-create-modal.component.html`:**
- Add `mat-select` dropdown at top of form, before name field
- Wrap name input in `*ngIf="!isLocked"`
- Wrap add/edit/delete buttons in `*ngIf="!isLocked"`
- Add `*ngIf="isLocked"` read-only summary section
- Add lock indicator banner

**`pay-rule-sets-table.component.ts`:**
- On delete: if rule set name is in locked presets, check assigned worker count via API before allowing

### File Structure

| File | Action |
|------|--------|
| `models/pay-rule-sets/pay-rule-set-presets.ts` | CREATE - preset definitions + interface |
| `models/pay-rule-sets/index.ts` | EDIT - export new file |
| `components/pay-rule-sets-create-modal/pay-rule-sets-create-modal.component.ts` | EDIT - add preset logic |
| `components/pay-rule-sets-create-modal/pay-rule-sets-create-modal.component.html` | EDIT - add dropdown + read-only view |
| `components/pay-rule-sets-table/pay-rule-sets-table.component.ts` | EDIT - add delete guard |

### Verification

1. Open Pay Rule Sets page, click Create
2. Verify dropdown shows all 5 GLS-A presets grouped under "GLS-A / 3F"
3. Select "Jordbrug - Standard" - verify name is read-only, rules shown as summary, no edit buttons
4. Click Create - verify rule set appears in table
5. Open Create again - verify "Jordbrug - Standard" is gone from dropdown
6. Try to delete it (with no workers assigned) - succeeds
7. Open Create again - verify "Jordbrug - Standard" reappears
8. Select "Blank" - verify full editing UI works as before

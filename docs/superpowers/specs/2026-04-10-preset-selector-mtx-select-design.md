# Preset Selector mtx-select Migration

## Context

The Pay Rule Set create modal uses a native `mat-select` with manual `mat-optgroup` for the overenskomst preset selector. This has two problems:

1. **Dropdown clipped by dialog** - The mat-select panel is constrained by the dialog container, so with 37 presets the list gets cut off and only ~5 options are visible.
2. **Doesn't match plugin convention** - Every other dropdown in the time-planning plugin uses `mtx-select` (ng-select), including `managingTagIds`, `payRuleSetId`, `workingHoursSite`, `planningTags`, and the working hours grid cells.

## Fix

Replace `mat-select` with `mtx-select` using native `groupBy`, following the proven pattern from `assigned-site-dialog.component.html`.

## Changes

### 1. HTML template
`pay-rule-sets-create-modal.component.html`:

Replace the `mat-select` block with:

```html
<mat-form-field class="full-width preset-selector-field">
  <mat-label>{{ 'Overenskomst' | translate }}</mat-label>
  <mtx-select
    [ngModel]="selectedPreset"
    (ngModelChange)="onPresetChanged($event)"
    [items]="availablePresets"
    bindLabel="label"
    groupBy="group"
    [clearable]="true"
    [searchable]="true"
    appendTo="body"
    id="presetSelector"
    placeholder="{{ 'Blank (custom rules)' | translate }}">
  </mtx-select>
</mat-form-field>
```

Key properties:
- `groupBy="group"` - native grouping using the `group` field on each preset
- `appendTo="body"` - **critical**: renders dropdown outside the dialog, preventing clipping
- `[searchable]="true"` - type-to-filter for 37+ presets
- `[clearable]="true"` - allows clearing back to blank custom rules
- `[ngModel]` - template-driven binding matching plugin convention

### 2. TS cleanup
`pay-rule-sets-create-modal.component.ts`:

Remove these now-unused members (native `groupBy` handles them):
- `get presetGroups(): string[]` getter
- `getPresetsForGroup(group: string): PayRuleSetPreset[]` method

Keep:
- `availablePresets`, `selectedPreset`, `onPresetChanged()`
- `isLocked` getter
- All other preset-related logic

### 3. Module import
Verify `MtxSelectModule` (or `NgSelectModule` via `@ng-matero/extensions/select`) is imported in `pay-rule-sets.module.ts`. Other modules using mtx-select have this import. The assigned-site-dialog uses it so the root shared module likely already has it.

### 4. E2E test
`time-planning-glsa-3f-pay-rules.spec.ts`:

The helper `selectPreset()` needs to update how it opens and selects. Change from clicking mat-select panel to ng-dropdown-panel pattern used elsewhere:

```typescript
async function selectPreset(page: Page, label: string): Promise<void> {
  // Click the mtx-select to open dropdown
  await page.locator('#presetSelector').click();
  
  // Wait for ng-dropdown-panel
  const dropdown = page.locator('ng-dropdown-panel');
  await dropdown.waitFor({ state: 'visible', timeout: 10000 });
  
  // Click the option by label text
  await dropdown.locator('.ng-option').filter({ hasText: label }).first().click();
  
  // Wait for dropdown to close
  await dropdown.waitFor({ state: 'hidden', timeout: 5000 }).catch(() => {});
}
```

## Verification

1. Open Pay Rule Sets create modal
2. Click the Overenskomst dropdown
3. Verify ALL 37 presets are visible (scrollable) - not capped at 5
4. Verify groups shown: "GLS-A / 3F" and "KA / Krifa"
5. Type "Agroindustri" - verify filtering works
6. Clear the selection - verify form returns to blank state
7. E2E test still passes with updated selectors

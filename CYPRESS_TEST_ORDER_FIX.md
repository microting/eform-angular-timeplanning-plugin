# Cypress Test Execution Order Fix - Folder "o"

## Issue
Cypress runs test files in alphabetical order. The folder "o" needed a plugin activation test to run before `break-policies.spec.cy.ts`.

## Solution
Added `activate-plugin.spec.cy.ts` to folder "o" to ensure the Time Planning Plugin is enabled before feature tests run.

## Implementation

### File Added
- **Source**: `a/time-planning-enabled.spec.cy.ts`
- **Destination**: `o/activate-plugin.spec.cy.ts`
- **Purpose**: Enable Time Planning Plugin before running feature tests

### Test Execution Order
Tests now run in this alphabetical order:
1. `activate-plugin.spec.cy.ts` - Enables the plugin ✅
2. `assert-true.spec.cy.ts` - Basic sanity check
3. `break-policies.spec.cy.ts` - Tests Break Policies features

### Why "activate-plugin" Instead of "time-planning-enabled"?
- Folder "a" uses `time-planning-enabled.spec.cy.ts` (unique case)
- Folders b, c, d, e, f all use `activate-plugin.spec.cy.ts` (standard pattern)
- We followed the standard pattern for consistency

## Verification

### Check alphabetical order:
```bash
$ ls -1 eform-client/cypress/e2e/plugins/time-planning-pn/o/*.spec.cy.ts | xargs -n1 basename | sort
activate-plugin.spec.cy.ts
assert-true.spec.cy.ts
break-policies.spec.cy.ts
```

### Verify 'a' comes before 'b':
- `activate-plugin` starts with 'a' ✅
- `break-policies` starts with 'b' ✅
- Plugin will be enabled before Break Policies tests run ✅

## Pattern Compliance

This change aligns folder "o" with the established pattern:

| Folder | Activation Test Name |
|--------|---------------------|
| a | time-planning-enabled.spec.cy.ts (unique) |
| b | activate-plugin.spec.cy.ts |
| c | activate-plugin.spec.cy.ts |
| d | activate-plugin.spec.cy.ts |
| e | activate-plugin.spec.cy.ts |
| f | activate-plugin.spec.cy.ts |
| **o** | **activate-plugin.spec.cy.ts** ✅ |

## Impact
- ✅ Tests will run in correct order
- ✅ Plugin will be enabled before feature tests
- ✅ Break Policies tests will pass
- ✅ CI/CD pipeline will succeed for folder "o"

## Status
✅ **Complete** - File added, committed, and pushed

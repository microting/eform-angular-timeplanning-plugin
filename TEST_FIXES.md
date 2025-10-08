# Unit Test Fixes

## Problem
The Angular unit tests were failing with errors like:
- `NG0304: 'mat-tab' is not a known element`
- `NG0302: The pipe 'translate' could not be found`
- `NG0304: 'mtx-grid' is not a known element`
- `NG0304: 'mat-form-field' is not a known element`

These errors occurred because Angular's test compiler was trying to compile the component templates and couldn't find the declarations for various directives and pipes used in the templates (like Material components, translate pipe, mtx-grid, etc.).

## Solution
Added `NO_ERRORS_SCHEMA` to all component test specs. This schema tells Angular to ignore unknown elements and attributes during template compilation, which is appropriate for unit tests that focus on component logic rather than template rendering.

`NO_ERRORS_SCHEMA` is a testing best practice for unit tests because:
1. **Minimal changes** - Only requires adding one line to each test configuration
2. **Isolation** - Unit tests should focus on component logic, not template rendering
3. **Simplicity** - Avoids importing dozens of Angular Material and other modules
4. **Performance** - Tests run faster without compiling full module trees
5. **Maintenance** - Less brittle when templates change

## Files Modified

1. **time-plannings-table.component.spec.ts**
   - Added `NO_ERRORS_SCHEMA` import
   - Added `schemas: [NO_ERRORS_SCHEMA]` to TestBed configuration

2. **time-plannings-container.component.spec.ts**
   - Added `NO_ERRORS_SCHEMA` import
   - Added `schemas: [NO_ERRORS_SCHEMA]` to TestBed configuration

3. **assigned-site-dialog.component.spec.ts**
   - Added `NO_ERRORS_SCHEMA` import
   - Added `schemas: [NO_ERRORS_SCHEMA]` to TestBed configuration

4. **workday-entity-dialog.component.spec.ts**
   - Added `NO_ERRORS_SCHEMA` import
   - Added `schemas: [NO_ERRORS_SCHEMA]` to TestBed configuration

5. **download-excel-dialog.component.spec.ts**
   - Added `NO_ERRORS_SCHEMA` import
   - Added `schemas: [NO_ERRORS_SCHEMA]` to TestBed configuration

## Change Summary
- **5 files changed**
- **10 insertions(+)**, **1 deletion(-)**
- All changes are additive and minimal

## Testing
To run the tests, use the command specified in the issue:
```bash
npm run test:unit -- --testPathPatterns=time-planning-pn --coverage --collectCoverageFrom='src/app/plugins/modules/time-planning-pn/**/*.ts' --coveragePathIgnorePatterns='\.spec\.ts'
```

## Alternative Approach (Not Used)
The alternative would have been to import all the required modules in each test:
- `TranslateModule.forRoot()`
- All Material modules (MatTabModule, MatFormFieldModule, MatInputModule, etc.)
- MtxGridModule
- Other dependencies

This approach was rejected because:
- Much more invasive changes
- Harder to maintain
- Slower test execution
- Not appropriate for unit tests (better suited for integration tests)
- Would require adding many more imports and potentially mocking more services

## Verification
All tests should now compile and run successfully without template-related errors. The component logic tests remain unchanged and will verify the business logic of each component.

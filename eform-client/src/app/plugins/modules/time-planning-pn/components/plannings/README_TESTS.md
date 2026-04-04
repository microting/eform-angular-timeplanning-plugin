# Angular Unit Tests for Time Planning Plugin

This directory contains unit tests for the Time Planning plugin components.

## Test Files

- `time-plannings-container.component.spec.ts` - Unit tests for the container component
- `time-plannings-table.component.spec.ts` - Unit tests for the table component

## Running Tests

These tests are designed to run when the plugin is integrated into the main eform-angular-frontend repository.

To run the tests locally:

1. Copy the plugin files to the main frontend repository (use devinstall.sh)
2. Navigate to the frontend directory
3. Run: `npm test` or `ng test`

## Test Coverage

### TimePlanningsContainerComponent
- Date navigation (forward/backward)
- Date formatting
- Event handlers
- Dialog interactions
- Site filtering (resigned sites toggle)

### TimePlanningsTableComponent
- Time conversion utilities (minutes to time, hours to time)
- Cell styling logic based on work status
- Date validation
- Stop time display formatting

## Running in CI/CD

The GitHub Actions workflow will automatically run these tests when changes are pushed to the repository.

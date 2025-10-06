# Package.json Configuration for Angular Unit Tests

## Overview
This document explains the package.json configuration needed in the main `eform-angular-frontend` repository to support running unit tests for the time-planning-pn plugin.

## Required Test Scripts

The GitHub Actions workflows will try to run tests using one of these approaches (in order):

### Option 1: test:ci script (Recommended)
If your package.json has a `test:ci` script, it will use that:

```json
{
  "scripts": {
    "test:ci": "ng test --watch=false --code-coverage --browsers=ChromeHeadless"
  }
}
```

### Option 2: test script with parameters
If `test:ci` is not available, it will try to use the `test` script with additional parameters:

```json
{
  "scripts": {
    "test": "ng test"
  }
}
```

The workflow will add `--watch=false --browsers=ChromeHeadless` automatically.

## Common Issues and Solutions

### Issue 0: "Cannot find module 'karma'" Error

**Error message:**
```
Error: Cannot find module 'karma'
```

**Cause:** The frontend repository doesn't have Karma installed, which is required for running Angular unit tests with `ng test`.

**Solution:** Add Karma and related dependencies to the frontend's package.json:

```json
{
  "devDependencies": {
    "karma": "~6.4.0",
    "karma-chrome-launcher": "~3.1.0",
    "karma-coverage": "~2.2.0",
    "karma-jasmine": "~5.1.0",
    "karma-jasmine-html-reporter": "~2.0.0",
    "@types/jasmine": "~4.3.0",
    "jasmine-core": "~4.5.0"
  },
  "scripts": {
    "test": "ng test",
    "test:ci": "ng test --no-watch --no-progress --browsers=ChromeHeadless --code-coverage"
  }
}
```

Then run:
```bash
npm install
# or
yarn install
```

**Note:** The GitHub Actions workflow will now detect if Karma is missing and skip the tests gracefully with a helpful message instead of failing.

### Issue 1: "--include" parameter not recognized

If you're using Karma with Jasmine, the `--include` parameter might not work. Instead, you can:

**Solution A:** Use a karma.conf.js configuration that supports file filtering:
```javascript
// karma.conf.js
module.exports = function(config) {
  config.set({
    // ... other config
    files: [
      { pattern: './src/**/*.spec.ts', included: true, watched: true }
    ],
  });
};
```

**Solution B:** Update package.json to support include patterns:
```json
{
  "scripts": {
    "test:ci": "ng test --watch=false --code-coverage --browsers=ChromeHeadless",
    "test:plugin": "ng test --watch=false --browsers=ChromeHeadless --include='**/time-planning-pn/**/*.spec.ts'"
  }
}
```

### Issue 2: ChromeHeadless not available

If ChromeHeadless browser is not configured:

**Solution:** Install and configure Chrome headless in karma.conf.js:
```javascript
// karma.conf.js
module.exports = function(config) {
  config.set({
    browsers: ['ChromeHeadless'],
    customLaunchers: {
      ChromeHeadlessCI: {
        base: 'ChromeHeadless',
        flags: ['--no-sandbox', '--disable-gpu']
      }
    }
  });
};
```

Then update package.json:
```json
{
  "scripts": {
    "test:ci": "ng test --watch=false --browsers=ChromeHeadlessCI"
  }
}
```

### Issue 3: Angular version compatibility

For Angular 15+, you might need to use the new test configuration:

```json
{
  "scripts": {
    "test": "ng test",
    "test:ci": "ng test --no-watch --no-progress --browsers=ChromeHeadless --code-coverage"
  }
}
```

### Issue 4: Jest instead of Karma

If your project uses Jest instead of Karma:

```json
{
  "scripts": {
    "test": "jest",
    "test:ci": "jest --ci --coverage --testPathPattern='time-planning-pn'"
  }
}
```

## Recommended Configuration

For the most compatibility with the time-planning-pn plugin tests, use this configuration:

```json
{
  "scripts": {
    "test": "ng test",
    "test:ci": "ng test --no-watch --no-progress --browsers=ChromeHeadless --code-coverage",
    "test:headless": "ng test --no-watch --browsers=ChromeHeadless"
  },
  "devDependencies": {
    "@angular-devkit/build-angular": "^15.0.0",
    "karma": "~6.4.0",
    "karma-chrome-launcher": "~3.1.0",
    "karma-coverage": "~2.2.0",
    "karma-jasmine": "~5.1.0",
    "karma-jasmine-html-reporter": "~2.0.0"
  }
}
```

## Workflow Behavior

The GitHub Actions workflow (`.github/workflows/dotnet-core-master.yml` and `dotnet-core-pr.yml`) will:

1. Check if `test:ci` script exists in package.json
2. If yes, run: `npm run test:ci -- --include='**/time-planning-pn/**/*.spec.ts'`
3. If no, check if `test` script exists
4. If yes, run: `npm run test -- --watch=false --browsers=ChromeHeadless --include='**/time-planning-pn/**/*.spec.ts'`
5. If neither exists, skip the tests with a message
6. The step has `continue-on-error: true`, so it won't fail the entire workflow

## Testing Locally

To test if your configuration works:

```bash
# Clone both repositories
git clone https://github.com/microting/eform-angular-frontend.git
git clone https://github.com/microting/eform-angular-timeplanning-plugin.git

# Copy plugin files
cp -r eform-angular-timeplanning-plugin/eform-client/src/app/plugins/modules/time-planning-pn \
      eform-angular-frontend/eform-client/src/app/plugins/modules/

# Install dependencies
cd eform-angular-frontend/eform-client
npm install

# Try running tests
npm run test:ci -- --include='**/time-planning-pn/**/*.spec.ts'
# or
npm run test -- --watch=false --browsers=ChromeHeadless
```

## Contact

If you need help configuring the tests, check:
- Angular CLI testing documentation: https://angular.io/guide/testing
- Karma configuration: https://karma-runner.github.io/latest/config/configuration-file.html
- The test files in this repository for examples

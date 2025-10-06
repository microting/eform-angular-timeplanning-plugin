# Package.json Configuration for Angular Unit Tests

## Overview
This document explains the package.json and related configuration files needed in the main `eform-angular-frontend` repository to support running unit tests for the time-planning-pn plugin.

## Required Changes to Frontend Repository

### 1. Update package.json

Add these dependencies and scripts to your `package.json`:

```json
{
  "scripts": {
    "test": "ng test",
    "test:ci": "ng test --no-watch --no-progress --browsers=ChromeHeadless --code-coverage"
  },
  "devDependencies": {
    "@types/jasmine": "~5.1.0",
    "jasmine-core": "~5.1.0",
    "karma": "~6.4.0",
    "karma-chrome-launcher": "~3.2.0",
    "karma-coverage": "~2.2.0",
    "karma-jasmine": "~5.1.0",
    "karma-jasmine-html-reporter": "~2.1.0"
  }
}
```

Then run:
```bash
npm install
# or
yarn install
```

### 2. Update src/test.ts

The `zone.js` import paths have changed in newer versions. Update your `src/test.ts` file:

**Remove these old imports:**
```typescript
import 'zone.js/dist/long-stack-trace-zone';
import 'zone.js/dist/proxy.js';
import 'zone.js/dist/sync-test';
import 'zone.js/dist/jasmine-patch';
import 'zone.js/dist/async-test';
import 'zone.js/dist/fake-async-test';
```

**Replace with these new imports:**
```typescript
import 'zone.js';
import 'zone.js/testing';
```

**Complete example of src/test.ts:**
```typescript
// This file is required by karma.conf.js and loads recursively all the .spec and framework files

import 'zone.js';
import 'zone.js/testing';
import { getTestBed } from '@angular/core/testing';
import {
  BrowserDynamicTestingModule,
  platformBrowserDynamicTesting
} from '@angular/platform-browser-dynamic/testing';

declare const require: {
  context(path: string, deep?: boolean, filter?: RegExp): {
    <T>(id: string): T;
    keys(): string[];
  };
};

// First, initialize the Angular testing environment.
getTestBed().initTestEnvironment(
  BrowserDynamicTestingModule,
  platformBrowserDynamicTesting(),
);

// Then we find all the tests.
const context = require.context('./', true, /\.spec\.ts$/);
// And load the modules.
context.keys().map(context);
```

### 3. Update tsconfig.spec.json

Add `@types/jasmine` to the types array:

```json
{
  "extends": "./tsconfig.json",
  "compilerOptions": {
    "outDir": "./out-tsc/spec",
    "types": [
      "jasmine",
      "node"
    ]
  },
  "include": [
    "src/**/*.spec.ts",
    "src/**/*.d.ts"
  ]
}
```

### 4. Update karma.conf.js

Ensure your `karma.conf.js` includes the coverage reporter:

```javascript
module.exports = function (config) {
  config.set({
    basePath: '',
    frameworks: ['jasmine', '@angular-devkit/build-angular'],
    plugins: [
      require('karma-jasmine'),
      require('karma-chrome-launcher'),
      require('karma-jasmine-html-reporter'),
      require('karma-coverage'),
      require('@angular-devkit/build-angular/plugins/karma')
    ],
    client: {
      jasmine: {
        // you can add configuration options for Jasmine here
        // the possible options are listed at https://jasmine.github.io/api/edge/Configuration.html
        // for example, you can disable the random execution with `random: false`
        // or set a specific seed with `seed: 4321`
      },
      clearContext: false // leave Jasmine Spec Runner output visible in browser
    },
    jasmineHtmlReporter: {
      suppressAll: true // removes the duplicated traces
    },
    coverageReporter: {
      dir: require('path').join(__dirname, './coverage'),
      subdir: '.',
      reporters: [
        { type: 'html' },
        { type: 'text-summary' },
        { type: 'lcovonly' }
      ]
    },
    reporters: ['progress', 'kjhtml'],
    browsers: ['Chrome'],
    customLaunchers: {
      ChromeHeadless: {
        base: 'Chrome',
        flags: [
          '--headless',
          '--disable-gpu',
          '--no-sandbox',
          '--remote-debugging-port=9222'
        ]
      }
    },
    restartOnFileChange: true
  });
};
```

## Required Test Scripts

The GitHub Actions workflows will try to run tests using one of these approaches (in order):

### Option 1: test:ci script (Recommended)
If your package.json has a `test:ci` script, it will use that:

```json
{
  "scripts": {
    "test:ci": "ng test --no-watch --no-progress --browsers=ChromeHeadless --code-coverage"
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

**Solution:** Add Karma and related dependencies to the frontend's package.json (see section 1 above).

**Note:** The GitHub Actions workflow will now detect if Karma is missing and skip the tests gracefully with a helpful message instead of failing.

### Issue 1: "Can not load reporter 'coverage'" Error

**Error message:**
```
ERROR [reporter]: Can not load reporter "coverage", it is not registered!
```

**Cause:** The `karma-coverage` package is not installed or not properly configured in karma.conf.js.

**Solution:** 
1. Install `karma-coverage`: `npm install --save-dev karma-coverage`
2. Add it to the plugins array in karma.conf.js (see section 4 above)
3. Configure the coverageReporter in karma.conf.js (see section 4 above)

### Issue 2: Zone.js Module Not Found Errors

**Error messages:**
```
Error: Module not found: Error: Package path ./dist/long-stack-trace-zone is not exported
Error: Module not found: Error: Package path ./dist/proxy.js is not exported
Error: Module not found: Error: Package path ./dist/sync-test is not exported
Error: Module not found: Error: Package path ./dist/jasmine-patch is not exported
Error: Module not found: Error: Package path ./dist/async-test is not exported
Error: Module not found: Error: Package path ./dist/fake-async-test is not exported
```

**Cause:** Zone.js v0.12.0+ changed its module exports. The old import paths no longer work.

**Solution:** Update `src/test.ts` to use the new import paths (see section 2 above):
```typescript
// Remove old imports:
// import 'zone.js/dist/long-stack-trace-zone';
// import 'zone.js/dist/proxy.js';
// etc...

// Use new imports:
import 'zone.js';
import 'zone.js/testing';
```

### Issue 3: TypeScript "Cannot find name 'describe'" Errors

**Error messages:**
```
error TS2593: Cannot find name 'describe'
error TS2304: Cannot find name 'beforeEach'
error TS2304: Cannot find name 'it'
error TS2304: Cannot find name 'expect'
```

**Cause:** TypeScript can't find the Jasmine type definitions.

**Solution:**
1. Install `@types/jasmine`: `npm install --save-dev @types/jasmine`
2. Update `tsconfig.spec.json` to include jasmine in the types array (see section 3 above)

### Issue 4: "--include" parameter not recognized

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
    "test:ci": "ng test --no-watch --no-progress --browsers=ChromeHeadless --code-coverage",
    "test:plugin": "ng test --no-watch --browsers=ChromeHeadless --include='**/time-planning-pn/**/*.spec.ts'"
  }
}
```

### Issue 5: ChromeHeadless not available

If ChromeHeadless browser is not configured:

**Solution:** Configure Chrome headless in karma.conf.js (see section 4 above for the customLaunchers configuration).

### Issue 6: Angular version compatibility

For Angular 15+, you might need to use the new test configuration:

```json
{
  "scripts": {
    "test": "ng test",
    "test:ci": "ng test --no-watch --no-progress --browsers=ChromeHeadless --code-coverage"
  }
}
```

### Issue 7: Jest instead of Karma

If your project uses Jest instead of Karma:

```json
{
  "scripts": {
    "test": "jest",
    "test:ci": "jest --ci --coverage --testPathPattern='time-planning-pn'"
  }
}
```

## Quick Setup Checklist

To enable unit tests in the frontend repository, complete these steps:

- [ ] Add dependencies to package.json (karma, jasmine, etc.)
- [ ] Run `npm install` or `yarn install`
- [ ] Update `src/test.ts` with new zone.js imports
- [ ] Update `tsconfig.spec.json` to include jasmine types
- [ ] Update `karma.conf.js` with coverage reporter configuration
- [ ] Add `test:ci` script to package.json
- [ ] Test locally: `npm run test:ci`

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
    "@types/jasmine": "~5.1.0",
    "jasmine-core": "~5.1.0",
    "karma": "~6.4.0",
    "karma-chrome-launcher": "~3.2.0",
    "karma-coverage": "~2.2.0",
    "karma-jasmine": "~5.1.0",
    "karma-jasmine-html-reporter": "~2.1.0"
  }
}
```

## Workflow Behavior

The GitHub Actions workflow (`.github/workflows/dotnet-core-master.yml` and `dotnet-core-pr.yml`) will:

1. Check if Karma is installed in node_modules
2. If not, skip tests with a helpful message
3. If yes, check if `test:ci` script exists in package.json
4. If yes, run: `npm run test:ci -- --include='**/time-planning-pn/**/*.spec.ts'`
5. If no, check if `test` script exists
6. If yes, run: `npm run test -- --watch=false --browsers=ChromeHeadless --include='**/time-planning-pn/**/*.spec.ts'`
7. If neither exists, skip the tests with a message
8. The step has `continue-on-error: true`, so it won't fail the entire workflow

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

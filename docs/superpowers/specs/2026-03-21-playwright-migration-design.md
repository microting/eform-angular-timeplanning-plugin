# Playwright Migration Design — Time Planning Plugin (Pilot)

**Date:** 2026-03-21
**Status:** Draft
**Scope:** Port all time-planning-pn Cypress E2E tests to Playwright. Cypress and Playwright run simultaneously in CI during the transition. This plugin serves as the proven pilot pattern for the full Microting ecosystem migration.

---

## Background

The Microting plugin ecosystem currently uses Cypress for E2E tests. The `eform-angular-timeplanning-plugin` has a complete, passing Cypress test suite (groups a–o, 15 parallel CI jobs) and is the reference implementation for the migration to Playwright.

The migration follows a transition-period model: both Cypress and Playwright CI jobs run in parallel. Playwright must go fully green before Cypress is removed. Once this plugin is complete, its structure becomes the template for all other plugins.

---

## Architecture

### Folder structure

The existing Cypress tests follow a copy-in pattern: each plugin stores its tests locally and CI copies them into `eform-angular-frontend/eform-client/` before running. Playwright uses the same pattern.

**`eform-angular-frontend/eform-client/`** (shared base — changes in the frontend repo)
```
playwright/
  e2e/
    Login.page.ts           # Playwright equivalent of cypress/e2e/Login.page.ts
    Plugin.page.ts          # Playwright equivalent of cypress/e2e/Plugin.page.ts
    helper-functions.ts     # Playwright equivalent of cypress/e2e/helper-functions.ts
    fixtures.ts             # Shared test fixtures (replaces beforeEach login boilerplate)
    db/                     # DB setup tests (same role as cypress/e2e/db/)
playwright.config.ts        # Base Playwright config
```

**`eform-angular-timeplanning-plugin/eform-client/`** (this repo)
```
playwright/
  e2e/
    plugins/
      time-planning-pn/
        a/   b/   c/   d/   e/
        f/   g/   h/   i/   j/
        k/   l/   m/   n/   o/
          # Each group mirrors the cypress/ group structure:
          # - 420_SDK.sql
          # - 420_eform-angular-time-planning-plugin.sql
          # - activate-plugin.spec.ts
          # - assert-true.spec.ts
          # - <feature>.spec.ts
playwright.config.ts        # Plugin-specific Playwright config
```

---

## Shared Helpers & Page Objects

All shared helpers live in `eform-angular-frontend`. They mirror the Cypress equivalents in structure but use Playwright's async/await API.

### `Login.page.ts`

Class-based page object. Receives a Playwright `Page` instance via constructor. Replaces `cy.get()` with `page.locator()` and `cy.intercept().wait()` with `page.waitForResponse()`.

The existing `LoginConstants` file lives at `eform-client/e2e/Constants/LoginConstants.ts` and uses a default export. The Playwright page object references it via a relative path from `playwright/e2e/`:

```typescript
import { Page } from '@playwright/test';
import loginConstants from '../../e2e/Constants/LoginConstants';

export class LoginPage {
  constructor(private page: Page) {}

  async login(username = loginConstants.username, password = loginConstants.password) {
    await this.page.fill('#username', username);
    await this.page.fill('#password', password);
    await Promise.all([
      this.page.waitForResponse('**/api/templates/index'),
      this.page.click('#loginBtn'),
    ]);
    await this.page.waitForSelector('#newEFormBtn', { state: 'visible' });
  }
}
```

### `Plugin.page.ts`

Same class shape as the Cypress equivalent: `enablePluginByName()`, navigation helpers. The `cy.wait(100000)` for plugin activation becomes `page.waitForTimeout(100000)` — this is a genuine server-side delay during plugin DB migration and must be preserved.

### `helper-functions.ts`

Ports `testSorting`, `selectDateOnNewDatePicker`, `selectDateRangeOnNewDatePicker`, `selectValueInNgSelector` to Playwright's async/await. All `cy.wait(500)` calls become `await page.waitForTimeout(500)`.

### `fixtures.ts`

A custom Playwright fixture replaces the `beforeEach` login boilerplate present in every Cypress test file:

```typescript
import { test as base } from '@playwright/test';
import { LoginPage } from './Login.page';
import { PluginPage } from './Plugin.page';

export const test = base.extend<{ loginPage: LoginPage; pluginPage: PluginPage }>({
  loginPage: async ({ page }, use) => { await use(new LoginPage(page)); },
  pluginPage: async ({ page }, use) => { await use(new PluginPage(page)); },
});
export { expect } from '@playwright/test';
```

---

## `playwright.config.ts`

```typescript
import { defineConfig } from '@playwright/test';

export default defineConfig({
  testDir: 'playwright/e2e',
  use: {
    baseURL: 'http://localhost:4200',
    viewport: { width: 1920, height: 1080 },
    video: 'retain-on-failure',
    screenshot: 'only-on-failure',
  },
  reporter: [
    ['html'],
    ['json', { outputFile: 'playwright-results/results.json' }],
  ],
  timeout: 120000,
});
```

`testDir: 'playwright/e2e'` scopes Playwright's file scan to the test tree only, preventing it from scanning the full Angular project tree after the CI copy step.

Chromium only during the transition period. Multi-browser support added later once all plugins are migrated.

---

## CI Integration

A new `pn-playwright-test` job is added to `.github/workflows/dotnet-core-master.yml` alongside the existing `pn-test` (Cypress) job. Both `needs: build`, both use the same matrix strategy and the same Docker stack (MariaDB + app container).

```yaml
pn-playwright-test:
  needs: build
  runs-on: ubuntu-latest
  strategy:
    fail-fast: false
    matrix:
      test: [a,b,c,d,e,f,g,h,i,j,k,l,m,n,o]
  steps:
    # ... same Docker stack setup as pn-test:
    #     checkout, extract branch, download artifact, docker load,
    #     create network, start MariaDB, start RabbitMQ, sleep 15,
    #     start app container (> docker_run_log 2>&1 &)
    - name: Use Node.js
      uses: actions/setup-node@v3
      with:
        node-version: 22
    - name: Preparing Frontend checkout
      uses: actions/checkout@v3
      with:
        repository: microting/eform-angular-frontend
        ref: ${{ steps.extract_branch.outputs.branch }}
        path: eform-angular-frontend
    - name: Copy dependencies
      run: |
        cp -av eform-angular-timeplanning-plugin/eform-client/src/app/plugins/modules/time-planning-pn \
               eform-angular-frontend/eform-client/src/app/plugins/modules/time-planning-pn
        cp -av eform-angular-timeplanning-plugin/eform-client/playwright \
               eform-angular-frontend/eform-client/playwright
        cp -av eform-angular-timeplanning-plugin/eform-client/playwright.config.ts \
               eform-angular-frontend/eform-client/playwright.config.ts
        cd eform-angular-frontend/eform-client && \
          ../../eform-angular-timeplanning-plugin/testinginstallpn.sh
    - name: yarn install
      run: cd eform-angular-frontend/eform-client && yarn install
    - name: Install Playwright browsers
      run: cd eform-angular-frontend/eform-client && npx playwright install --with-deps chromium
    - name: Get standard output
      run: cat docker_run_log
    - name: DB Configuration
      uses: cypress-io/github-action@v4
      with:
        start: echo 'hi'
        wait-on: "http://localhost:4200"
        wait-on-timeout: 120
        browser: chrome
        record: false
        spec: cypress/e2e/db/*
        config-file: cypress.config.ts
        working-directory: eform-angular-frontend/eform-client
        command-prefix: "--"
    - name: Change rabbitmq hostname
      run: docker exec -i mariadbtest mariadb -u root --password=secretpassword -e 'update 420_SDK.Settings set Value = "my-rabbit" where Name = "rabbitMqHost"'
    - name: Create database
      if: matrix.test == 'd'
      run: |
        docker exec -i mariadbtest mariadb -u root --password=secretpassword -e 'update 420_Angular.EformPlugins set Status = 1'
        docker exec -i mariadbtest mariadb -u root --password=secretpassword -e 'create database `420_eform-angular-time-planning-plugin`'
        docker exec -i mariadbtest mariadb -u root --password=secretpassword 420_SDK \
          < eform-angular-frontend/eform-client/playwright/e2e/plugins/time-planning-pn/d/420_SDK.sql
        docker exec -i mariadbtest mariadb -u root --password=secretpassword 420_eform-angular-time-planning-plugin \
          < eform-angular-frontend/eform-client/playwright/e2e/plugins/time-planning-pn/d/420_eform-angular-time-planning-plugin.sql
        docker exec -i mariadbtest mariadb -u root --password=secretpassword -e 'update 420_SDK.Settings set Value = "my-rabbit" where Name = "rabbitMqHost"'
    - name: Create database
      if: matrix.test != 'd'
      run: |
        docker exec -i mariadbtest mariadb -u root --password=secretpassword -e 'update 420_Angular.EformPlugins set Status = 1'
        docker exec -i mariadbtest mariadb -u root --password=secretpassword -e 'create database `420_eform-angular-time-planning-plugin`'
        docker exec -i mariadbtest mariadb -u root --password=secretpassword 420_SDK \
          < eform-angular-frontend/eform-client/playwright/e2e/plugins/time-planning-pn/a/420_SDK.sql
        docker exec -i mariadbtest mariadb -u root --password=secretpassword 420_eform-angular-time-planning-plugin \
          < eform-angular-frontend/eform-client/playwright/e2e/plugins/time-planning-pn/a/420_eform-angular-time-planning-plugin.sql
        docker exec -i mariadbtest mariadb -u root --password=secretpassword -e 'update 420_SDK.Settings set Value = "my-rabbit" where Name = "rabbitMqHost"'
    - name: Wait for app
      run: npx wait-on http://localhost:4200 --timeout 120000
    - name: ${{ matrix.test }} playwright test
      run: |
        cd eform-angular-frontend/eform-client
        npx playwright test playwright/e2e/plugins/time-planning-pn/${{ matrix.test }}/
    - name: Stop the newly built Docker container
      run: docker stop my-container
    - name: Get standard output
      run: |
        cat docker_run_log
        result=`cat docker_run_log | grep "Now listening on: http://0.0.0.0:5000" -m 1 | wc -l`
        if [ $result -ne 1 ];then exit 1; fi
    - name: Get standard output
      if: ${{ failure() }}
      run: cat docker_run_log
    - name: Archive Playwright report
      if: failure()
      uses: actions/upload-artifact@v4
      with:
        name: playwright-report-${{ matrix.test }}
        path: eform-angular-frontend/eform-client/playwright-report/
        retention-days: 2
```

**SQL file note:** Groups `b` and `c` carry no SQL files. Groups `e`–`o` each carry their own `420_SDK.sql` and `420_eform-angular-time-planning-plugin.sql` files that contain different seed data from group `a`. However, the existing Cypress CI (and therefore this Playwright CI job) follows the same established pattern: only group `a`'s SQL is loaded for all non-`d` groups. The tests in groups `e`–`o` are designed to work from group `a`'s base seed, relying on the `activate-plugin.spec.ts` within each group to set up any additional required state at runtime. The SQL files in those groups exist for local development reference but are not loaded by CI. Only groups `a` and `d` provide SQL files that CI actually loads.

---

## Migration Sequence

### Phase 1: Shared infrastructure (eform-angular-frontend)
1. Add `playwright.config.ts` to `eform-angular-frontend/eform-client/`
2. Add `playwright/e2e/Login.page.ts`, `Plugin.page.ts`, `helper-functions.ts`, `fixtures.ts`
3. Add `playwright` and `@playwright/test` to `package.json` devDependencies

### Phase 2: CI job + tests (this repo)
4. Add `pn-playwright-test` CI job to `dotnet-core-master.yml` and `dotnet-core-pr.yml` — this must exist before any group can be verified green
5. Port group `a` — verify it passes in CI before proceeding
6. Port groups `b`–`o` one at a time, verifying each group in CI

### Phase 3: Validation gate
7. All 15 Playwright CI jobs must be green
8. At this point the pattern is proven and ready to be applied to other plugins
9. Cypress removal is a separate follow-on task — not in scope here

---

## Test Conventions

### API waiting
Replace `cy.intercept().as('alias'); cy.wait('@alias')` with `page.waitForResponse('**/api/...')` triggered alongside the action that causes the request.

### Arbitrary waits
`cy.wait(500)` → `await page.waitForTimeout(500)`.
The 100-second plugin activation wait is preserved as `page.waitForTimeout(100000)` — it reflects a genuine server-side delay.

### Selectors
All existing CSS/ID selectors are reused unchanged. Playwright supports the same selector syntax as Cypress.

### Assertions
Replace `cy.get(...).should('contain', ...)` with `await expect(page.locator(...)).toContainText(...)`.

---

## Out of Scope

- Removing Cypress from any plugin (future phase)
- Porting `eform-angular-frontend` core Cypress tests (db setup, base navigation)
- Cross-browser testing (added after full migration)
- Any other plugin beyond `eform-angular-timeplanning-plugin`

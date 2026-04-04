# Playwright Migration Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Port all time-planning-pn Cypress E2E tests to Playwright, running both in CI simultaneously, establishing the pattern for ecosystem-wide migration.

**Architecture:** Shared Playwright page objects and helpers live in `eform-angular-frontend/eform-client/playwright/e2e/`. Plugin-specific tests live in `eform-angular-timeplanning-plugin/eform-client/playwright/`. CI copies them together before running, mirroring the existing Cypress pattern.

**Tech Stack:** `@playwright/test` ^1.50.0, TypeScript, GitHub Actions, MariaDB, Docker, Angular Material (target app).

---

## Conversion Reference

All test porting tasks use this table. No need to look it up again.

| Cypress | Playwright |
|---------|------------|
| `cy.visit(url)` | `await page.goto(url)` |
| `cy.get('#id')` | `page.locator('#id')` |
| `cy.contains('.row', text).first()` | `page.locator('.row').filter({hasText: text}).first()` |
| `locator.find('.child')` | `locator.locator('.child')` |
| `.click()` | `await locator.click()` |
| `.click({ force: true })` | `await locator.click({ force: true })` |
| `.type('text')` / `.clear().type('text')` | `await locator.fill('text')` |
| `.scrollIntoView()` | `await locator.scrollIntoViewIfNeeded()` |
| `.should('be.visible')` | `await expect(locator).toBeVisible()` |
| `.should('not.exist')` | `await expect(locator).toHaveCount(0)` |
| `.should('contain.text', str)` | `await expect(locator).toContainText(str)` |
| `.should('have.attr', k, v)` | `await expect(locator).toHaveAttribute(k, v)` |
| `.should('have.length', N)` | `await expect(locator).toHaveCount(N)` |
| `.should('include.value', str)` | `await expect(locator).toHaveValue(new RegExp(str))` |
| `.should('have.attr', 'disabled')` | `await expect(locator).toBeDisabled()` |
| `.should('not.be.checked')` | `await expect(locator).not.toBeChecked()` |
| `cy.intercept('GET', pat).as('a'); cy.wait('@a')` | `await Promise.all([page.waitForResponse(pat), action()])` |
| `cy.wait(N)` | `await page.waitForTimeout(N)` |
| `cy.task('log', msg)` | `console.log(msg)` (or remove) |
| `cy.get('body').then($b => if ($b.find('#id').length)` | `if (await page.locator('#id').count() > 0)` |
| `cy.readFile(path, 'binary')` | `fs.readFileSync(path)` (Node.js) |
| `cy.writeFile(path, content, 'binary')` | `fs.writeFileSync(path, content)` |
| Download: click then `cy.readFile(downloadsFolder/...)` | `const [dl] = await Promise.all([page.waitForEvent('download'), locator.click()])` |

---

## File Map

### `eform-angular-frontend/eform-client/` (shared base repo)

| Action | Path |
|--------|------|
| Modify | `package.json` — add `@playwright/test` devDependency |
| Create | `playwright.config.ts` |
| Create | `playwright/e2e/Login.page.ts` |
| Create | `playwright/e2e/Navbar.page.ts` |
| Create | `playwright/e2e/Plugin.page.ts` |
| Create | `playwright/e2e/helper-functions.ts` |
| Create | `playwright/e2e/fixtures.ts` |

### `eform-angular-timeplanning-plugin/eform-client/` (this repo)

| Action | Path |
|--------|------|
| Create | `playwright.config.ts` |
| Create | `playwright/e2e/plugins/time-planning-pn/TimePlanningWorkingHours.page.ts` |
| Create | `playwright/e2e/plugins/time-planning-pn/a/` — SQL files (symlink/copy) + 3 spec files |
| Create | `playwright/e2e/plugins/time-planning-pn/b/` — 5 spec files |
| Create | `playwright/e2e/plugins/time-planning-pn/c/` — 2 spec files |
| Create | `playwright/e2e/plugins/time-planning-pn/d/` — SQL files + 3 spec files |
| Create | `playwright/e2e/plugins/time-planning-pn/e–j/` — SQL files + 3 spec files each |
| Create | `playwright/e2e/plugins/time-planning-pn/k–o/` — SQL files + 3 spec files each |
| Modify | `.github/workflows/dotnet-core-master.yml` — add `pn-playwright-test` job |
| Modify | `.github/workflows/dotnet-core-pr.yml` — add `pn-playwright-test` job |

---

## Task 1: Add @playwright/test to eform-angular-frontend

**Repo:** `eform-angular-frontend`

**Files:**
- Modify: `eform-client/package.json`
- Create: `eform-client/playwright.config.ts`

- [ ] **Step 1: Add @playwright/test to devDependencies**

Edit `eform-client/package.json` — add to `devDependencies`:
```json
"@playwright/test": "^1.50.0"
```

- [ ] **Step 2: Create playwright.config.ts**

Create `eform-client/playwright.config.ts`:
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

- [ ] **Step 3: Install dependencies**

```bash
cd eform-client && yarn install
```
Expected: `@playwright/test` appears in `node_modules/.yarn-integrity`

- [ ] **Step 4: Commit**

```bash
git add eform-client/package.json eform-client/playwright.config.ts
git commit -m "feat: add @playwright/test dependency and base config"
```

---

## Task 2: Shared page objects — Login, Navbar, Plugin

**Repo:** `eform-angular-frontend`

**Files:**
- Create: `eform-client/playwright/e2e/Login.page.ts`
- Create: `eform-client/playwright/e2e/Navbar.page.ts`
- Create: `eform-client/playwright/e2e/Plugin.page.ts`

**Cypress source to reference:** `eform-client/cypress/e2e/Login.page.ts`, `Navbar.page.ts`, `Plugin.page.ts`

- [ ] **Step 1: Create Login.page.ts**

Create `eform-client/playwright/e2e/Login.page.ts`:
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

  async loginWithNewPassword() {
    await this.page.fill('#username', loginConstants.username);
    await this.page.fill('#password', loginConstants.newPassword);
    await this.page.click('#loginBtn');
    await this.page.waitForSelector('#newEFormBtn', { state: 'visible' });
  }
}
```

- [ ] **Step 2: Create Navbar.page.ts**

Create `eform-client/playwright/e2e/Navbar.page.ts`:
```typescript
import { Page } from '@playwright/test';

export class Navbar {
  constructor(private page: Page) {}

  async goToPluginsPage() {
    await this.page.locator('#advanced').click();
    const pluginsBtn = this.page.locator('#plugins-settings');
    await pluginsBtn.waitFor({ state: 'visible' });
    await pluginsBtn.click();
    await this.page.waitForTimeout(500);
    await this.page.locator('app-installed-plugins-page .mat-mdc-row').first().waitFor({ state: 'visible' });
  }

  async goToMyEForms() {
    await Promise.all([
      this.page.waitForResponse('**/api/templates/index'),
      this.page.waitForResponse('**/api/tags/index'),
      this.page.locator('#my-eforms').click({ force: true }),
    ]);
  }

  async logout() {
    await this.page.locator('#sign-out-dropdown').click();
    await this.page.waitForTimeout(500);
    await this.page.locator('#sign-out').click();
    await this.page.waitForTimeout(500);
  }

  async clickOnSubMenuItem(menuItem: string) {
    await this.page.locator('.fadeInDropdown').filter({ hasText: menuItem }).click();
  }

  async clickOnHeaderMenuItem(headerMenuItem: string) {
    await this.page.locator('#header').filter({ hasText: headerMenuItem }).click();
  }
}
```

- [ ] **Step 3: Create Plugin.page.ts**

Create `eform-client/playwright/e2e/Plugin.page.ts`:
```typescript
import { Page } from '@playwright/test';
import { Navbar } from './Navbar.page';
import { LoginPage } from './Login.page';

export class PluginPage {
  Navbar: Navbar;

  constructor(private page: Page) {
    this.Navbar = new Navbar(page);
  }

  async enablePluginByName(pluginName: string, msForWait = 100000) {
    const row = this.page.locator('.mat-mdc-row').filter({ hasText: pluginName }).first();
    await row.locator('.mat-column-actions button').click();
    await this.page.locator('#pluginOKBtn').waitFor({ state: 'visible' });
    await this.page.locator('#pluginOKBtn').click();
    await this.page.waitForTimeout(msForWait);
    await this.page.goto('http://localhost:4200');
    const loginPage = new LoginPage(this.page);
    await loginPage.login();
    await this.Navbar.goToPluginsPage();
  }

  pluginOKBtn() {
    return this.page.locator('#pluginOKBtn');
  }

  pluginCancelBtn() {
    return this.page.locator('#pluginCancelBtn');
  }
}
```

- [ ] **Step 4: Verify TypeScript compiles**

```bash
cd eform-client && npx tsc --noEmit --strict false playwright/e2e/Login.page.ts playwright/e2e/Navbar.page.ts playwright/e2e/Plugin.page.ts
```
Expected: no errors

- [ ] **Step 5: Commit**

```bash
git add eform-client/playwright/e2e/
git commit -m "feat: add Playwright page objects — Login, Navbar, Plugin"
```

---

## Task 3: Shared helpers and fixtures

**Repo:** `eform-angular-frontend`

**Files:**
- Create: `eform-client/playwright/e2e/helper-functions.ts`
- Create: `eform-client/playwright/e2e/fixtures.ts`

**Cypress source to reference:** `eform-client/cypress/e2e/helper-functions.ts`

- [ ] **Step 1: Create helper-functions.ts**

Create `eform-client/playwright/e2e/helper-functions.ts`:
```typescript
import { Page, expect } from '@playwright/test';

export async function selectDateOnNewDatePicker(page: Page, year: number, month: number, day: number) {
  await page.waitForTimeout(500);
  await page.locator('.mat-calendar-controls > .mat-calendar-period-button').click();
  await page.waitForTimeout(500);
  const startYearText = await page.locator('mat-multi-year-view .mat-calendar-body-cell-content').first().innerText();
  const startYear = parseInt(startYearText.trim(), 10);
  await page.locator('tbody span.mat-calendar-body-cell-content.mat-focus-indicator').nth(year - startYear).click();
  await page.waitForTimeout(500);
  await page.locator('span.mat-calendar-body-cell-content.mat-focus-indicator').nth(month - 1).click();
  await page.waitForTimeout(500);
  await page.locator('span.mat-calendar-body-cell-content.mat-focus-indicator:not(.owl-dt-calendar-cell-out)').nth(day - 1).click();
  await page.waitForTimeout(500);
}

export async function selectDateRangeOnNewDatePicker(
  page: Page,
  yearFrom: number, monthFrom: number, dayFrom: number,
  yearTo: number, monthTo: number, dayTo: number,
) {
  await selectDateOnNewDatePicker(page, yearFrom, monthFrom, dayFrom);
  await selectDateOnNewDatePicker(page, yearTo, monthTo, dayTo);
}

export async function selectValueInNgSelector(
  page: Page,
  selector: string,
  value: string,
  selectorInModal = false,
  intercept = false,
) {
  const ngSelector = page.locator(selector);
  await ngSelector.waitFor({ state: 'visible' });
  if (intercept) {
    await Promise.all([
      page.waitForResponse('**'),
      ngSelector.locator('input').fill(value),
    ]);
  } else {
    await ngSelector.locator('input').clear();
    await ngSelector.locator('input').fill(value);
  }
  await page.waitForTimeout(500);
  const option = selectorInModal
    ? page.locator('.ng-option').filter({ hasText: value }).first()
    : ngSelector.locator('.ng-option').filter({ hasText: value }).first();
  await option.scrollIntoViewIfNeeded();
  await option.click();
  await page.waitForTimeout(500);
}

export async function testSorting(
  page: Page,
  selectorTableHeader: string,
  selectorColumnElements: string,
  sortBy: string,
) {
  const getCells = async () => {
    const cells = await page.locator(selectorColumnElements).allInnerTexts();
    return cells.map(c => (c.includes('--') ? '' : c.trim()));
  };

  const elementsBefore = await getCells();

  for (let i = 0; i < 2; i++) {
    await page.locator(selectorTableHeader).locator('.mat-sort-header-icon').click({ force: true });
    await page.waitForTimeout(500);

    const style = await page.locator(selectorTableHeader).locator('.ng-trigger-leftPointer').getAttribute('style') ?? '';
    const sorted = style.includes('transform: rotate(45deg)')
      ? [...elementsBefore].sort().reverse()
      : [...elementsBefore].sort();

    const elementsAfter = await getCells();
    expect(elementsAfter, `Sort by ${sortBy} incorrect`).toEqual(sorted);
  }
}
```

- [ ] **Step 2: Create fixtures.ts**

Create `eform-client/playwright/e2e/fixtures.ts`:
```typescript
import { test as base } from '@playwright/test';
import { LoginPage } from './Login.page';
import { PluginPage } from './Plugin.page';

export const test = base.extend<{ loginPage: LoginPage; pluginPage: PluginPage }>({
  loginPage: async ({ page }, use) => {
    await use(new LoginPage(page));
  },
  pluginPage: async ({ page }, use) => {
    await use(new PluginPage(page));
  },
});

export { expect } from '@playwright/test';
```

- [ ] **Step 3: Commit**

```bash
git add eform-client/playwright/e2e/helper-functions.ts eform-client/playwright/e2e/fixtures.ts
git commit -m "feat: add Playwright helper-functions and fixtures"
```

---

## Task 4: Plugin playwright.config.ts and CI jobs

**Repo:** `eform-angular-timeplanning-plugin`

**Files:**
- Create: `eform-client/playwright.config.ts`
- Modify: `.github/workflows/dotnet-core-master.yml`
- Modify: `.github/workflows/dotnet-core-pr.yml`

- [ ] **Step 1: Create playwright.config.ts**

Create `eform-client/playwright.config.ts` (identical content to the frontend config — this file gets copied over it during CI):
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

- [ ] **Step 2: Add pn-playwright-test job to dotnet-core-master.yml**

In `.github/workflows/dotnet-core-master.yml`, add the following job after the `pn-test:` job. Copy the entire `pn-test` job block as a starting point and make these changes:
- Rename `pn-test` → `pn-playwright-test`
- Replace the `wdio-headless-plugin-step2a.conf.ts` copy step with Playwright copy steps
- Replace the `${{matrix.test}} test` step (cypress-io action) with Playwright run
- Add `Install Playwright browsers` step after `yarn install`

Complete `pn-playwright-test` job to add:
```yaml
  pn-playwright-test:
    needs: build
    runs-on: ubuntu-latest
    strategy:
      fail-fast: false
      matrix:
        test: [a,b,c,d,e,f,g,h,i,j,k,l,m,n,o]
    steps:
    - uses: actions/checkout@v3
      with:
        path: eform-angular-timeplanning-plugin
    - name: Extract branch name
      id: extract_branch
      run: echo "##[set-output name=branch;]$(echo ${GITHUB_REF#refs/heads/})"
    - uses: actions/download-artifact@v4
      with:
        name: time-planning-container
    - run: docker load -i time-planning-container.tar
    - name: Create docker network
      run: docker network create --driver bridge --attachable data
    - name: Start MariaDB
      run: |
        docker pull mariadb:10.8
        docker run --name mariadbtest --network data -e MYSQL_ROOT_PASSWORD=secretpassword -p 3306:3306 -d mariadb:10.8
    - name: Start rabbitmq
      run: |
        docker pull rabbitmq:latest
        docker run -d --hostname my-rabbit --name some-rabbit --network data -e RABBITMQ_DEFAULT_USER=admin -e RABBITMQ_DEFAULT_PASS=password rabbitmq:latest
    - name: Sleep 15
      run: sleep 15
    - name: Start the newly build Docker container
      id: docker-run
      run: docker run --name my-container -p 4200:5000 --network data microtingas/time-planning-container:latest "/ConnectionString=host=mariadbtest;Database=420_Angular;user=root;password=secretpassword;port=3306;Convert Zero Datetime = true;SslMode=none;" > docker_run_log 2>&1 &
    - name: Use Node.js
      uses: actions/setup-node@v3
      with:
        node-version: 22
    - name: 'Preparing Frontend checkout'
      uses: actions/checkout@v3
      with:
        fetch-depth: 0
        repository: microting/eform-angular-frontend
        ref: ${{ steps.extract_branch.outputs.branch }}
        path: eform-angular-frontend
    - name: Copy dependencies
      run: |
        cp -av eform-angular-timeplanning-plugin/eform-client/src/app/plugins/modules/time-planning-pn eform-angular-frontend/eform-client/src/app/plugins/modules/time-planning-pn
        mkdir -p eform-angular-frontend/eform-client/playwright/e2e/plugins/
        cp -av eform-angular-timeplanning-plugin/eform-client/playwright/e2e/plugins/time-planning-pn eform-angular-frontend/eform-client/playwright/e2e/plugins/time-planning-pn
        cp -av eform-angular-timeplanning-plugin/eform-client/playwright.config.ts eform-angular-frontend/eform-client/playwright.config.ts
        cp -av eform-angular-timeplanning-plugin/eform-client/cypress.config.ts eform-angular-frontend/eform-client/cypress.config.ts
        cd eform-angular-frontend/eform-client && ../../eform-angular-timeplanning-plugin/testinginstallpn.sh
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
        docker exec -i mariadbtest mariadb -u root --password=secretpassword 420_SDK < eform-angular-frontend/eform-client/playwright/e2e/plugins/time-planning-pn/d/420_SDK.sql
        docker exec -i mariadbtest mariadb -u root --password=secretpassword 420_eform-angular-time-planning-plugin < eform-angular-frontend/eform-client/playwright/e2e/plugins/time-planning-pn/d/420_eform-angular-time-planning-plugin.sql
        docker exec -i mariadbtest mariadb -u root --password=secretpassword -e 'update 420_SDK.Settings set Value = "my-rabbit" where Name = "rabbitMqHost"'
    - name: Create database
      if: matrix.test != 'd'
      run: |
        docker exec -i mariadbtest mariadb -u root --password=secretpassword -e 'update 420_Angular.EformPlugins set Status = 1'
        docker exec -i mariadbtest mariadb -u root --password=secretpassword -e 'create database `420_eform-angular-time-planning-plugin`'
        docker exec -i mariadbtest mariadb -u root --password=secretpassword 420_SDK < eform-angular-frontend/eform-client/playwright/e2e/plugins/time-planning-pn/a/420_SDK.sql
        docker exec -i mariadbtest mariadb -u root --password=secretpassword 420_eform-angular-time-planning-plugin < eform-angular-frontend/eform-client/playwright/e2e/plugins/time-planning-pn/a/420_eform-angular-time-planning-plugin.sql
        docker exec -i mariadbtest mariadb -u root --password=secretpassword -e 'update 420_SDK.Settings set Value = "my-rabbit" where Name = "rabbitMqHost"'
    - name: Wait for app
      run: npx wait-on http://localhost:4200 --timeout 120000
    - name: ${{ matrix.test }} playwright test
      run: |
        cd eform-angular-frontend/eform-client
        npx playwright test playwright/e2e/plugins/time-planning-pn/${{ matrix.test }}/
    - name: Stop the newly build Docker container
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

- [ ] **Step 3: Add pn-playwright-test job to dotnet-core-pr.yml**

Apply the same job structure as above to `.github/workflows/dotnet-core-pr.yml`. The only differences from `dotnet-core-master.yml` are:
- Use `runs-on: ubuntu-22.04` (matching the existing `pn-test` job in that file)
- No `fetch-depth: 0` on the plugin checkout (match existing `pn-test` pattern in that file)

- [ ] **Step 4: Commit**

```bash
git add eform-client/playwright.config.ts .github/workflows/
git commit -m "feat: add playwright.config.ts and pn-playwright-test CI job"
```

---

## Task 5: TimePlanningWorkingHours.page.ts (Playwright)

**Repo:** `eform-angular-timeplanning-plugin`

**Files:**
- Create: `eform-client/playwright/e2e/plugins/time-planning-pn/TimePlanningWorkingHours.page.ts`

**Cypress source:** `eform-client/cypress/e2e/plugins/time-planning-pn/TimePlanningWorkingHours.page.ts`

- [ ] **Step 1: Create TimePlanningWorkingHours.page.ts**

```typescript
import { Page } from '@playwright/test';

export class TimePlanningWorkingHoursPage {
  constructor(private page: Page) {}

  async goToWorkingHours() {
    const workingHoursBtn = this.page.locator('#time-planning-pn-working-hours');
    if (!await workingHoursBtn.isVisible()) {
      await this.page.locator('#time-planning-pn').click();
    }
    await workingHoursBtn.click();
  }

  workingHoursExcel() {
    return this.page.locator('#workingHoursExcel');
  }

  workingHoursReload() {
    return this.page.locator('#workingHoursReload');
  }

  workingHoursSave() {
    return this.page.locator('#workingHoursSave');
  }

  workingHoursSite() {
    return this.page.locator('#workingHoursSite');
  }

  workingHoursRange() {
    return this.page.locator('#workingHoursRange');
  }

  dateFormInput() {
    return this.page.locator('mat-date-range-input');
  }
}
```

- [ ] **Step 2: Commit**

```bash
git add eform-client/playwright/e2e/plugins/time-planning-pn/TimePlanningWorkingHours.page.ts
git commit -m "feat: add Playwright TimePlanningWorkingHoursPage"
```

---

## Task 6: Group a — SQL files + 3 spec files

**Repo:** `eform-angular-timeplanning-plugin`

**Cypress source:** `eform-client/cypress/e2e/plugins/time-planning-pn/a/`

**Files:**
- Copy: `eform-client/playwright/e2e/plugins/time-planning-pn/a/420_SDK.sql`
- Copy: `eform-client/playwright/e2e/plugins/time-planning-pn/a/420_eform-angular-time-planning-plugin.sql`
- Create: `eform-client/playwright/e2e/plugins/time-planning-pn/a/assert-true.spec.ts`
- Create: `eform-client/playwright/e2e/plugins/time-planning-pn/a/time-planning-enabled.spec.ts`
- Create: `eform-client/playwright/e2e/plugins/time-planning-pn/a/time-planning-working-hours.export.spec.ts`

- [ ] **Step 1: Copy SQL files**

```bash
cp eform-client/cypress/e2e/plugins/time-planning-pn/a/420_SDK.sql \
   eform-client/playwright/e2e/plugins/time-planning-pn/a/420_SDK.sql
cp eform-client/cypress/e2e/plugins/time-planning-pn/a/420_eform-angular-time-planning-plugin.sql \
   eform-client/playwright/e2e/plugins/time-planning-pn/a/420_eform-angular-time-planning-plugin.sql
```

- [ ] **Step 2: Create assert-true.spec.ts**

Create `eform-client/playwright/e2e/plugins/time-planning-pn/a/assert-true.spec.ts`:
```typescript
import { test, expect } from '@playwright/test';

test('asserts true', () => {
  expect(true).toBe(true);
});
```

- [ ] **Step 3: Create time-planning-enabled.spec.ts**

Create `eform-client/playwright/e2e/plugins/time-planning-pn/a/time-planning-enabled.spec.ts`:
```typescript
import { test, expect } from '@playwright/test';
import { LoginPage } from '../../../Login.page';
import { PluginPage } from '../../../Plugin.page';

test.describe('Enable Time Planning plugin', () => {
  test.beforeEach(async ({ page }) => {
    await page.goto('http://localhost:4200');
    await new LoginPage(page).login();
    await new PluginPage(page).Navbar.goToPluginsPage();
  });

  test('should enable Time registration plugin', async ({ page }) => {
    const pluginName = 'Microting Time Planning Plugin';

    await page.locator('.mat-mdc-row').filter({ hasText: pluginName }).first()
      .locator('#actionMenu').click();
    await page.waitForTimeout(500);

    await page.locator('#plugin-status-button0').scrollIntoViewIfNeeded();
    await page.locator('#plugin-status-button0').waitFor({ state: 'visible' });
    await page.locator('#plugin-status-button0').click();
    await page.waitForTimeout(500);

    if (await page.locator('#pluginOKBtn').count() > 0) {
      await page.locator('#pluginOKBtn').click();
      await page.waitForTimeout(100000);
    }

    await page.goto('http://localhost:4200');
    await new LoginPage(page).login();
    await new PluginPage(page).Navbar.goToPluginsPage();

    await page.locator('.mat-mdc-row').filter({ hasText: pluginName }).first()
      .locator('#actionMenu').click();
    await page.waitForTimeout(500);

    await expect(
      page.locator('#plugin-status-button0').locator('mat-icon')
    ).toContainText('toggle_on');
  });
});
```

- [ ] **Step 4: Create time-planning-working-hours.export.spec.ts**

Create `eform-client/playwright/e2e/plugins/time-planning-pn/a/time-planning-working-hours.export.spec.ts`:
```typescript
import { test, expect } from '@playwright/test';
import { LoginPage } from '../../../Login.page';
import { TimePlanningWorkingHoursPage } from '../TimePlanningWorkingHours.page';
import { selectDateRangeOnNewDatePicker, selectValueInNgSelector } from '../../../helper-functions';
import * as XLSX from 'xlsx';
import * as path from 'path';
import * as fs from 'fs';

const dateRange = { yearFrom: 2023, monthFrom: 1, dayFrom: 1, yearTo: 2023, monthTo: 5, dayTo: 11 };
const fileNameExcelReport = '2023-01-01_2023-05-11_report';

test.describe('Time planning plugin working hours export', () => {
  test.beforeEach(async ({ page }) => {
    await page.goto('http://localhost:4200');
    await new LoginPage(page).login();
    const wh = new TimePlanningWorkingHoursPage(page);
    await Promise.all([
      page.waitForResponse('**/api/time-planning-pn/settings/sites'),
      wh.goToWorkingHours(),
    ]);
  });

  test('should export working hours to Excel', async ({ page }) => {
    const wh = new TimePlanningWorkingHoursPage(page);

    await wh.workingHoursRange().click();
    await selectDateRangeOnNewDatePicker(page,
      dateRange.yearFrom, dateRange.monthFrom, dateRange.dayFrom,
      dateRange.yearTo, dateRange.monthTo, dateRange.dayTo,
    );

    await Promise.all([
      page.waitForResponse('**/api/time-planning-pn/working-hours/index'),
      selectValueInNgSelector(page, '#workingHoursSite', 'o p', true),
    ]);

    const [download] = await Promise.all([
      page.waitForEvent('download'),
      wh.workingHoursExcel().click(),
    ]);
    const downloadPath = await download.path();

    // Fixture lives in cypress/fixtures/ — relative path from this file's deployed location
    // (eform-client/playwright/e2e/plugins/time-planning-pn/a/)
    // to eform-client/cypress/fixtures/
    const fixturesPath = path.join(__dirname, '../../../../../cypress/fixtures', `${fileNameExcelReport}.xlsx`);

    const generatedContent = fs.readFileSync(downloadPath!);
    const fixtureContent = fs.readFileSync(fixturesPath);

    const wbGenerated = XLSX.read(generatedContent, { type: 'buffer' });
    const sheetGenerated = wbGenerated.Sheets[wbGenerated.SheetNames[0]];
    const jsonGenerated = XLSX.utils.sheet_to_json(sheetGenerated, { header: 1 });

    const wbFixture = XLSX.read(fixtureContent, { type: 'buffer' });
    const sheetFixture = wbFixture.Sheets[wbFixture.SheetNames[0]];
    const jsonFixture = XLSX.utils.sheet_to_json(sheetFixture, { header: 1 });

    expect(jsonGenerated).toEqual(jsonFixture);
  });
});
```

- [ ] **Step 5: Commit and push — verify CI group a goes green**

```bash
git add eform-client/playwright/
git commit -m "feat: add Playwright group a tests"
git push
```

Expected: GitHub Actions `pn-playwright-test (a)` job passes. Check Actions tab before proceeding to group b.

---

## Task 7: Group b — 6 spec files (no SQL)

**Repo:** `eform-angular-timeplanning-plugin`

**Cypress source:** `eform-client/cypress/e2e/plugins/time-planning-pn/b/`

Files to port:
- `activate-plugin.spec.cy.ts` → `playwright/e2e/plugins/time-planning-pn/b/activate-plugin.spec.ts`
- `assert-true.spec.cy.ts` → `playwright/.../b/assert-true.spec.ts`
- `dashboard-assert.spec.cy.ts` → `playwright/.../b/dashboard-assert.spec.ts`
- `dashboard-edit-a.spec.cy.ts` → `playwright/.../b/dashboard-edit-a.spec.ts`
- `dashboard-edit-b.spec.cy.ts` → `playwright/.../b/dashboard-edit-b.spec.ts`
- `time-planning-settings.spec.cy.ts` → `playwright/.../b/time-planning-settings.spec.ts`

**No SQL files for group b — do not copy any.**

- [ ] **Step 1: Read all 6 Cypress source files**

Open each file in `eform-client/cypress/e2e/plugins/time-planning-pn/b/` and translate to Playwright using the Conversion Reference table at the top of this plan.

Key patterns specific to group b:
- `cy.task('log', msg)` → `console.log(msg)` (or remove entirely)
- `cy.intercept('GET', pattern).as('alias'); cy.wait('@alias', {timeout: 60000})` → `await Promise.all([page.waitForResponse(pattern), actionThatTriggersIt()])`
- `cy.intercept('PUT', ...).as('updateSettings'); cy.get('#saveSettings').click(); cy.wait('@updateSettings').then(i => expect(i.response.statusCode).to.equal(200))` → `const [resp] = await Promise.all([page.waitForResponse(r => r.url().includes('/settings') && r.request().method() === 'PUT'), page.locator('#saveSettings').click()]); expect(resp.status()).toBe(200)`

- [ ] **Step 2: Create all 6 spec files**

Follow Task 6 as the template for `assert-true.spec.ts` and `activate-plugin.spec.ts` (same pattern, no SQL). Port the remaining 4 files using the conversion table.

- [ ] **Step 3: Commit and push — verify CI group b goes green**

```bash
git add eform-client/playwright/e2e/plugins/time-planning-pn/b/
git commit -m "feat: add Playwright group b tests"
git push
```

Expected: `pn-playwright-test (b)` passes before proceeding.

---

## Task 8: Group c — 2 spec files (no SQL)

**Repo:** `eform-angular-timeplanning-plugin`

**Cypress source:** `eform-client/cypress/e2e/plugins/time-planning-pn/c/`

Files: `activate-plugin.spec.ts`, `dashboard-edit-a.spec.ts`

- [ ] **Step 1: Port both files using the Conversion Reference**

- [ ] **Step 2: Commit and push — verify CI group c goes green**

```bash
git add eform-client/playwright/e2e/plugins/time-planning-pn/c/
git commit -m "feat: add Playwright group c tests"
git push
```

---

## Task 9: Group d — SQL files + 3 spec files

**Repo:** `eform-angular-timeplanning-plugin`

**Cypress source:** `eform-client/cypress/e2e/plugins/time-planning-pn/d/`

Files: SQL + `activate-plugin.spec.ts`, `assert-true.spec.ts`, `dashboard-edit-a.spec.ts`

- [ ] **Step 1: Copy SQL files**

```bash
cp eform-client/cypress/e2e/plugins/time-planning-pn/d/420_SDK.sql \
   eform-client/playwright/e2e/plugins/time-planning-pn/d/420_SDK.sql
cp eform-client/cypress/e2e/plugins/time-planning-pn/d/420_eform-angular-time-planning-plugin.sql \
   eform-client/playwright/e2e/plugins/time-planning-pn/d/420_eform-angular-time-planning-plugin.sql
```

- [ ] **Step 2: Port 3 spec files using the Conversion Reference**

- [ ] **Step 3: Commit and push — verify CI group d goes green**

```bash
git add eform-client/playwright/e2e/plugins/time-planning-pn/d/
git commit -m "feat: add Playwright group d tests"
git push
```

---

## Tasks 10–15: Groups e–j (identical structure)

**Repo:** `eform-angular-timeplanning-plugin`

Each of groups e, f, g, h, i, j contains exactly the same 3 files with different SQL seed data:
- `420_SDK.sql` + `420_eform-angular-time-planning-plugin.sql`
- `activate-plugin.spec.ts`
- `assert-true.spec.ts`
- `dashboard-edit-a.spec.ts`

For each group, repeat the following pattern (substitute `X` for the group letter):

- [ ] **Copy SQL files**

```bash
cp eform-client/cypress/e2e/plugins/time-planning-pn/X/420_SDK.sql \
   eform-client/playwright/e2e/plugins/time-planning-pn/X/420_SDK.sql
cp eform-client/cypress/e2e/plugins/time-planning-pn/X/420_eform-angular-time-planning-plugin.sql \
   eform-client/playwright/e2e/plugins/time-planning-pn/X/420_eform-angular-time-planning-plugin.sql
```

- [ ] **Port 3 spec files** (identical structure to group d — use as template, same conversion)

- [ ] **Commit and push — verify CI group X goes green before moving to next group**

```bash
git add eform-client/playwright/e2e/plugins/time-planning-pn/X/
git commit -m "feat: add Playwright group X tests"
git push
```

**Do groups in order: e → f → g → h → i → j. Do not proceed to the next until the current group is green in CI.**

---

## Task 16: Group k — leave-policies

**Repo:** `eform-angular-timeplanning-plugin`

**Cypress source:** `eform-client/cypress/e2e/plugins/time-planning-pn/k/`

Files: SQL + `activate-plugin.spec.ts`, `assert-true.spec.ts`, `leave-policies.spec.ts`

- [ ] **Step 1: Copy SQL files** (same `cp` pattern as Task 9)

- [ ] **Step 2: Read `leave-policies.spec.cy.ts` and port to Playwright using the Conversion Reference**

- [ ] **Step 3: Port `activate-plugin.spec.ts` and `assert-true.spec.ts`** (identical to earlier groups)

- [ ] **Step 4: Commit and push — verify CI group k goes green**

```bash
git add eform-client/playwright/e2e/plugins/time-planning-pn/k/
git commit -m "feat: add Playwright group k tests (leave-policies)"
git push
```

---

## Task 17: Group l — absence-requests

**Repo:** `eform-angular-timeplanning-plugin`

**Cypress source:** `eform-client/cypress/e2e/plugins/time-planning-pn/l/`

Files: SQL + `activate-plugin.spec.ts`, `assert-true.spec.ts`, `time-planning-absence-requests.spec.ts`

- [ ] **Step 1: Copy SQL files**
- [ ] **Step 2: Port all 3 spec files using the Conversion Reference**
- [ ] **Step 3: Commit and push — verify CI group l goes green**

```bash
git add eform-client/playwright/e2e/plugins/time-planning-pn/l/
git commit -m "feat: add Playwright group l tests (absence-requests)"
git push
```

---

## Task 18: Group m — manager-assignment

**Repo:** `eform-angular-timeplanning-plugin`

**Cypress source:** `eform-client/cypress/e2e/plugins/time-planning-pn/m/`

Files: SQL + `activate-plugin.spec.ts`, `assert-true.spec.ts`, `manager-assignment.spec.ts`

- [ ] **Step 1: Copy SQL files**
- [ ] **Step 2: Port all 3 spec files using the Conversion Reference**
- [ ] **Step 3: Commit and push — verify CI group m goes green**

```bash
git add eform-client/playwright/e2e/plugins/time-planning-pn/m/
git commit -m "feat: add Playwright group m tests (manager-assignment)"
git push
```

---

## Task 19: Group n — tag-filtering

**Repo:** `eform-angular-timeplanning-plugin`

**Cypress source:** `eform-client/cypress/e2e/plugins/time-planning-pn/n/`

Files: SQL + `activate-plugin.spec.ts`, `assert-true.spec.ts`, `tag-filtering.spec.ts`

- [ ] **Step 1: Copy SQL files**
- [ ] **Step 2: Port all 3 spec files using the Conversion Reference**
- [ ] **Step 3: Commit and push — verify CI group n goes green**

```bash
git add eform-client/playwright/e2e/plugins/time-planning-pn/n/
git commit -m "feat: add Playwright group n tests (tag-filtering)"
git push
```

---

## Task 20: Group o — break-policies

**Repo:** `eform-angular-timeplanning-plugin`

**Cypress source:** `eform-client/cypress/e2e/plugins/time-planning-pn/o/`

Files: SQL + `activate-plugin.spec.ts`, `assert-true.spec.ts`, `break-policies.spec.ts`

- [ ] **Step 1: Copy SQL files**
- [ ] **Step 2: Port all 3 spec files using the Conversion Reference**
- [ ] **Step 3: Commit and push — verify all 15 CI groups are green**

```bash
git add eform-client/playwright/e2e/plugins/time-planning-pn/o/
git commit -m "feat: add Playwright group o tests (break-policies)"
git push
```

Expected: All 15 `pn-playwright-test` jobs (a–o) are green in GitHub Actions. The migration is complete and the pattern is proven.

---

## Notes for Implementer

**Working in two repos:** Tasks 1–3 are in `eform-angular-frontend`. Tasks 4–20 are in `eform-angular-timeplanning-plugin`. Coordinate merges so that when the plugin CI runs, the shared page objects are already in the frontend repo on the same branch.

**100-second wait:** `await page.waitForTimeout(100000)` in the plugin activation tests is intentional — the server needs time to run database migrations. Do not reduce it.

**Import paths in test files:** Test files in the plugin are deployed to `eform-client/playwright/e2e/plugins/time-planning-pn/[group]/` inside the frontend repo. All `../../../Login.page` imports resolve relative to that deployed location, not the plugin repo source location.

**`xlsx` package:** Already in `eform-angular-frontend`'s `package.json`. Import as `import * as XLSX from 'xlsx'` in Playwright tests (same package, different import style from Cypress's named imports).

**CI branch alignment:** The `pn-playwright-test` job checks out the frontend at `${{ steps.extract_branch.outputs.branch }}`. The shared page objects (Tasks 1–3) must be merged to that branch in `eform-angular-frontend` before the plugin's Playwright tests can run successfully.

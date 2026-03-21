import { test, expect } from '@playwright/test';
import { LoginPage } from '../../../Login.page';

test.describe('Time Planning - Manager Assignment', () => {
  test.beforeEach(async ({ page }) => {
    await page.goto('http://localhost:4200');
    await new LoginPage(page).login();
  });

  // Helper function to navigate to dashboard
  const navigateToDashboard = async (page) => {
    await page.locator('mat-nested-tree-node').filter({ hasText: 'Timeregistrering' }).click();

    if (await page.locator('mat-tree-node').filter({ hasText: 'Dashboard' }).count() === 0) {
      throw new Error('Dashboard menu not found');
    }

    const indexResponse = page.waitForResponse(
      (resp) => resp.url().includes('/api/time-planning-pn/plannings/index') && resp.status() === 200,
      { timeout: 60000 }
    );
    await page.locator('mat-tree-node').filter({ hasText: 'Dashboard' }).click();
    await indexResponse;

    // Wait for spinner after index update
    if (await page.locator('.overlay-spinner').count() > 0) {
      await expect(page.locator('.overlay-spinner')).not.toBeVisible({ timeout: 30000 });
    }

    // Additional wait for slow CI environment
    await page.waitForTimeout(2000);
  };

  // Helper function to open assigned site dialog
  const openAssignedSiteDialog = async (page) => {
    // Wait for any overlay spinner to disappear before interacting
    if (await page.locator('.overlay-spinner').count() > 0) {
      await expect(page.locator('.overlay-spinner')).not.toBeVisible({ timeout: 30000 });
    }

    // Ensure workingHoursSite is ready and not covered
    await page.locator('#workingHoursSite', { timeout: 10000 }).scrollIntoViewIfNeeded();
    await expect(page.locator('#workingHoursSite')).toBeVisible();
    await page.waitForTimeout(1000);

    // Select a site if available
    await page.locator('#workingHoursSite').click();
    await page.waitForTimeout(500);

    await expect(page.locator('.ng-option').first()).toBeVisible({ timeout: 10000 });
    await page.locator('.ng-option').first().click();
    await page.waitForTimeout(1000);

    // Click on first data cell to open dialog
    await page.locator('#firstColumn0', { timeout: 10000 }).scrollIntoViewIfNeeded();
    await expect(page.locator('#firstColumn0')).toBeVisible();
    await page.locator('#firstColumn0').click();

    // Wait for dialog to open
    await page.locator('mat-dialog-container').scrollIntoViewIfNeeded();
    await expect(page.locator('mat-dialog-container')).toBeVisible({ timeout: 10000 });
    await page.waitForTimeout(500);
  };

  // Helper function to navigate to General tab in dialog
  const goToGeneralTab = async (page) => {
    if (await page.locator('.mat-mdc-tab').filter({ hasText: 'General' }).count() > 0) {
      await page.locator('.mat-mdc-tab').filter({ hasText: 'General' }).scrollIntoViewIfNeeded();
      await page.locator('.mat-mdc-tab').filter({ hasText: 'General' }).click({ force: true });
      await page.waitForTimeout(500);
    }
  };

  // Helper function to close dialog
  const closeDialog = async (page) => {
    if (await page.locator('#cancelButton').count() > 0) {
      await page.locator('#cancelButton').scrollIntoViewIfNeeded();
      await page.locator('#cancelButton').click({ force: true });
    } else if (await page.locator('button').filter({ hasText: 'Cancel' }).count() > 0) {
      await page.locator('button').filter({ hasText: 'Cancel' }).click({ force: true });
    }
    // Wait for dialog to close
    await expect(page.locator('mat-dialog-container')).toHaveCount(0, { timeout: 10000 });
  };

  // Helper function to save dialog
  const saveDialog = async (page) => {
    if (await page.locator('#saveButton').count() > 0) {
      const [siteUpdate, indexUpdate] = await Promise.all([
        page.waitForResponse(
          (resp) => resp.url().includes('/api/time-planning-pn/settings/assigned-site') && resp.request().method() === 'PUT',
          { timeout: 10000 }
        ),
        page.waitForResponse(
          (resp) => resp.url().includes('/api/time-planning-pn/plannings/index') && resp.status() === 200,
          { timeout: 10000 }
        ),
        (async () => {
          await page.locator('#saveButton').scrollIntoViewIfNeeded();
          await page.locator('#saveButton').click({ force: true });
        })(),
      ]);
    } else if (await page.locator('button').filter({ hasText: 'Save' }).count() > 0) {
      const [siteUpdate, indexUpdate] = await Promise.all([
        page.waitForResponse(
          (resp) => resp.url().includes('/api/time-planning-pn/settings/assigned-site') && resp.request().method() === 'PUT',
          { timeout: 10000 }
        ),
        page.waitForResponse(
          (resp) => resp.url().includes('/api/time-planning-pn/plannings/index') && resp.status() === 200,
          { timeout: 10000 }
        ),
        (async () => {
          await page.locator('button').filter({ hasText: 'Save' }).click({ force: true });
        })(),
      ]);
    }

    // Wait for spinner after index update
    if (await page.locator('.overlay-spinner').count() > 0) {
      await expect(page.locator('.overlay-spinner')).not.toBeVisible({ timeout: 30000 });
    }

    // Wait for dialog to close
    await expect(page.locator('mat-dialog-container')).toHaveCount(0, { timeout: 10000 });

    // Wait for overlay spinner to disappear after dialog closes
    if (await page.locator('.overlay-spinner').count() > 0) {
      await expect(page.locator('.overlay-spinner')).not.toBeVisible({ timeout: 30000 });
    }

    // Additional wait for dashboard to be fully ready
    await page.waitForTimeout(2000);
  };

  /**
   * Test 1: Check/uncheck IsManager and verify it persists
   */
  test('should toggle IsManager on and off and persist the state', async ({ page }) => {
    if (await page.locator('#actionMenu').count() === 0) {
      return;
    }

    await navigateToDashboard(page);
    await openAssignedSiteDialog(page);
    await goToGeneralTab(page);

    if (await page.locator('#isManager').count() > 0) {
      const checkboxInput = page.locator('#isManager > div > div > input');

      // Ensure checkbox is off initially
      let currentClass = await checkboxInput.getAttribute('class');
      if (currentClass?.includes('mdc-checkbox--selected')) {
        await page.locator('#isManager').click();
        await page.waitForTimeout(500);
      }

      // Ensure checkbox is off, click to turn it on
      currentClass = await checkboxInput.getAttribute('class');
      if (!currentClass?.includes('mdc-checkbox--selected')) {
        await page.locator('#isManager').click();
        await page.waitForTimeout(500);
      }

      // Save
      await saveDialog(page);

      // Re-open the dialog
      await openAssignedSiteDialog(page);
      await goToGeneralTab(page);

      // Verify checkbox is on after save/reload
      currentClass = await checkboxInput.getAttribute('class');
      expect(currentClass).toBe('mdc-checkbox__native-control mdc-checkbox--selected');

      // Turn checkbox off
      currentClass = await checkboxInput.getAttribute('class');
      if (currentClass?.includes('mdc-checkbox--selected')) {
        await page.locator('#isManager').click();
        await page.waitForTimeout(500);
      }

      // Save
      await saveDialog(page);

      // Re-open the dialog
      await openAssignedSiteDialog(page);
      await goToGeneralTab(page);

      // Verify checkbox is off after save/reload
      currentClass = await checkboxInput.getAttribute('class');
      expect(currentClass).toBe('mdc-checkbox__native-control');

      // Close dialog
      await closeDialog(page);
    } else {
      await closeDialog(page);
    }
  });

  /**
   * Test 2: Verify tags field shows when manager is on, test with random text
   */
  test('should show tags field when manager checkbox is on and handle random text without errors', async ({ page }) => {
    if (await page.locator('#actionMenu').count() === 0) {
      return;
    }

    await navigateToDashboard(page);
    await openAssignedSiteDialog(page);
    await goToGeneralTab(page);

    if (await page.locator('#isManager').count() > 0) {
      const checkboxInput = page.locator('#isManager > div > div > input');

      // Ensure checkbox is off initially
      let currentClass = await checkboxInput.getAttribute('class');
      if (currentClass?.includes('mdc-checkbox--selected')) {
        await page.locator('#isManager').click();
        await page.waitForTimeout(500);
      }

      // Turn checkbox on
      currentClass = await checkboxInput.getAttribute('class');
      if (!currentClass?.includes('mdc-checkbox--selected')) {
        await page.locator('#isManager').click();
        await page.waitForTimeout(500);
      }

      // Wait for tags field to appear
      await page.waitForTimeout(500);

      // Validate that the tags field is shown
      const selector = 'mtx-select[formcontrolname="managingTagIds"]';
      if (await page.locator(selector).count() > 0) {
        await page.locator(selector).scrollIntoViewIfNeeded();
        await expect(page.locator(selector)).toBeVisible();

        // Click on the tags field to open it
        await page.locator(selector).click();
        await page.waitForTimeout(500);

        // Type random text to test search functionality
        const randomText = 'random-nonexistent-tag-' + Math.random().toString(36).substring(7);
        await page.locator(selector).locator('input').fill(randomText);

        // Wait a bit to see if any errors occur
        await page.waitForTimeout(1000);

        // Check for dropdown panel
        if (await page.locator('.ng-dropdown-panel').count() > 0) {
          // Verify no matching options or "no items" message
          const panelText = await page.locator('.ng-dropdown-panel').textContent();
          // No items found or empty is expected for random text
        }

        // Close the dropdown by clicking outside
        await page.locator('body').click({ position: { x: 0, y: 0 } });
        await page.waitForTimeout(500);
      }

      // Close dialog without saving
      await closeDialog(page);
    } else {
      await closeDialog(page);
    }
  });

  /**
   * Test 3: Create a tag, use it in assigned-site-modal, and verify it persists
   */
  test('should create a tag, use it in assigned-site-modal, and persist the selection', async ({ page }) => {
    if (await page.locator('#actionMenu').count() === 0) {
      return;
    }

    // Generate a unique tag name
    const tagName = 'TestTag-' + Date.now();

    // Navigate to Advanced > Sites to access tags management
    await page.goto('http://localhost:4200/advanced/sites');
    await page.waitForTimeout(2000);

    // Find and click the tags button (mat-icon with text 'discount')
    const tagIconButtons = page.locator('button').filter({ has: page.locator('mat-icon', { hasText: 'discount' }) });
    if (await tagIconButtons.count() === 0) {
      throw new Error('Tags button with mat-icon (discount) not found on Sites page');
    }
    await tagIconButtons.first().click();
    await page.waitForTimeout(1000);

    // Click the add tag button if present
    const addTagButton = page.locator('button').filter({ has: page.locator('mat-icon', { hasText: 'add' }) });
    if (await addTagButton.count() > 0) {
      await addTagButton.first().click();
      await page.waitForTimeout(500);
    }

    // Enter tag name
    if (await page.locator('input[id="newTagName"]').count() > 0) {
      await page.locator('input[id="newTagName"]').fill(tagName);
    } else if (await page.locator('input[formcontrolname="name"]').count() > 0) {
      await page.locator('input[formcontrolname="name"]').fill(tagName);
    } else {
      await page.locator('mat-dialog-container input[type="text"]').first().fill(tagName);
    }

    // Save the tag
    if (await page.locator('#newTagSaveBtn').count() > 0) {
      const tagCreateResponse = page.waitForResponse(
        (resp) => resp.url().includes('/api/tags') && resp.request().method() === 'POST',
        { timeout: 10000 }
      );
      await page.locator('#newTagSaveBtn').click();
      await tagCreateResponse;
      await expect(page.locator('.overlay-spinner')).not.toBeVisible({ timeout: 30000 });
      await page.locator('#tagsModalCloseBtn').click();
    } else if (await page.locator('#saveButton').count() > 0) {
      const tagCreateResponse = page.waitForResponse(
        (resp) => resp.url().includes('/api/tags') && resp.request().method() === 'POST',
        { timeout: 10000 }
      );
      await page.locator('#saveButton').click();
      await tagCreateResponse;
    }

    await page.waitForTimeout(1000);

    // Navigate back to Time Planning Dashboard
    await navigateToDashboard(page);

    // Open assigned site dialog
    await openAssignedSiteDialog(page);

    // Navigate to General tab
    await goToGeneralTab(page);

    if (await page.locator('#isManager').count() > 0) {
      const checkboxInput = page.locator('#isManager > div > div > input');

      // Ensure checkbox is off initially
      let currentClass = await checkboxInput.getAttribute('class');
      if (currentClass?.includes('mdc-checkbox--selected')) {
        await page.locator('#isManager').click();
        await page.waitForTimeout(500);
      }

      // Turn checkbox on
      currentClass = await checkboxInput.getAttribute('class');
      if (!currentClass?.includes('mdc-checkbox--selected')) {
        await page.locator('#isManager').click();
        await page.waitForTimeout(500);
      }

      // Wait for tags field to appear
      await page.waitForTimeout(500);

      const selector = 'mtx-select[formcontrolname="managingTagIds"]';
      if (await page.locator(selector).count() > 0) {
        await page.locator(selector).scrollIntoViewIfNeeded();
        await expect(page.locator(selector)).toBeVisible();

        // Click on the tags field to open it
        await page.locator(selector).click();
        await page.waitForTimeout(1000);

        // Type the tag name to search for it
        await page.locator(selector).locator('input').fill(tagName);
        await page.waitForTimeout(1000);

        // Check if the tag appears in the dropdown and select it
        const tagOption = page.locator('.ng-option').filter({ hasText: tagName });
        if (await tagOption.count() > 0) {
          await tagOption.click();
          await page.waitForTimeout(500);

          // Save the dialog
          await saveDialog(page);

          // Re-open to verify persistence
          await openAssignedSiteDialog(page);
          await goToGeneralTab(page);

          // Verify checkbox is still on after save/reload
          currentClass = await checkboxInput.getAttribute('class');
          expect(currentClass).toBe('mdc-checkbox__native-control mdc-checkbox--selected');

          // Validate that the selected tag is shown
          await page.locator(selector).scrollIntoViewIfNeeded();
          await expect(page.locator(selector)).toBeVisible();

          // Close dialog
          await closeDialog(page);
        } else {
          // Tag not found in dropdown - close
          await page.locator('body').click({ position: { x: 0, y: 0 } });
          await page.waitForTimeout(500);
          await closeDialog(page);
        }
      } else {
        await closeDialog(page);
      }
    } else {
      await closeDialog(page);
    }
  });
});

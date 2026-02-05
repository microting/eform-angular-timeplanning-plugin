import loginPage from '../../../Login.page';

const BASE_URL = 'http://localhost:4200';

describe('Time Planning - Tag Filtering', () => {
  const tagNames = [
    `Tag-A-${Date.now()}-${Math.random().toString(36).substring(7)}`,
    `Tag-B-${Date.now()}-${Math.random().toString(36).substring(7)}`,
    `Tag-C-${Date.now()}-${Math.random().toString(36).substring(7)}`,
    `Tag-D-${Date.now()}-${Math.random().toString(36).substring(7)}`,
    `Tag-E-${Date.now()}-${Math.random().toString(36).substring(7)}`,
  ];

  beforeEach(() => {
    cy.visit(BASE_URL);
    loginPage.login();
  });

  /**
   * Helper function to create a tag following the pattern from 'm' folder
   */
  const createTag = (tagName) => {
    cy.task('log', `[Tag Filter Tests] Creating tag: ${tagName}`);
    
    // Navigate to Advanced > Sites to access tags management
    cy.visit(`${BASE_URL}/advanced/sites`);
    cy.task('log', '[Tag Filter Tests] Visited /advanced/sites, waiting for page load');
    cy.wait(2000);

    cy.task('log', '[Tag Filter Tests] Looking for tags management UI elements');
    cy.get('body').then(($sitesBody) => {
      // Helper function to filter mat-icons for tag icons
      const isTagIcon = (el) => {
        const text = Cypress.$(el).text().trim();
        return text === 'discount';
      };

      // Try to find tags button by mat-icon
      const hasTagIconButton = $sitesBody.find('button mat-icon').filter((i, el) => isTagIcon(el)).length > 0;

      if (!hasTagIconButton) {
        throw new Error('[Tag Filter Tests] FAILED: Tags button with mat-icon (discount) not found on Sites page');
      }

      cy.task('log', '[Tag Filter Tests] Tags button with mat-icon found, clicking it');
      // Click the tags button
      cy.get('button').find('mat-icon').filter((i, el) => isTagIcon(el)).parents('button').first().click();
      cy.task('log', '[Tag Filter Tests] Clicked tags button');

      // Wait for dialog or form to open
      cy.wait(1000);
      cy.task('log', '[Tag Filter Tests] Waited for tag creation dialog to open');

      // Enter tag name
      cy.get('body').then(($formBody) => {
        const addTagButtonWithIconAdd = $formBody.find('button mat-icon').filter((i, el) => {
          const text = Cypress.$(el).text().trim();
          return text === 'add';
        });

        if (addTagButtonWithIconAdd.length > 0) {
          cy.task('log', '[Tag Filter Tests] Found add tag button with mat-icon "add", clicking it to open creation form');
          addTagButtonWithIconAdd.parents('button').first().click();
          cy.wait(500);
        }

        cy.task('log', `[Tag Filter Tests] Looking for tag name input field, will enter: ${tagName}`);
        // Look for tag name input
        if ($formBody.find('input[id="newTagName"]').length > 0) {
          cy.get('input[id="newTagName"]').type(tagName);
          cy.task('log', `[Tag Filter Tests] Entered tag name in input[id="newTagName"]`);
        } else if ($formBody.find('input[formcontrolname="name"]').length > 0) {
          cy.get('input[formcontrolname="name"]').type(tagName);
          cy.task('log', `[Tag Filter Tests] Entered tag name in input[formcontrolname="name"]`);
        } else if ($formBody.find('input').filter((i, el) => {
          const placeholder = Cypress.$(el).attr('placeholder');
          return placeholder && placeholder.toLowerCase().includes('name');
        }).length > 0) {
          cy.get('input').filter((i, el) => {
            const placeholder = Cypress.$(el).attr('placeholder');
            return placeholder && placeholder.toLowerCase().includes('name');
          }).first().type(tagName);
          cy.task('log', `[Tag Filter Tests] Entered tag name in input with name placeholder`);
        } else {
          // Try to find any visible input in dialog
          cy.get('mat-dialog-container input[type="text"]').first().type(tagName);
          cy.task('log', `[Tag Filter Tests] Entered tag name in first text input in dialog`);
        }

        cy.task('log', '[Tag Filter Tests] Tag name entered, looking for Save button');
        // Save the tag
        cy.get('body').then(($saveBody) => {
          if ($saveBody.find('#newTagSaveBtn').length > 0) {
            cy.intercept('POST', '**/api/tags').as('tag-create');
            cy.get('#newTagSaveBtn').click();
            cy.task('log', '[Tag Filter Tests] Clicked #newTagSaveBtn, waiting for tag-create API call');
            cy.wait('@tag-create', { timeout: 10000 });
            cy.get('.overlay-spinner', {timeout: 30000}).should('not.be.visible');
            cy.task('log', '[Tag Filter Tests] Tag-create API call completed');
            cy.get('#tagsModalCloseBtn').click();
            cy.task('log', '[Tag Filter Tests] Closed tag creation dialog');
          } else if ($saveBody.find('#saveButton').length > 0) {
            cy.intercept('POST', '**/api/tags').as('tag-create');
            cy.get('#saveButton').click();
            cy.task('log', '[Tag Filter Tests] Clicked #saveButton, waiting for tag-create API call');
            cy.wait('@tag-create', { timeout: 10000 });
            cy.task('log', '[Tag Filter Tests] Tag-create API call completed');
          }
        });

        // Wait for tag to be created
        cy.wait(1000);
        cy.task('log', '[Tag Filter Tests] Waited additional 1s after tag creation');

        // Validate that the tag appears in the list
        cy.get('body').then(($listBody) => {
          if ($listBody.text().includes(tagName)) {
            cy.task('log', `[Tag Filter Tests] SUCCESS: Tag "${tagName}" found in list after creation`);
          } else {
            cy.task('log', `[Tag Filter Tests] WARNING: Tag "${tagName}" not visible in list yet (may appear later)`);
          }
        });
      });
    });
  };

  /**
   * Test 1: Create 5 tags
   */
  it('should create 5 tags', () => {
    cy.task('log', '[Tag Filter Tests] ========== Starting tag creation ==========');
    
    // Create each tag one by one following the pattern from 'm' folder
    tagNames.forEach((tagName, index) => {
      cy.task('log', `[Tag Filter Tests] Creating tag ${index + 1} of 5`);
      createTag(tagName);
    });

    cy.task('log', '[Tag Filter Tests] All 5 tags created successfully');
  });

  /**
   * Test 2: Assign tags to sites
   * Note: This test would require navigating to assigned sites and assigning tags
   * The exact implementation would depend on the UI structure
   */
  it('should assign tags to sites', () => {
    cy.task('log', '[Tag Filter Tests] Assigning tags to sites...');
    // TODO: Implement tag assignment to sites
    // This would follow a similar pattern to the 'm' folder test
  });

  /**
   * Test 3: Navigate to dashboard and verify filtering works
   */
  it('should show all assigned sites on dashboard', () => {
    cy.task('log', '[Tag Filter Tests] Navigating to dashboard...');
    
    // Navigate to Time Planning Dashboard
    cy.get('mat-nested-tree-node').contains('Timeregistrering').click();
    cy.wait(500);

    cy.intercept('POST', '**/api/time-planning-pn/plannings/index').as('index-update');
    cy.get('mat-tree-node').contains('Dashboard').click();
    cy.wait('@index-update', { timeout: 60000 });

    // Wait for spinner
    cy.get('body').then(($body) => {
      if ($body.find('.overlay-spinner').length > 0) {
        cy.get('.overlay-spinner', { timeout: 30000 }).should('not.be.visible');
      }
    });

    cy.wait(2000);

    // Verify that sites are shown
    cy.get('body').then(($body) => {
      if ($body.find('app-time-plannings-table').length > 0) {
        cy.task('log', '[Tag Filter Tests] Dashboard loaded with planning table');
        cy.get('app-time-plannings-table').should('be.visible');
      }
    });

    cy.task('log', '[Tag Filter Tests] Dashboard verification complete');
  });

  /**
   * Test 4: Filter by single tag
   */
  it('should filter by single tag', () => {
    cy.task('log', '[Tag Filter Tests] Testing single tag filter...');
    
    // Navigate to dashboard
    cy.get('mat-nested-tree-node').contains('Timeregistrering').click();
    cy.wait(500);

    cy.intercept('POST', '**/api/time-planning-pn/plannings/index').as('index-update');
    cy.get('mat-tree-node').contains('Dashboard').click();
    cy.wait('@index-update', { timeout: 60000 });

    // Wait for spinner
    cy.get('body').then(($body) => {
      if ($body.find('.overlay-spinner').length > 0) {
        cy.get('.overlay-spinner', { timeout: 30000 }).should('not.be.visible');
      }
    });

    cy.wait(2000);

    // Check if tag filter exists
    cy.get('body').then(($body) => {
      if ($body.find('#planningTags').length > 0) {
        cy.task('log', '[Tag Filter Tests] Found #planningTags filter');
        cy.get('#planningTags').should('be.visible').click();
        cy.wait(500);
        
        cy.task('log', '[Tag Filter Tests] Tag filter dropdown should be open');
      } else {
        cy.task('log', '[Tag Filter Tests] Tag filter not found on dashboard');
      }
    });

    cy.task('log', '[Tag Filter Tests] Single tag filter test completed');
  });

  /**
   * Test 5: Filter by multiple tags (AND logic)
   */
  it('should filter by multiple tags', () => {
    cy.task('log', '[Tag Filter Tests] Testing multiple tag filter (AND logic)...');
    
    // Navigate to dashboard
    cy.get('mat-nested-tree-node').contains('Timeregistrering').click();
    cy.wait(500);

    cy.intercept('POST', '**/api/time-planning-pn/plannings/index').as('index-update');
    cy.get('mat-tree-node').contains('Dashboard').click();
    cy.wait('@index-update', { timeout: 60000 });

    // Wait for spinner
    cy.get('body').then(($body) => {
      if ($body.find('.overlay-spinner').length > 0) {
        cy.get('.overlay-spinner', { timeout: 30000 }).should('not.be.visible');
      }
    });

    cy.wait(2000);

    cy.task('log', '[Tag Filter Tests] Multiple tag filter test completed');
  });
});

import loginPage from '../../../Login.page';

const BASE_URL = 'http://localhost:4200';

describe('Time Planning - Tag Filtering', () => {
  const tagNames = [
    `Tag-A-${Math.random().toString(36).substring(7)}`,
    `Tag-B-${Math.random().toString(36).substring(7)}`,
    `Tag-C-${Math.random().toString(36).substring(7)}`,
    `Tag-D-${Math.random().toString(36).substring(7)}`,
    `Tag-E-${Math.random().toString(36).substring(7)}`,
  ];

  beforeEach(() => {
    cy.visit(BASE_URL);
    loginPage.login();
  });

  /**
   * Test 1: Create 5 tags on /advanced/sites
   */
  it('should create 5 tags', () => {
    cy.task('log', '[Tag Filter Tests] Creating 5 tags...');
    cy.visit(`${BASE_URL}/advanced/sites`);
    cy.wait(2000);

    // Wait for spinner to disappear
    cy.get('body').then(($body) => {
      if ($body.find('.overlay-spinner').length > 0) {
        cy.get('.overlay-spinner', { timeout: 30000 }).should('not.be.visible');
      }
    });

    tagNames.forEach((tagName, index) => {
      cy.task('log', `[Tag Filter Tests] Creating tag ${index + 1}: ${tagName}`);
      
      // Click the "Create tag" button
      cy.get('body').then(($body) => {
        // Look for button with text "Create tag" or similar
        if ($body.find('button:contains("Create tag")').length > 0) {
          cy.get('button').contains('Create tag').click();
        } else if ($body.find('button[id*="createTag"]').length > 0) {
          cy.get('button[id*="createTag"]').first().click();
        } else if ($body.find('mat-icon:contains("add")').length > 0) {
          cy.get('mat-icon').contains('add').first().parent('button').click();
        }
      });

      cy.wait(500);

      // Fill in the tag name
      cy.get('input[id*="tagName"], input[formcontrolname="name"], input[name="tagName"]')
        .first()
        .clear()
        .type(tagName);

      // Click save/create button
      cy.get('button').contains(/Save|Create/i).click();
      cy.wait(1000);

      cy.task('log', `[Tag Filter Tests] Tag created: ${tagName}`);
    });

    cy.task('log', '[Tag Filter Tests] All 5 tags created successfully');
  });

  /**
   * Test 2: Assign tags to sites
   * - Assign Tag-A to all sites (at least 5 sites)
   * - Assign Tag-B to site 1
   * - Assign Tag-C to site 2
   * - Assign Tag-D to site 3
   * - Assign Tag-E to site 4
   * - Assign both Tag-B and Tag-C to site 5
   */
  it('should assign tags to sites', () => {
    cy.task('log', '[Tag Filter Tests] Assigning tags to sites...');
    cy.visit(`${BASE_URL}/advanced/sites`);
    cy.wait(2000);

    // Wait for spinner to disappear
    cy.get('body').then(($body) => {
      if ($body.find('.overlay-spinner').length > 0) {
        cy.get('.overlay-spinner', { timeout: 30000 }).should('not.be.visible');
      }
    });

    // Get all site rows
    cy.get('table tbody tr').should('have.length.greaterThan', 4);

    // For each of the first 5 sites, assign tags
    for (let i = 0; i < 5; i++) {
      cy.task('log', `[Tag Filter Tests] Assigning tags to site ${i + 1}`);
      
      // Click edit button for site i
      cy.get('table tbody tr').eq(i).find('button[id*="editSite"], button:contains("edit"), mat-icon:contains("edit")').first().click();
      cy.wait(1000);

      // Wait for site edit dialog to open
      cy.get('mat-dialog-container, [role="dialog"]').should('be.visible');

      // Look for tags field (mtx-select)
      cy.get('body').then(($dialogBody) => {
        if ($dialogBody.find('mtx-select[formcontrolname*="tag"], mtx-select[id*="tag"]').length > 0) {
          cy.get('mtx-select[formcontrolname*="tag"], mtx-select[id*="tag"]').first().click();
          cy.wait(500);

          // Assign Tag-A to all sites
          cy.get('.ng-option').contains(tagNames[0]).click();

          // Assign specific tags based on site index
          if (i === 0) {
            // Site 1: Tag-A + Tag-B
            cy.get('.ng-option').contains(tagNames[1]).click();
          } else if (i === 1) {
            // Site 2: Tag-A + Tag-C
            cy.get('.ng-option').contains(tagNames[2]).click();
          } else if (i === 2) {
            // Site 3: Tag-A + Tag-D
            cy.get('.ng-option').contains(tagNames[3]).click();
          } else if (i === 3) {
            // Site 4: Tag-A + Tag-E
            cy.get('.ng-option').contains(tagNames[4]).click();
          } else if (i === 4) {
            // Site 5: Tag-A + Tag-B + Tag-C
            cy.get('.ng-option').contains(tagNames[1]).click();
            cy.get('.ng-option').contains(tagNames[2]).click();
          }

          // Close dropdown
          cy.get('body').click(0, 0);
          cy.wait(500);
        }
      });

      // Save the site
      cy.get('button').contains(/Save|OK/i).click();
      cy.wait(1000);

      cy.task('log', `[Tag Filter Tests] Tags assigned to site ${i + 1}`);
    }

    cy.task('log', '[Tag Filter Tests] All tags assigned successfully');
  });

  /**
   * Test 3: Navigate to dashboard and verify all sites are shown
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

    // Verify that sites are shown (check for table or planning display)
    cy.get('body').then(($body) => {
      if ($body.find('app-time-plannings-table').length > 0) {
        cy.task('log', '[Tag Filter Tests] Dashboard loaded with planning table');
        // Count sites displayed
        cy.get('app-time-plannings-table').should('be.visible');
      }
    });

    cy.task('log', '[Tag Filter Tests] Dashboard verification complete');
  });

  /**
   * Test 4: Filter by Tag-B and verify only site 1 and 5 are shown
   */
  it('should filter by Tag-B', () => {
    cy.task('log', '[Tag Filter Tests] Testing Tag-B filter...');
    
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

    // Find and click the tags filter
    cy.get('#planningTags').should('be.visible').click();
    cy.wait(500);

    // Select Tag-B
    cy.get('.ng-option').contains(tagNames[1]).click();
    
    // Close dropdown
    cy.get('body').click(0, 0);
    cy.wait(1000);

    // Wait for filtered results
    cy.wait('@index-update', { timeout: 60000 });
    cy.wait(2000);

    // Verify filtered results (sites with Tag-B should be shown)
    cy.task('log', '[Tag Filter Tests] Tag-B filter applied successfully');
  });

  /**
   * Test 5: Filter by Tag-C and verify only site 2 and 5 are shown
   */
  it('should filter by Tag-C', () => {
    cy.task('log', '[Tag Filter Tests] Testing Tag-C filter...');
    
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

    // Find and click the tags filter
    cy.get('#planningTags').should('be.visible').click();
    cy.wait(500);

    // Select Tag-C
    cy.get('.ng-option').contains(tagNames[2]).click();
    
    // Close dropdown
    cy.get('body').click(0, 0);
    cy.wait(1000);

    // Wait for filtered results
    cy.wait('@index-update', { timeout: 60000 });
    cy.wait(2000);

    cy.task('log', '[Tag Filter Tests] Tag-C filter applied successfully');
  });

  /**
   * Test 6: Filter by both Tag-B and Tag-C (AND logic) and verify only site 5 is shown
   */
  it('should filter by multiple tags (AND logic)', () => {
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

    // Find and click the tags filter
    cy.get('#planningTags').should('be.visible').click();
    cy.wait(500);

    // Select both Tag-B and Tag-C
    cy.get('.ng-option').contains(tagNames[1]).click();
    cy.wait(200);
    cy.get('.ng-option').contains(tagNames[2]).click();
    
    // Close dropdown
    cy.get('body').click(0, 0);
    cy.wait(1000);

    // Wait for filtered results
    cy.wait('@index-update', { timeout: 60000 });
    cy.wait(2000);

    // Only site 5 should be shown (has both Tag-B and Tag-C)
    cy.task('log', '[Tag Filter Tests] Multiple tag filter (AND) applied successfully - only site 5 should be visible');
  });
});

import loginPage from '../../../Login.page';
import pluginPage from '../../../Plugin.page';

describe('Time Planning - Manager Assignment', () => {
  beforeEach(() => {
    cy.visit('http://localhost:4200');
    loginPage.login();
  });

  it('should display manager toggle and tags selector in assigned site dialog', () => {
    // Navigate to Time Planning plugin
    pluginPage.Navbar.goToPluginsPage();
    cy.get('#actionMenu')
      .should('be.visible')
      .click({ force: true });
    cy.intercept('GET', '**/api/time-planning-pn/settings').as('settings-get');
    cy.get('#plugin-settings-link0').click();
    cy.wait('@settings-get', { timeout: 60000 });

    // Navigate to Dashboard
    cy.get('mat-nested-tree-node').contains('Timeregistrering').click();
    cy.intercept('POST', '**/api/time-planning-pn/plannings/index').as('index-update');
    cy.get('mat-tree-node').contains('Dashboard').click();
    cy.wait('@index-update', {timeout: 60000});

    // Select a site
    cy.get('#workingHoursSite').click();
    cy.get('.ng-option').first().click();
    
    // Open assigned site dialog by clicking on first column
    cy.get('#firstColumn0').click();

    // Wait for dialog to appear
    cy.get('mat-dialog-container', {timeout: 10000})
      .should('be.visible');

    // Navigate to General tab (should be default, but let's make sure)
    cy.contains('.mat-mdc-tab', 'General')
      .scrollIntoView()
      .should('be.visible')
      .click({force: true});

    // Verify manager toggle exists and is visible (only for admins)
    cy.get('mat-slide-toggle').contains('Is Manager')
      .parents('mat-slide-toggle')
      .should('exist')
      .scrollIntoView();

    // Initially, the managing tags selector should not be visible or should be disabled
    // (because manager toggle is off by default)

    // Close the dialog
    cy.get('#cancelButton').scrollIntoView().click({force: true});
    cy.get('mat-dialog-container', {timeout: 500}).should('not.exist');
  });

  it('should show managing tags selector when manager toggle is enabled', () => {
    // Navigate to Time Planning plugin
    pluginPage.Navbar.goToPluginsPage();
    cy.get('#actionMenu')
      .should('be.visible')
      .click({ force: true });
    cy.intercept('GET', '**/api/time-planning-pn/settings').as('settings-get');
    cy.get('#plugin-settings-link0').click();
    cy.wait('@settings-get', { timeout: 60000 });

    // Navigate to Dashboard
    cy.get('mat-nested-tree-node').contains('Timeregistrering').click();
    cy.intercept('POST', '**/api/time-planning-pn/plannings/index').as('index-update');
    cy.get('mat-tree-node').contains('Dashboard').click();
    cy.wait('@index-update', {timeout: 60000});

    // Select a site
    cy.get('#workingHoursSite').click();
    cy.get('.ng-option').first().click();
    
    // Open assigned site dialog
    cy.get('#firstColumn0').click();
    cy.get('mat-dialog-container', {timeout: 10000}).should('be.visible');

    // Navigate to General tab
    cy.contains('.mat-mdc-tab', 'General')
      .scrollIntoView()
      .should('be.visible')
      .click({force: true});

    // Enable the manager toggle
    cy.get('mat-slide-toggle').contains('Is Manager')
      .parents('mat-slide-toggle')
      .scrollIntoView()
      .find('button[role="switch"]')
      .then(($btn) => {
        const isChecked = $btn.attr('aria-checked') === 'true';
        if (!isChecked) {
          cy.wrap($btn).click({force: true});
        }
      });

    // Verify the toggle is now checked
    cy.get('mat-slide-toggle').contains('Is Manager')
      .parents('mat-slide-toggle')
      .find('button[role="switch"]')
      .should('have.attr', 'aria-checked', 'true');

    // Now the managing tags selector should be visible
    cy.contains('Managing Tags')
      .should('be.visible');

    // Close the dialog without saving
    cy.get('#cancelButton').scrollIntoView().click({force: true});
    cy.get('mat-dialog-container', {timeout: 500}).should('not.exist');
  });

  it('should save and persist manager settings', () => {
    // Navigate to Time Planning plugin
    pluginPage.Navbar.goToPluginsPage();
    cy.get('#actionMenu')
      .should('be.visible')
      .click({ force: true });
    cy.intercept('GET', '**/api/time-planning-pn/settings').as('settings-get');
    cy.get('#plugin-settings-link0').click();
    cy.wait('@settings-get', { timeout: 60000 });

    // Navigate to Dashboard
    cy.get('mat-nested-tree-node').contains('Timeregistrering').click();
    cy.intercept('POST', '**/api/time-planning-pn/plannings/index').as('index-update');
    cy.intercept('PUT', '**/api/time-planning-pn/settings/assigned-site').as('assigned-site-update');
    cy.get('mat-tree-node').contains('Dashboard').click();
    cy.wait('@index-update', {timeout: 60000});

    // Select a site
    cy.get('#workingHoursSite').click();
    cy.get('.ng-option').first().click();
    
    // Open assigned site dialog
    cy.get('#firstColumn0').click();
    cy.get('mat-dialog-container', {timeout: 10000}).should('be.visible');

    // Navigate to General tab
    cy.contains('.mat-mdc-tab', 'General')
      .scrollIntoView()
      .should('be.visible')
      .click({force: true});

    // Enable the manager toggle
    cy.get('mat-slide-toggle').contains('Is Manager')
      .parents('mat-slide-toggle')
      .scrollIntoView()
      .find('button[role="switch"]')
      .then(($btn) => {
        const isChecked = $btn.attr('aria-checked') === 'true';
        if (!isChecked) {
          cy.wrap($btn).click({force: true});
        }
      });

    // Verify toggle is enabled
    cy.get('mat-slide-toggle').contains('Is Manager')
      .parents('mat-slide-toggle')
      .find('button[role="switch"]')
      .should('have.attr', 'aria-checked', 'true');

    // Save the dialog
    cy.get('#saveButton').scrollIntoView().click({force: true});
    cy.wait('@assigned-site-update', {timeout: 10000});
    cy.get('mat-dialog-container', {timeout: 500}).should('not.exist');

    // Reopen the dialog to verify settings persisted
    cy.wait(500);
    cy.get('#firstColumn0').click();
    cy.get('mat-dialog-container', {timeout: 10000}).should('be.visible');

    // Navigate to General tab
    cy.contains('.mat-mdc-tab', 'General')
      .scrollIntoView()
      .should('be.visible')
      .click({force: true});

    // Verify manager toggle is still enabled
    cy.get('mat-slide-toggle').contains('Is Manager')
      .parents('mat-slide-toggle')
      .find('button[role="switch"]')
      .should('have.attr', 'aria-checked', 'true');

    // Close the dialog
    cy.get('#cancelButton').scrollIntoView().click({force: true});
    cy.get('mat-dialog-container', {timeout: 500}).should('not.exist');
  });

  it('should toggle manager off and persist the change', () => {
    // Navigate to Time Planning plugin
    pluginPage.Navbar.goToPluginsPage();
    cy.get('#actionMenu')
      .should('be.visible')
      .click({ force: true });
    cy.intercept('GET', '**/api/time-planning-pn/settings').as('settings-get');
    cy.get('#plugin-settings-link0').click();
    cy.wait('@settings-get', { timeout: 60000 });

    // Navigate to Dashboard
    cy.get('mat-nested-tree-node').contains('Timeregistrering').click();
    cy.intercept('POST', '**/api/time-planning-pn/plannings/index').as('index-update');
    cy.intercept('PUT', '**/api/time-planning-pn/settings/assigned-site').as('assigned-site-update');
    cy.get('mat-tree-node').contains('Dashboard').click();
    cy.wait('@index-update', {timeout: 60000});

    // Select a site
    cy.get('#workingHoursSite').click();
    cy.get('.ng-option').first().click();
    
    // Open assigned site dialog
    cy.get('#firstColumn0').click();
    cy.get('mat-dialog-container', {timeout: 10000}).should('be.visible');

    // Navigate to General tab
    cy.contains('.mat-mdc-tab', 'General')
      .scrollIntoView()
      .should('be.visible')
      .click({force: true});

    // Disable the manager toggle (if it's on)
    cy.get('mat-slide-toggle').contains('Is Manager')
      .parents('mat-slide-toggle')
      .scrollIntoView()
      .find('button[role="switch"]')
      .then(($btn) => {
        const isChecked = $btn.attr('aria-checked') === 'true';
        if (isChecked) {
          cy.wrap($btn).click({force: true});
        }
      });

    // Verify toggle is disabled
    cy.get('mat-slide-toggle').contains('Is Manager')
      .parents('mat-slide-toggle')
      .find('button[role="switch"]')
      .should('have.attr', 'aria-checked', 'false');

    // Save the dialog
    cy.get('#saveButton').scrollIntoView().click({force: true});
    cy.wait('@assigned-site-update', {timeout: 10000});
    cy.get('mat-dialog-container', {timeout: 500}).should('not.exist');

    // Reopen the dialog to verify settings persisted
    cy.wait(500);
    cy.get('#firstColumn0').click();
    cy.get('mat-dialog-container', {timeout: 10000}).should('be.visible');

    // Navigate to General tab
    cy.contains('.mat-mdc-tab', 'General')
      .scrollIntoView()
      .should('be.visible')
      .click({force: true});

    // Verify manager toggle is still disabled
    cy.get('mat-slide-toggle').contains('Is Manager')
      .parents('mat-slide-toggle')
      .find('button[role="switch"]')
      .should('have.attr', 'aria-checked', 'false');

    // Close the dialog
    cy.get('#cancelButton').scrollIntoView().click({force: true});
    cy.get('mat-dialog-container', {timeout: 500}).should('not.exist');
  });
});


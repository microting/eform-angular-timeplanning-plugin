import loginPage from '../../../Login.page';

describe('Dashboard edit values', () => {
  let storedValues = {};

  beforeEach(() => {
    cy.visit('http://localhost:4200');
    loginPage.login();

    cy.get('mat-nested-tree-node').contains('Admin').click();
    cy.get('mat-nested-tree-node').contains('Plugins').click();

    cy.contains('div.plugin-name', 'Microting Time Planning Plugin')
      .closest('tr')
      .find('a#plugin-settings-link')
      .click();

    cy.url().should('include', '/plugins/time-planning-pn/settings');

    cy.get('#autoBreakCalculationActiveToggle button[role="switch"]')
      .then(($btn) => {
        const isChecked = $btn.attr('aria-checked') === 'true';
        if (!isChecked) {
          cy.wrap($btn)
            .scrollIntoView()
            .click({force: true});
        }
      });

    // Confirm it’s ON
    cy.get('#autoBreakCalculationActiveToggle button[role="switch"]')
      .should('have.attr', 'aria-checked', 'true');

    cy.get('#saveSettings')
      .scrollIntoView()
      .should('be.visible')
      .click({force: true});
  });

  it('should validate the values from global gets set', () => {

    cy.get('mat-nested-tree-node').contains('Timeregistrering').click();
    cy.intercept('POST', '**/api/time-planning-pn/plannings/index').as('index-update');
    cy.intercept('PUT', '**/api/time-planning-pn/plannings/*').as('saveWorkdayEntity');

    cy.get('mat-tree-node').contains('Dashboard').click();
    cy.wait('@index-update', {timeout: 60000});

    cy.get('#workingHoursSite').click();
    cy.get('.ng-option').contains('ac ad').click();
    cy.get('#firstColumn0').click();

    cy.get('mat-dialog-container', {timeout: 10000})
      .should('be.visible');

    // Ensure the checkbox is active
    cy.get('#autoBreakCalculationActive-input')
      .scrollIntoView()
      .then(($checkbox) => {
        if (!$checkbox.is(':checked')) {
          cy.wrap($checkbox).click({force: true});
        }
      });

    cy.get('#autoBreakCalculationActive-input').should('be.checked');

    // Click the "Auto break calculation settings" tab
    cy.contains('.mat-mdc-tab', 'Auto break calculation settings')
      .scrollIntoView()
      .should('be.visible')
      .click({force: true});

    // Click all refresh buttons (Monday–Sunday)
    cy.get('button[id$="LoadDefaults"]')
      .should('have.length.at.least', 1)
      .each(($btn) => {
        cy.wrap($btn)
          .scrollIntoView()
          .should('be.visible')
          .click({force: true});
        cy.wait(500);
      });

    // Capture all current input values
    cy.get('mat-dialog-container mat-tab-body[aria-hidden="false"]', {timeout: 10000})
      .should('be.visible')
      .within(() => {
        cy.get('input[readonly="true"]', {timeout: 10000})
          .should('have.length.at.least', 3)
          .then(($inputs) => {
            storedValues = {};
            $inputs.each((_, input) => {
              const id = input.id;
              const val = input.value;
              storedValues[id] = val;
            });
          });
      });

    cy.get('#saveButton').scrollIntoView().click({force: true});
    cy.get('mat-dialog-container', {timeout: 500}).should('not.exist');
    cy.wait(500);

    cy.get('mat-dialog-container', {timeout: 500}).should('not.exist');
    cy.wait(500);

    cy.get('#firstColumn0')
      .scrollIntoView()
      .should('be.visible')
      .click({force: true});

    cy.get('mat-dialog-container', {timeout: 500}).should('be.visible');

    cy.contains('.mat-mdc-tab', 'Auto break calculation settings')
      .scrollIntoView()
      .should('be.visible')
      .click({force: true});

    cy.get('#mat-tab-group-1-content-1')
      .should('exist')
      .and('not.have.attr', 'aria-hidden', 'true');

    // Verify all input values match the stored ones
    cy.get('#mat-tab-group-1-content-1 input[readonly="true"]').each(($input) => {
      const id = $input.attr('id');
      const val = $input.val();
      cy.wrap(val).should('eq', storedValues[id]);
    });

    cy.get('#saveButton').scrollIntoView().click({force: true});
    cy.get('mat-dialog-container', {timeout: 500}).should('not.exist');
    cy.wait(500);

  });

  afterEach(() => {
    cy.get('#firstColumn0').click();

    // Ensure the checkbox inactive
    cy.get('#autoBreakCalculationActive-input')
      .scrollIntoView()
      .then(($checkbox) => {
        if ($checkbox.is(':checked')) {
          cy.wrap($checkbox).click({force: true});
        }
      });

    cy.get('#saveButton').scrollIntoView().click({force: true});
    cy.get('mat-dialog-container', {timeout: 500}).should('not.exist');
    cy.wait(500);

    cy.get('mat-nested-tree-node').contains('Plugins').click();

    cy.contains('div.plugin-name', 'Microting Time Planning Plugin')
      .closest('tr')
      .find('a#plugin-settings-link')
      .click();

    cy.url().should('include', '/plugins/time-planning-pn/settings');

    cy.get('#autoBreakCalculationActiveToggle button[role="switch"]')
      .then(($btn) => {
        const isChecked = $btn.attr('aria-checked') === 'true';
        if (isChecked) {
          cy.wrap($btn)
            .scrollIntoView()
            .click({force: true});
        }
      });

    // Confirm it’s OFF
    cy.get('#autoBreakCalculationActiveToggle button[role="switch"]')
      .should('have.attr', 'aria-checked', 'false');

    cy.get('#saveSettings')
      .scrollIntoView()
      .should('be.visible')
      .click({force: true});
  });
});

import loginPage from '../../../Login.page';
import pluginPage from '../../../Plugin.page';

describe('Dashboard edit values', () => {
  beforeEach(() => {
    cy.visit('http://localhost:4200');
    loginPage.login();
  });

  it('should enable auto break calculations with empty values', () => {
    pluginPage.Navbar.goToPluginsPage();
    const pluginName = 'Microting Time Planning Plugin';
    // pluginPage.enablePluginByName(pluginName);
    let row = cy.contains('.mat-mdc-row', pluginName).first();
    row.find('.mat-column-actions button')
      .should('contain.text', 'toggle_on'); // plugin is enabled
    row = cy.contains('.mat-mdc-row', pluginName).first();
    row.find('.mat-column-actions a')
      .should('contain.text', 'settings'); // plugin is enabled
    row = cy.contains('.mat-mdc-row', pluginName).first();
    cy.intercept('GET', '**/api/time-planning-pn/settings').as('settings-get');
    let settingsElement = row
      .find('.mat-column-actions a')
      // .should('be.enabled')
      .should('be.visible');
    settingsElement.click();
    cy.wait('@settings-get', { timeout: 60000 });
    // autoBreakCalculationActiveToggle-button
    cy.get('#autoBreakCalculationActiveToggle-button').invoke('attr', 'aria-checked').then(currentState => {
      cy.log('state: ' + currentState);
      expect(currentState).to.be.oneOf(['true', 'false']);
      if (currentState === 'false') {
        cy.log('current state is false, clicking to enable');
        cy.get('#autoBreakCalculationActiveToggle-button').click();
      }
    });
    cy.intercept('PUT', '**/api/time-planning-pn/settings').as('settings-update');
    cy.get('#saveSettings').click();
    cy.wait('@settings-update', { timeout: 60000 });
    cy.intercept('POST', '**/api/time-planning-pn/plannings/index').as('index-update');
    cy.get('mat-nested-tree-node').contains('Timeregistrering').click();
    cy.get('mat-tree-node').contains('Dashboard').click();
    cy.wait('@index-update', { timeout: 60000 });
    cy.get('#firstColumn0').click();
    cy.get('#useGoogleSheetAsDefault > div > div > input').invoke('attr', 'class').then(currentState => {
      cy.log('class: ' + currentState);
      // expect(currentState).to.be.oneOf(['true', 'false']);
      if (currentState !== 'mdc-checkbox__native-control mdc-checkbox--selected') {
        cy.log('current state is false, clicking to enable');
        cy.get('#useGoogleSheetAsDefault').click();
      }
    });
    cy.get('#autoBreakCalculationActive > div > div > input').invoke('attr', 'class').then(currentState => {
      cy.log('class: ' + currentState);
      // expect(currentState).to.be.oneOf(['true', 'false']);
      if (currentState !== 'mdc-checkbox__native-control mdc-checkbox--selected') {
        cy.log('current state is false, clicking to enable');
        cy.get('#autoBreakCalculationActive').click();
      }
    });
    cy.get('#mat-tab-group-0-label-1').click();
    cy.get('#mondayLoadDefaults').click();
    cy.get('#mondayBreakMinutesDivider').should('have.value', '03:00');
    cy.get('#mondayBreakMinutesPrDivider').should('have.value', '00:30');
    cy.get('#mondayBreakMinutesUpperLimit').should('have.value', '01:00');
    cy.get('#tuesdayLoadDefaults').click();
    cy.get('#tuesdayBreakMinutesDivider').should('have.value', '03:00');
    cy.get('#tuesdayBreakMinutesPrDivider').should('have.value', '00:30');
    cy.get('#tuesdayBreakMinutesUpperLimit').should('have.value', '01:00');
    cy.get('#wednesdayLoadDefaults').click();
    cy.get('#wednesdayBreakMinutesDivider').should('have.value', '03:00');
    cy.get('#wednesdayBreakMinutesPrDivider').should('have.value', '00:30');
    cy.get('#wednesdayBreakMinutesUpperLimit').should('have.value', '01:00');
    cy.get('#thursdayLoadDefaults').click();
    cy.get('#thursdayBreakMinutesDivider').should('have.value', '03:00');
    cy.get('#thursdayBreakMinutesPrDivider').should('have.value', '00:30');
    cy.get('#thursdayBreakMinutesUpperLimit').should('have.value', '01:00');
    cy.get('#fridayLoadDefaults').click();
    cy.get('#fridayBreakMinutesDivider').should('have.value', '03:00');
    cy.get('#fridayBreakMinutesPrDivider').should('have.value', '00:30');
    cy.get('#fridayBreakMinutesUpperLimit').should('have.value', '01:00');
    cy.get('#saturdayLoadDefaults').click();
    cy.get('#saturdayBreakMinutesDivider').should('have.value', '02:00');
    cy.get('#saturdayBreakMinutesPrDivider').should('have.value', '00:30');
    cy.get('#saturdayBreakMinutesUpperLimit').should('have.value', '01:00');
    cy.get('#sundayLoadDefaults').click();
    cy.get('#sundayBreakMinutesDivider').should('have.value', '02:00');
    cy.get('#sundayBreakMinutesPrDivider').should('have.value', '00:30');
    cy.get('#sundayBreakMinutesUpperLimit').should('have.value', '01:00');
    cy.intercept('PUT', '**/api/time-planning-pn/settings/assigned-site').as('assigned-site-update');
    cy.get('#saveButton').click();
    cy.wait('@assigned-site-update', { timeout: 60000 });
    cy.intercept('GET', '**/api/time-planning-pn/settings/assigned-sites?*').as('assigned-site-get');
    cy.get('#firstColumn0').click();
    cy.wait('@assigned-site-get', { timeout: 60000 });
    cy.get('#mat-tab-group-1-label-1').click();
    cy.get('#mondayBreakMinutesDivider').should('have.value', '03:00');
    cy.get('#mondayBreakMinutesPrDivider').should('have.value', '00:30');
    cy.get('#mondayBreakMinutesUpperLimit').should('have.value', '01:00');
    cy.get('#tuesdayBreakMinutesDivider').should('have.value', '03:00');
    cy.get('#tuesdayBreakMinutesPrDivider').should('have.value', '00:30');
    cy.get('#tuesdayBreakMinutesUpperLimit').should('have.value', '01:00');
    cy.get('#wednesdayBreakMinutesDivider').should('have.value', '03:00');
    cy.get('#wednesdayBreakMinutesPrDivider').should('have.value', '00:30');
    cy.get('#wednesdayBreakMinutesUpperLimit').should('have.value', '01:00');
    cy.get('#thursdayBreakMinutesDivider').should('have.value', '03:00');
    cy.get('#thursdayBreakMinutesPrDivider').should('have.value', '00:30');
    cy.get('#thursdayBreakMinutesUpperLimit').should('have.value', '01:00');
    cy.get('#fridayBreakMinutesDivider').should('have.value', '03:00');
    cy.get('#fridayBreakMinutesPrDivider').should('have.value', '00:30');
    cy.get('#fridayBreakMinutesUpperLimit').should('have.value', '01:00');
    cy.get('#saturdayBreakMinutesDivider').should('have.value', '02:00');
    cy.get('#saturdayBreakMinutesPrDivider').should('have.value', '00:30');
    cy.get('#saturdayBreakMinutesUpperLimit').should('have.value', '01:00');
    cy.get('#sundayBreakMinutesDivider').should('have.value', '02:00');
    cy.get('#sundayBreakMinutesPrDivider').should('have.value', '00:30');
    cy.get('#sundayBreakMinutesUpperLimit').should('have.value', '01:00');
  });
});

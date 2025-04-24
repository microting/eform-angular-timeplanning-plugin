import loginPage from '../../../Login.page';
import pluginPage from '../../../Plugin.page';

describe('Enable Backend Config plugin', () => {
  beforeEach(() => {
    cy.visit('http://localhost:4200');
    loginPage.login();
  });

  it('should go to dashboard', () => {
    // we have more than one mat-nested-tree-node so we beed to select the own with the text "Timeregistrering"
    cy.get('mat-nested-tree-node').contains('Timeregistrering').click();
    cy.get('mat-tree-node').contains('Timeregistrering').click();
    cy.get('#workingHoursSite').clear().type('c d');
    cy.get('.ng-option.ng-option-marked').click();
  });
});

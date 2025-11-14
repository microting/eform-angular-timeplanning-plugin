import loginPage from '../../../Login.page';
import pluginPage from '../../../Plugin.page';

describe('Enable Backend Config plugin', () => {
  beforeEach(() => {
    cy.visit('http://localhost:4200');
    loginPage.login();
    pluginPage.Navbar.goToPluginsPage();
    // pluginPage.rowNum()
    //   .should('not.eq', 0) // we have plugins list
    //   .should('eq', 1); // we have only 1 plugin: time planning
  });
  it('should enabled Time registration plugin', () => {
    const pluginName = 'Microting Time Planning Plugin';
    
    // Open action menu for the plugin
    cy.contains('.mat-mdc-row', pluginName).first().find('#actionMenu').click();
    cy.wait(500);
    
    // Verify the plugin is enabled by checking the status button in the action menu
    cy.get('#plugin-status-button0')
      .find('mat-icon')
      .should('contain.text', 'toggle_on'); // plugin is enabled
  });
});

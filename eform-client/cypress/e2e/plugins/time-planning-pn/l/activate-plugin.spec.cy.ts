import loginPage from '../../../Login.page';
import pluginPage from '../../../Plugin.page';

describe('Time Planning Plugin Activation for Absence Requests Tests', () => {
  beforeEach(() => {
    cy.visit('http://localhost:4200');
    loginPage.login();
  });

  it('should activate time-planning plugin', () => {
    pluginPage.Navbar.goToPluginsPage();
    cy.intercept('GET', '**/api/plugins-management/plugins').as('getPlugins');
    cy.wait('@getPlugins', { timeout: 30000 });
    
    pluginPage.getEnableBtn('time-planning-pn').then((enableBtn) => {
      if (enableBtn.is(':visible')) {
        enableBtn.click();
        cy.wait(1000);
      }
    });
  });
});

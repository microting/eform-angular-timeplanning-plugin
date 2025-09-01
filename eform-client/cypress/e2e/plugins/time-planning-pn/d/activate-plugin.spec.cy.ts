import loginPage from '../../../Login.page';
import pluginPage from '../../../Plugin.page';

describe('Enable Backend Config plugin', () => {
  beforeEach(() => {
    cy.visit('http://localhost:4200');
    loginPage.login();
    pluginPage.Navbar.goToPluginsPage();
  });

  it('should activate the plugin', () => {
    const pluginName = 'Microting Time Planning Plugin';
    pluginPage.enablePluginByName(pluginName);
  });
});

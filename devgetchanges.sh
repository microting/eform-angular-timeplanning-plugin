#!/bin/bash

cd ~

rm -fR Documents/workspace/microting/eform-angular-timeplanning-plugin/eform-client/src/app/plugins/modules/time-planning-pn

cp -a Documents/workspace/microting/eform-angular-frontend/eform-client/src/app/plugins/modules/time-planning-pn Documents/workspace/microting/eform-angular-timeplanning-plugin/eform-client/src/app/plugins/modules/time-planning-pn

rm -fR Documents/workspace/microting/eform-angular-timeplanning-plugin/eFormAPI/Plugins/TimePlanning.Pn

cp -a Documents/workspace/microting/eform-angular-frontend/eFormAPI/Plugins/TimePlanning.Pn Documents/workspace/microting/eform-angular-timeplanning-plugin/eFormAPI/Plugins/TimePlanning.Pn

# Test files rm
rm -fR Documents/workspace/microting/eform-angular-timeplanning-plugin/eform-client/e2e/Tests/time-planning-settings/
rm -fR Documents/workspace/microting/eform-angular-timeplanning-plugin/eform-client/e2e/Tests/time-planning-general/
rm -fR Documents/workspace/microting/eform-angular-timeplanning-plugin/eform-client/wdio-headless-plugin-step2.conf.ts
rm -fR Documents/workspace/microting/eform-angular-timeplanning-plugin/eform-client/e2e/Page\ objects/TimePlanning
rm -fR Documents/workspace/microting/eform-angular-timeplanning-plugin/eform-client/cypress/e2e/plugins/time-planning-pn

# Test files cp
cp -a Documents/workspace/microting/eform-angular-frontend/eform-client/e2e/Tests/time-planning-settings Documents/workspace/microting/eform-angular-timeplanning-plugin/eform-client/e2e/Tests/time-planning-settings
cp -a Documents/workspace/microting/eform-angular-frontend/eform-client/e2e/Tests/time-planning-general Documents/workspace/microting/eform-angular-timeplanning-plugin/eform-client/e2e/Tests/time-planning-general
cp -a Documents/workspace/microting/eform-angular-frontend/eform-client/wdio-plugin-step2.conf.ts Documents/workspace/microting/eform-angular-timeplanning-plugin/eform-client/wdio-headless-plugin-step2.conf.ts
cp -a Documents/workspace/microting/eform-angular-frontend/eform-client/e2e/Page\ objects/TimePlanning Documents/workspace/microting/eform-angular-timeplanning-plugin/eform-client/e2e/Page\ objects/TimePlanning
cp -a Documents/workspace/microting/eform-angular-frontend/eform-client/cypress/e2e/plugins/time-planning-pn Documents/workspace/microting/eform-angular-timeplanning-plugin/eform-client/cypress/e2e/plugins/time-planning-pn

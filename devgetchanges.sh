#!/bin/bash

cd ~

rm -fR Documents/workspace/microting/eform-angular-timeplanning-plugin/eform-client/src/app/plugins/modules/time-planning-pn

cp -a Documents/workspace/microting/eform-angular-frontend/eform-client/src/app/plugins/modules/time-planning-pn Documents/workspace/microting/eform-angular-timeplanning-plugin/eform-client/src/app/plugins/modules/time-planning-pn

rm -fR Documents/workspace/microting/eform-angular-timeplanning-plugin/eFormAPI/Plugins/TimePlanning.Pn

cp -a Documents/workspace/microting/eform-angular-frontend/eFormAPI/Plugins/TimePlanning.Pn Documents/workspace/microting/eform-angular-timeplanning-plugin/eFormAPI/Plugins/TimePlanning.Pn

# Test files rm
rm -fR Documents/workspace/microting/eform-angular-timeplanning-plugin/eform-client/e2e/Tests/time-planning-settings/
rm -fR Documents/workspace/microting/eform-angular-timeplanning-plugin/eform-client/e2e/Tests/time-planning-general/
rm -fR Documents/workspace/microting/eform-angular-timeplanning-plugin/eform-client/wdio-headless-plugin-step2.conf.js
rm -fR Documents/workspace/microting/eform-angular-timeplanning-plugin/eform-client/e2e/Page\ objects/TimePlanning

# Test files cp
cp -a Documents/workspace/microting/eform-angular-frontend/eform-client/e2e/Tests/time-planning-settings Documents/workspace/microting/eform-angular-timeplanning-plugin/eform-client/e2e/Tests/time-planning-settings
cp -a Documents/workspace/microting/eform-angular-frontend/eform-client/e2e/Tests/time-planning-general Documents/workspace/microting/eform-angular-timeplanning-plugin/eform-client/e2e/Tests/time-planning-general
cp -a Documents/workspace/microting/eform-angular-frontend/eform-client/wdio-plugin-step2.conf.js Documents/workspace/microting/eform-angular-timeplanning-plugin/eform-client/wdio-headless-plugin-step2.conf.js
cp -a Documents/workspace/microting/eform-angular-frontend/eform-client/e2e/Page\ objects/TimePlanning Documents/workspace/microting/eform-angular-timeplanning-plugin/eform-client/e2e/Page\ objects/TimePlanning

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using eFormCore;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Services;
using Google.Apis.Sheets.v4;
using Google.Apis.Sheets.v4.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microting.eForm;
using Microting.eForm.Infrastructure.Constants;
using Microting.eFormApi.BasePn.Abstractions;
using Microting.eFormApi.BasePn.Infrastructure.Helpers.PluginDbOptions;
using Microting.TimePlanningBase.Infrastructure.Data;
using Microting.TimePlanningBase.Infrastructure.Data.Entities;
using Sentry;
using TimePlanning.Pn.Infrastructure.Models.Settings;

namespace TimePlanning.Pn.Infrastructure.Helpers;

public class GoogleSheetHelper
{
    public static async Task PushToGoogleSheet(Core core, TimePlanningPnDbContext dbContext, ILogger logger)
    {
        var privateKeyId = Environment.GetEnvironmentVariable("PRIVATE_KEY_ID");
        var googleSheetId = dbContext.PluginConfigurationValues
            .Single(x => x.Name == "TimePlanningBaseSettings:GoogleSheetId").Value;
        if (string.IsNullOrEmpty(privateKeyId))
        {
            return;
        }

        var applicationName = "Google Sheets API Integration";
        var sheetName = "PlanTimer";

        //var core = await coreHelper.GetCore();
        await using var sdkDbContext = core.DbContextHelper.GetDbContext();

        var privateKey = Environment.GetEnvironmentVariable("PRIVATE_KEY"); // Replace with your private key
        var clientEmail = Environment.GetEnvironmentVariable("CLIENT_EMAIL"); // Replace with your client email
        var projectId = Environment.GetEnvironmentVariable("PROJECT_ID"); // Replace with your project ID
        var clientId = Environment.GetEnvironmentVariable("CLIENT_ID"); // Replace with your client ID

        // Construct the JSON for the service account credentials
        string serviceAccountJson = $@"
        {{
          ""type"": ""service_account"",
          ""project_id"": ""{projectId}"",
          ""private_key_id"": ""{privateKeyId}"",
          ""private_key"": ""{privateKey}"",
          ""client_email"": ""{clientEmail}"",
          ""client_id"": ""{clientId}"",
          ""auth_uri"": ""https://accounts.google.com/o/oauth2/auth"",
          ""token_uri"": ""https://oauth2.googleapis.com/token"",
          ""auth_provider_x509_cert_url"": ""https://www.googleapis.com/oauth2/v1/certs"",
          ""client_x509_cert_url"": ""https://www.googleapis.com/robot/v1/metadata/x509/{clientEmail}""
        }}";

        // Authenticate using the dynamically constructed JSON
        var credential = GoogleCredential.FromJson(serviceAccountJson)
            .CreateScoped(SheetsService.Scope.Spreadsheets);

        var service = new SheetsService(new BaseClientService.Initializer
        {
            HttpClientInitializer = credential,
            ApplicationName = applicationName
        });

        try
        {
            var headerRequest = service.Spreadsheets.Values.Get(googleSheetId, $"{sheetName}!A1:1");
            var headerResponse = await headerRequest.ExecuteAsync();
            var existingHeaders = headerResponse.Values?.FirstOrDefault() ?? new List<object>();

            var assignedSites = await dbContext.AssignedSites
                .Where(x => x.WorkflowState != Constants.WorkflowStates.Removed)
                .Select(x => x.SiteId)
                .Distinct()
                .ToListAsync();

            var siteNames = await sdkDbContext.Sites
                .Where(x => assignedSites.Contains(x.MicrotingUid!.Value))
                .OrderBy(x => x.Name)
                .Select(x => x.Name)
                .ToListAsync();

            var newHeaders = existingHeaders.Cast<string>().ToList();
            foreach (var siteName in siteNames)
            {
                var timerHeader = $"{siteName} - timer";
                var textHeader = $"{siteName} - tekst";
                if (!newHeaders.Contains(timerHeader))
                {
                    newHeaders.Add(timerHeader);
                }

                if (!newHeaders.Contains(textHeader))
                {
                    newHeaders.Add(textHeader);
                }
            }

            if (!existingHeaders.Cast<string>().SequenceEqual(newHeaders))
            {
                var updateRequest = new ValueRange
                {
                    Values = new List<IList<object>> { newHeaders.Cast<object>().ToList() }
                };

                var columnLetter = GetColumnLetter(newHeaders.Count);
                updateRequest = new ValueRange
                {
                    Values = new List<IList<object>> { newHeaders.Cast<object>().ToList() }
                };
                var updateHeaderRequest =
                    service.Spreadsheets.Values.Update(updateRequest, googleSheetId, $"{sheetName}!A1:{columnLetter}1");
                updateHeaderRequest.ValueInputOption =
                    SpreadsheetsResource.ValuesResource.UpdateRequest.ValueInputOptionEnum.RAW;
                await updateHeaderRequest.ExecuteAsync();

                logger.LogInformation("Headers updated successfully.");
            }

            AutoAdjustColumnWidths(service, googleSheetId, sheetName, logger);

            try
            {
                // ... existing code ...

                var sheet = service.Spreadsheets.Get(googleSheetId).Execute().Sheets
                    .FirstOrDefault(s => s.Properties.Title == sheetName);
                if (sheet == null) throw new Exception($"Sheet '{sheetName}' not found.");

                var sheetId = sheet.Properties.SheetId;

                // ... existing code ...

                SetAlternatingColumnColors(service, googleSheetId, sheetId!.Value, newHeaders.Count, logger);

                logger.LogInformation("Headers are already up-to-date.");
            }
            catch (Exception ex)
            {
                logger.LogError($"An error occurred: {ex.Message}");
            }

            logger.LogInformation("Headers are already up-to-date.");
        }
        catch (Exception ex)
        {
            logger.LogError($"An error occurred: {ex.Message}");
        }
    }

    public static async Task PullEverythingFromGoogleSheet(Core core, TimePlanningPnDbContext dbContext, ILogger logger)
    {
        var privateKeyId = Environment.GetEnvironmentVariable("PRIVATE_KEY_ID");
        var googleSheetId = dbContext.PluginConfigurationValues
            .Single(x => x.Name == "TimePlanningBaseSettings:GoogleSheetId").Value;
        if (string.IsNullOrEmpty(privateKeyId))
        {
            return;
        }

        var applicationName = "Google Sheets API Integration";
        var sheetName = "PlanTimer";

        //var core = await coreHelper.GetCore();
        await using var sdkDbContext = core.DbContextHelper.GetDbContext();

        var privateKey = Environment.GetEnvironmentVariable("PRIVATE_KEY"); // Replace with your private key
        var clientEmail = Environment.GetEnvironmentVariable("CLIENT_EMAIL"); // Replace with your client email
        var projectId = Environment.GetEnvironmentVariable("PROJECT_ID"); // Replace with your project ID
        var clientId = Environment.GetEnvironmentVariable("CLIENT_ID"); // Replace with your client ID

        // Construct the JSON for the service account credentials
        string serviceAccountJson = $@"
        {{
          ""type"": ""service_account"",
          ""project_id"": ""{projectId}"",
          ""private_key_id"": ""{privateKeyId}"",
          ""private_key"": ""{privateKey}"",
          ""client_email"": ""{clientEmail}"",
          ""client_id"": ""{clientId}"",
          ""auth_uri"": ""https://accounts.google.com/o/oauth2/auth"",
          ""token_uri"": ""https://oauth2.googleapis.com/token"",
          ""auth_provider_x509_cert_url"": ""https://www.googleapis.com/oauth2/v1/certs"",
          ""client_x509_cert_url"": ""https://www.googleapis.com/robot/v1/metadata/x509/{clientEmail}""
        }}";

        // Authenticate using the dynamically constructed JSON
        var credential = GoogleCredential.FromJson(serviceAccountJson)
            .CreateScoped(SheetsService.Scope.Spreadsheets);

        var service = new SheetsService(new BaseClientService.Initializer
        {
            HttpClientInitializer = credential,
            ApplicationName = applicationName
        });

        var range = $"PlanTimer";
        var request =
            service.Spreadsheets.Values.Get(googleSheetId, range);

        // Fetch the data from the sheet
        var response = await request.ExecuteAsync();
        var values = response.Values;

        var headerRows = values?.FirstOrDefault();
        if (values is {Count: > 0})
        {
            // Skip the header row (first row)
            for (var i = 1; i < values.Count; i++)
            {
                var row = values[i];
                // Process each row
                string date = row[0].ToString();

                // Process the dato as date

                // Parse date and validate
                if (!DateTime.TryParseExact(date, "dd.MM.yyyy", CultureInfo.InvariantCulture,
                        DateTimeStyles.None, out var _))
                {
                    continue;
                }

                var dateValue = DateTime.ParseExact(date, "dd.MM.yyyy", CultureInfo.InvariantCulture);
                Console.WriteLine($"Processing date: {dateValue}");

                if (dateValue > DateTime.Now.AddDays(180))
                {
                    continue;
                }

                // Iterate over each pair of columns starting from the fourth column
                for (int j = 3; j < row.Count; j += 2)
                {

                    var siteName = headerRows[j].ToString().Split(" - ").Length > 1
                        ? headerRows[j].ToString().Split(" - ")[0].ToLower().Replace(" ", "").Trim()
                        : headerRows[j].ToString().Split(" - ").First().ToLower().Replace(" ", "").Trim();
                    Console.WriteLine($"Processing site: {siteName}");
                    var site = await sdkDbContext.Sites
                        .Where(x => x.WorkflowState != Constants.WorkflowStates.Removed)
                        .FirstOrDefaultAsync(x =>
                            x.Name.Replace(" ", "").Replace("-", "").ToLower() == siteName);
                    if (site == null)
                    {
                        continue;
                    }

                    var planHours = row.Count > j ? row[j].ToString() : string.Empty;
                    var planText = row.Count > j + 1 ? row[j + 1].ToString() : string.Empty;

                    if (string.IsNullOrEmpty(planHours))
                    {
                        planHours = "0";
                    }

                    // Replace comma with dot if needed
                    if (planHours.Contains(','))
                    {
                        planHours = planHours.Replace(",", ".");
                    }

                    double parsedPlanHours = double.Parse(planHours, NumberStyles.AllowDecimalPoint,
                        NumberFormatInfo.InvariantInfo);

                    var preTimePlanning = await dbContext.PlanRegistrations.AsNoTracking()
                        .Where(x => x.WorkflowState != Constants.WorkflowStates.Removed)
                        .Where(x => x.Date < dateValue && x.SdkSitId == (int) site.MicrotingUid!)
                        .OrderByDescending(x => x.Date)
                        .FirstOrDefaultAsync();

                    var midnight = new DateTime(dateValue.Year, dateValue.Month, dateValue.Day, 0, 0, 0);

                    var planRegistrations = await dbContext.PlanRegistrations.Where(x =>
                        x.Date == midnight && x.SdkSitId == site.MicrotingUid)
                        .Where(x => x.WorkflowState != Constants.WorkflowStates.Removed)
                        .ToListAsync();
                    if (planRegistrations.Count > 1)
                    {
                        Console.WriteLine(
                            $"Found multiple plan registrations for site: {site.Name} and date: {dateValue}. This should not happen.");
                        SentrySdk.CaptureMessage(
                            $"Found multiple plan registrations for site: {site.Name} and date: {dateValue}. This should not happen.");
                        foreach (var plan in planRegistrations)
                        {
                            Console.WriteLine(
                                $"PlanRegistration ID: {plan.Id}, PlanText: {plan.PlanText}, PlanHours: {plan.PlanHours}, Date: {plan.Date}, workflowState: {plan.WorkflowState}, SdkSitId: {plan.SdkSitId}");
                            SentrySdk.CaptureMessage(
                                $"PlanRegistration ID: {plan.Id}, PlanText: {plan.PlanText}, PlanHours: {plan.PlanHours}, Date: {plan.Date}, workflowState: {plan.WorkflowState}, SdkSitId: {plan.SdkSitId}");
                        }
                        continue;
                    }
                    var planRegistration = planRegistrations.FirstOrDefault();

                    if (planRegistration == null)
                    {
                        planRegistration = new PlanRegistration
                        {
                            Date = midnight,
                            PlanText = planText,
                            PlanHours = parsedPlanHours,
                            SdkSitId = (int) site.MicrotingUid!,
                            CreatedByUserId = 1,
                            UpdatedByUserId = 1,
                            NettoHours = 0,
                            PaiedOutFlex = 0,
                            Pause1Id = 0,
                            Pause2Id = 0,
                            Start1Id = 0,
                            Start2Id = 0,
                            Stop1Id = 0,
                            Stop2Id = 0,
                            Flex = 0,
                            StatusCaseId = 0
                        };

                        if (!string.IsNullOrEmpty(planRegistration.PlanText))
                        {
                            var splitList = planRegistration.PlanText.Split(';');
                            var firsSplit = splitList[0];

                            var regex = new Regex(@"(.*)-(.*)\/(.*)");
                            var match = regex.Match(firsSplit);
                            if (match.Captures.Count == 0)
                            {
                                regex = new Regex(@"(.*)-(.*)");
                                match = regex.Match(firsSplit);

                                if (match.Captures.Count == 1)
                                {
                                    var firstPart = match.Groups[1].Value;
                                    var firstPartSplit =
                                        firstPart.Split(['.', ':', '½'], StringSplitOptions.RemoveEmptyEntries);
                                    var firstPartHours = int.Parse(firstPartSplit[0]);
                                    var firstPartMinutes = firstPartSplit.Length > 1 ? int.Parse(firstPartSplit[1]) : 0;
                                    var firstPartTotalMinutes = firstPartHours * 60 + firstPartMinutes;
                                    var secondPart = match.Groups[2].Value;
                                    var secondPartSplit =
                                        secondPart.Split(['.', ':', '½'], StringSplitOptions.RemoveEmptyEntries);
                                    var secondPartHours = int.Parse(secondPartSplit[0]);
                                    var secondPartMinutes =
                                        secondPartSplit.Length > 1 ? int.Parse(secondPartSplit[1]) : 0;
                                    var secondPartTotalMinutes = secondPartHours * 60 + secondPartMinutes;
                                    planRegistration.PlannedStartOfShift1 = firstPartTotalMinutes;
                                    planRegistration.PlannedEndOfShift1 = secondPartTotalMinutes;

                                    if (match.Groups.Count == 4)
                                    {
                                        var breakPart = match.Groups[3].Value.Replace(",", ".").Trim();

                                        var breakPartMinutes = BreakTimeCalculator(breakPart);

                                        planRegistration.PlannedBreakOfShift1 = breakPartMinutes;
                                    }
                                    else
                                    {
                                        planRegistration.PlannedBreakOfShift1 = 0;
                                    }
                                }
                            }

                            if (match.Captures.Count == 1)
                            {
                                var firstPart = match.Groups[1].Value;
                                var firstPartSplit =
                                    firstPart.Split(['.', ':', '½'], StringSplitOptions.RemoveEmptyEntries);
                                var firstPartHours = int.Parse(firstPartSplit[0]);
                                var firstPartMinutes = firstPartSplit.Length > 1 ? int.Parse(firstPartSplit[1]) : 0;
                                var firstPartTotalMinutes = firstPartHours * 60 + firstPartMinutes;
                                var secondPart = match.Groups[2].Value;
                                var secondPartSplit =
                                    secondPart.Split(['.', ':', '½'], StringSplitOptions.RemoveEmptyEntries);
                                var secondPartHours = int.Parse(secondPartSplit[0]);
                                var secondPartMinutes =
                                    secondPartSplit.Length > 1 ? int.Parse(secondPartSplit[1]) : 0;
                                var secondPartTotalMinutes = secondPartHours * 60 + secondPartMinutes;
                                planRegistration.PlannedStartOfShift1 = firstPartTotalMinutes;
                                planRegistration.PlannedEndOfShift1 = secondPartTotalMinutes;

                                if (match.Groups.Count == 4)
                                {
                                    var breakPart = match.Groups[3].Value.Replace(",", ".").Trim();

                                    var breakPartMinutes = BreakTimeCalculator(breakPart);

                                    planRegistration.PlannedBreakOfShift1 = breakPartMinutes;
                                }
                                else
                                {
                                    planRegistration.PlannedBreakOfShift1 = 0;
                                }
                            }

                            if (splitList.Length > 1)
                            {
                                var secondSplit = splitList[1];
                                regex = new Regex(@"(.*)-(.*)\/(.*)");
                                match = regex.Match(secondSplit);
                                if (match.Captures.Count == 0)
                                {
                                    regex = new Regex(@"(.*)-(.*)");
                                    match = regex.Match(secondSplit);

                                    if (match.Captures.Count == 1)
                                    {
                                        var firstPart = match.Groups[1].Value;
                                        var firstPartSplit =
                                            firstPart.Split(['.', ':', '½'], StringSplitOptions.RemoveEmptyEntries);
                                        var firstPartHours = int.Parse(firstPartSplit[0]);
                                        var firstPartMinutes =
                                            firstPartSplit.Length > 1 ? int.Parse(firstPartSplit[1]) : 0;
                                        var firstPartTotalMinutes = firstPartHours * 60 + firstPartMinutes;
                                        var secondPart = match.Groups[2].Value;
                                        var secondPartSplit =
                                            secondPart.Split(['.', ':', '½'], StringSplitOptions.RemoveEmptyEntries);
                                        var secondPartHours = int.Parse(secondPartSplit[0]);
                                        var secondPartMinutes =
                                            secondPartSplit.Length > 1 ? int.Parse(secondPartSplit[1]) : 0;
                                        var secondPartTotalMinutes = secondPartHours * 60 + secondPartMinutes;
                                        planRegistration.PlannedStartOfShift2 = firstPartTotalMinutes;
                                        planRegistration.PlannedEndOfShift2 = secondPartTotalMinutes;

                                        if (match.Groups.Count == 4)
                                        {
                                            var breakPart = match.Groups[3].Value.Replace(",", ".").Trim();

                                            var breakPartMinutes = BreakTimeCalculator(breakPart);

                                            planRegistration.PlannedBreakOfShift2 = breakPartMinutes;
                                        }
                                        else
                                        {
                                            planRegistration.PlannedBreakOfShift2 = 0;
                                        }
                                    }
                                }

                                if (match.Captures.Count == 1)
                                {
                                    var firstPart = match.Groups[1].Value;
                                    var firstPartSplit =
                                        firstPart.Split(['.', ':', '½'], StringSplitOptions.RemoveEmptyEntries);
                                    var firstPartHours = int.Parse(firstPartSplit[0]);
                                    var firstPartMinutes = firstPartSplit.Length > 1 ? int.Parse(firstPartSplit[1]) : 0;
                                    var firstPartTotalMinutes = firstPartHours * 60 + firstPartMinutes;
                                    var secondPart = match.Groups[2].Value;
                                    var secondPartSplit =
                                        secondPart.Split(['.', ':', '½'], StringSplitOptions.RemoveEmptyEntries);
                                    var secondPartHours = int.Parse(secondPartSplit[0]);
                                    var secondPartMinutes =
                                        secondPartSplit.Length > 1 ? int.Parse(secondPartSplit[1]) : 0;
                                    var secondPartTotalMinutes = secondPartHours * 60 + secondPartMinutes;
                                    planRegistration.PlannedStartOfShift2 = firstPartTotalMinutes;
                                    planRegistration.PlannedEndOfShift2 = secondPartTotalMinutes;

                                    if (match.Groups.Count == 4)
                                    {
                                        var breakPart = match.Groups[3].Value.Replace(",", ".").Trim();

                                        var breakPartMinutes = BreakTimeCalculator(breakPart);

                                        planRegistration.PlannedBreakOfShift2 = breakPartMinutes;
                                    }
                                    else
                                    {
                                        planRegistration.PlannedBreakOfShift2 = 0;
                                    }
                                }
                            }

                            if (splitList.Length > 2)
                            {
                                var thirdSplit = splitList[2];
                                regex = new Regex(@"(.*)-(.*)\/(.*)");
                                match = regex.Match(thirdSplit);
                                if (match.Captures.Count == 0)
                                {
                                    regex = new Regex(@"(.*)-(.*)");
                                    match = regex.Match(thirdSplit);

                                    if (match.Captures.Count == 1)
                                    {
                                        var firstPart = match.Groups[1].Value;
                                        var firstPartSplit =
                                            firstPart.Split(['.', ':', '½'], StringSplitOptions.RemoveEmptyEntries);
                                        var firstPartHours = int.Parse(firstPartSplit[0]);
                                        var firstPartMinutes =
                                            firstPartSplit.Length > 1 ? int.Parse(firstPartSplit[1]) : 0;
                                        var firstPartTotalMinutes = firstPartHours * 60 + firstPartMinutes;
                                        var secondPart = match.Groups[2].Value;
                                        var secondPartSplit =
                                            secondPart.Split(['.', ':', '½'], StringSplitOptions.RemoveEmptyEntries);
                                        var secondPartHours = int.Parse(secondPartSplit[0]);
                                        var secondPartMinutes =
                                            secondPartSplit.Length > 1 ? int.Parse(secondPartSplit[1]) : 0;
                                        var secondPartTotalMinutes = secondPartHours * 60 + secondPartMinutes;
                                        planRegistration.PlannedStartOfShift3 = firstPartTotalMinutes;
                                        planRegistration.PlannedEndOfShift3 = secondPartTotalMinutes;

                                        if (match.Groups.Count == 4)
                                        {
                                            var breakPart = match.Groups[3].Value.Replace(",", ".").Trim();
                                            var breakPartMinutes = BreakTimeCalculator(breakPart);

                                            planRegistration.PlannedBreakOfShift3 = breakPartMinutes;
                                        }
                                        else
                                        {
                                            planRegistration.PlannedBreakOfShift3 = 0;
                                        }
                                    }
                                }
                            }

                            if (splitList.Length > 3)
                            {
                                var fourthSplit = splitList[3];
                                regex = new Regex(@"(.*)-(.*)\/(.*)");
                                match = regex.Match(fourthSplit);
                                if (match.Captures.Count == 0)
                                {
                                    regex = new Regex(@"(.*)-(.*)");
                                    match = regex.Match(fourthSplit);

                                    if (match.Captures.Count == 1)
                                    {
                                        var firstPart = match.Groups[1].Value;
                                        var firstPartSplit =
                                            firstPart.Split(['.', ':', '½'], StringSplitOptions.RemoveEmptyEntries);
                                        var firstPartHours = int.Parse(firstPartSplit[0]);
                                        var firstPartMinutes =
                                            firstPartSplit.Length > 1 ? int.Parse(firstPartSplit[1]) : 0;
                                        var firstPartTotalMinutes = firstPartHours * 60 + firstPartMinutes;
                                        var secondPart = match.Groups[2].Value;
                                        var secondPartSplit =
                                            secondPart.Split(['.', ':', '½'], StringSplitOptions.RemoveEmptyEntries);
                                        var secondPartHours = int.Parse(secondPartSplit[0]);
                                        var secondPartMinutes =
                                            secondPartSplit.Length > 1 ? int.Parse(secondPartSplit[1]) : 0;
                                        var secondPartTotalMinutes = secondPartHours * 60 + secondPartMinutes;
                                        planRegistration.PlannedStartOfShift4 = firstPartTotalMinutes;
                                        planRegistration.PlannedEndOfShift4 = secondPartTotalMinutes;

                                        if (match.Groups.Count == 4)
                                        {
                                            var breakPart = match.Groups[3].Value.Replace(",", ".").Trim();
                                            var breakPartMinutes = BreakTimeCalculator(breakPart);

                                            planRegistration.PlannedBreakOfShift4 = breakPartMinutes;
                                        }
                                        else
                                        {
                                            planRegistration.PlannedBreakOfShift4 = 0;
                                        }
                                    }
                                }
                            }

                            if (splitList.Length > 4)
                            {
                                var fifthSplit = splitList[4];
                                regex = new Regex(@"(.*)-(.*)\/(.*)");
                                match = regex.Match(fifthSplit);
                                if (match.Captures.Count == 0)
                                {
                                    regex = new Regex(@"(.*)-(.*)");
                                    match = regex.Match(fifthSplit);

                                    if (match.Captures.Count == 1)
                                    {
                                        var firstPart = match.Groups[1].Value;
                                        var firstPartSplit =
                                            firstPart.Split(['.', ':', '½'], StringSplitOptions.RemoveEmptyEntries);
                                        var firstPartHours = int.Parse(firstPartSplit[0]);
                                        var firstPartMinutes =
                                            firstPartSplit.Length > 1 ? int.Parse(firstPartSplit[1]) : 0;
                                        var firstPartTotalMinutes = firstPartHours * 60 + firstPartMinutes;
                                        var secondPart = match.Groups[2].Value;
                                        var secondPartSplit =
                                            secondPart.Split(['.', ':', '½'], StringSplitOptions.RemoveEmptyEntries);
                                        var secondPartHours = int.Parse(secondPartSplit[0]);
                                        var secondPartMinutes =
                                            secondPartSplit.Length > 1 ? int.Parse(secondPartSplit[1]) : 0;
                                        var secondPartTotalMinutes = secondPartHours * 60 + secondPartMinutes;
                                        planRegistration.PlannedStartOfShift5 = firstPartTotalMinutes;
                                        planRegistration.PlannedEndOfShift5 = secondPartTotalMinutes;

                                        if (match.Groups.Count == 4)
                                        {
                                            var breakPart = match.Groups[3].Value.Replace(",", ".").Trim();

                                            var breakPartMinutes = BreakTimeCalculator(breakPart);

                                            planRegistration.PlannedBreakOfShift5 = breakPartMinutes;
                                        }
                                        else
                                        {
                                            planRegistration.PlannedBreakOfShift5 = 0;
                                        }
                                    }
                                }

                                var calculatedPlanHoursInMinutes = 0;
                                var originalPlanHours = planRegistration.PlanHours;
                                if (planRegistration.PlannedStartOfShift1 != 0 && planRegistration.PlannedEndOfShift1 != 0)
                                {
                                    calculatedPlanHoursInMinutes += planRegistration.PlannedEndOfShift1 -
                                                                    planRegistration.PlannedStartOfShift1 -
                                                                    planRegistration.PlannedBreakOfShift1;
                                    planRegistration.PlanHours = calculatedPlanHoursInMinutes / 60.0;
                                }

                                if (planRegistration.PlannedStartOfShift2 != 0 && planRegistration.PlannedEndOfShift2 != 0)
                                {
                                    calculatedPlanHoursInMinutes += planRegistration.PlannedEndOfShift2 -
                                                                    planRegistration.PlannedStartOfShift2 -
                                                                    planRegistration.PlannedBreakOfShift2;
                                    planRegistration.PlanHours = calculatedPlanHoursInMinutes / 60.0;
                                }

                                if (planRegistration.PlannedStartOfShift3 != 0 && planRegistration.PlannedEndOfShift3 != 0)
                                {
                                    calculatedPlanHoursInMinutes += planRegistration.PlannedEndOfShift3 -
                                                                    planRegistration.PlannedStartOfShift3 -
                                                                    planRegistration.PlannedBreakOfShift3;
                                    planRegistration.PlanHours = calculatedPlanHoursInMinutes / 60.0;
                                }

                                if (planRegistration.PlannedStartOfShift4 != 0 && planRegistration.PlannedEndOfShift4 != 0)
                                {
                                    calculatedPlanHoursInMinutes += planRegistration.PlannedEndOfShift4 -
                                                                    planRegistration.PlannedStartOfShift4 -
                                                                    planRegistration.PlannedBreakOfShift4;
                                    planRegistration.PlanHours = calculatedPlanHoursInMinutes / 60.0;
                                }

                                if (planRegistration.PlannedStartOfShift5 != 0 && planRegistration.PlannedEndOfShift5 != 0)
                                {
                                    calculatedPlanHoursInMinutes += planRegistration.PlannedEndOfShift5 -
                                                                    planRegistration.PlannedStartOfShift5 -
                                                                    planRegistration.PlannedBreakOfShift5;
                                    planRegistration.PlanHours = calculatedPlanHoursInMinutes / 60.0;
                                }
                            }
                        }

                        if (preTimePlanning != null)
                        {
                            planRegistration.SumFlexStart = preTimePlanning.SumFlexEnd;
                            planRegistration.SumFlexEnd =
                                preTimePlanning.SumFlexEnd + planRegistration.NettoHours -
                                planRegistration.PlanHours -
                                planRegistration.PaiedOutFlex;
                            planRegistration.Flex = planRegistration.NettoHours - planRegistration.PlanHours;
                        }
                        else
                        {
                            planRegistration.SumFlexEnd =
                                planRegistration.NettoHours - planRegistration.PlanHours -
                                planRegistration.PaiedOutFlex;
                            planRegistration.SumFlexStart = 0;
                            planRegistration.Flex = planRegistration.NettoHours - planRegistration.PlanHours;
                        }

                        await planRegistration.Create(dbContext);
                    }
                    else
                    {
                        // print to console if the current PlanText is different from the one in the database
                        if (planRegistration.PlanText != planText)
                        {
                            Console.WriteLine(
                                $"PlanText for site: {site.Name} and date: {dateValue} has changed from {planRegistration.PlanText} to {planText}");
                        }

                        planRegistration.PlanText = planText;
                        // print to console if the current PlanHours is different from the one in the database
                        if (planRegistration.PlanHours != parsedPlanHours)
                        {
                            Console.WriteLine(
                                $"PlanHours for site: {site.Name} and date: {dateValue} has changed from {planRegistration.PlanHours} to {parsedPlanHours}");
                        }

                        planRegistration.PlanHours = parsedPlanHours;
                        planRegistration.UpdatedByUserId = 1;

                        var regex = new Regex(@"(.*)-(.*)\/(.*)");
                        var match = regex.Match(planRegistration.PlanText);
                        if (match.Captures.Count == 0)
                        {
                            regex = new Regex(@"(.*)-(.*)");
                            match = regex.Match(planRegistration.PlanText);
                        }

                        if (match.Captures.Count == 1)
                        {
                            var firstPart = match.Groups[1].Value;
                            var firstPartSplit =
                                firstPart.Split(['.', ':', '½'], StringSplitOptions.RemoveEmptyEntries);
                            var firstPartHours = int.Parse(firstPartSplit[0]);
                            var firstPartMinutes = firstPartSplit.Length > 1 ? int.Parse(firstPartSplit[1]) : 0;
                            var firstPartTotalMinutes = firstPartHours * 60 + firstPartMinutes;
                            var secondPart = match.Groups[2].Value;
                            var secondPartSplit =
                                secondPart.Split(['.', ':', '½'], StringSplitOptions.RemoveEmptyEntries);
                            var secondPartHours = int.Parse(secondPartSplit[0]);
                            var secondPartMinutes =
                                secondPartSplit.Length > 1 ? int.Parse(secondPartSplit[1]) : 0;
                            var secondPartTotalMinutes = secondPartHours * 60 + secondPartMinutes;
                            planRegistration.PlannedStartOfShift1 = firstPartTotalMinutes;
                            planRegistration.PlannedEndOfShift1 = secondPartTotalMinutes;

                            if (match.Groups.Count == 4)
                            {
                                var breakPart = match.Groups[3].Value.Replace(",", ".").Trim();
                                var breakPartMinutes = breakPart switch
                                {
                                    "0.5" => 30,
                                    ".5" => 30,
                                    ".75" => 45,
                                    "0.75" => 45,
                                    "¾" => 45,
                                    "½" => 30,
                                    "1" => 60,
                                    _ => 0
                                };

                                planRegistration.PlannedBreakOfShift1 = breakPartMinutes;
                            }
                        }

                        if (preTimePlanning != null)
                        {
                            planRegistration.SumFlexStart = preTimePlanning.SumFlexEnd;
                            planRegistration.SumFlexEnd =
                                preTimePlanning.SumFlexEnd + planRegistration.PlanHours -
                                planRegistration.NettoHours -
                                planRegistration.PaiedOutFlex;
                            planRegistration.Flex = planRegistration.NettoHours - planRegistration.PlanHours;
                        }
                        else
                        {
                            planRegistration.SumFlexEnd =
                                planRegistration.PlanHours - planRegistration.NettoHours -
                                planRegistration.PaiedOutFlex;
                            planRegistration.SumFlexStart = 0;
                            planRegistration.Flex = planRegistration.NettoHours - planRegistration.PlanHours;
                        }

                        await planRegistration.Update(dbContext);
                    }
                }
            }
        }
        else
        {
            Console.WriteLine("No data found.");
        }
    }

    private static int BreakTimeCalculator(string breakPart)
    {
        return breakPart switch
        {
            "0.1" => 5,
            ".1" => 5,
            "0.15" => 10,
            ".15" => 10,
            "0.25" => 15,
            ".25" => 15,
            "0.3" => 20,
            ".3" => 20,
            "0.4" => 25,
            ".4" => 25,
            "0.5" => 30,
            ".5" => 30,
            "0.6" => 35,
            ".6" => 35,
            "0.7" => 40,
            ".7" => 40,
            "0.75" => 45,
            ".75" => 45,
            "0.8" => 50,
            ".8" => 50,
            "0.9" => 55,
            ".9" => 55,
            "¾" => 45,
            "½" => 30,
            "1" => 60,
            _ => 0
        };
    }

    static void AutoAdjustColumnWidths(SheetsService service, string spreadsheetId, string sheetName, ILogger logger)
    {
        try
        {
            var sheet = service.Spreadsheets.Get(spreadsheetId).Execute().Sheets
                .FirstOrDefault(s => s.Properties.Title == sheetName);
            if (sheet == null) throw new Exception($"Sheet '{sheetName}' not found.");

            var sheetId = sheet.Properties.SheetId;

            var autoResizeRequest = new Request
            {
                AutoResizeDimensions = new AutoResizeDimensionsRequest
                {
                    Dimensions = new DimensionRange
                    {
                        SheetId = sheetId,
                        Dimension = "COLUMNS",
                        StartIndex = 0, // Start from the first column
                        EndIndex = sheet.Properties.GridProperties.ColumnCount // Auto-adjust all columns
                    }
                }
            };

            var batchRequest = new BatchUpdateSpreadsheetRequest
            {
                Requests = new List<Request> { autoResizeRequest }
            };

            service.Spreadsheets.BatchUpdate(batchRequest, spreadsheetId).Execute();

            logger.LogInformation("Column widths auto-adjusted successfully.");
        }
        catch (Exception ex)
        {
            logger.LogError($"An error occurred while auto-adjusting column widths: {ex.Message}");
        }
    }

    private static string GetColumnLetter(int columnIndex)
    {
        string columnLetter = "";
        while (columnIndex > 0)
        {
            int modulo = (columnIndex - 1) % 26;
            columnLetter = Convert.ToChar(65 + modulo) + columnLetter;
            columnIndex = (columnIndex - modulo) / 26;
        }

        return columnLetter;
    }

    static void SetAlternatingColumnColors(SheetsService service, string spreadsheetId, int sheetId, int columnCount,
        ILogger logger)
    {
        var requests = new List<Request>();

        for (int i = 3; i < columnCount; i += 2) // Start from column D (index 3) and increment by 2
        {
            var color1 = new Color { Red = 1, Green = 1, Blue = 1 };
            var color2 = new Color { Red = 0.9f, Green = 0.9f, Blue = 0.9f };

            var color = ((i / 2) % 2 == 0) ? color1 : color2;

            var updateCellsRequest1 = new Request
            {
                RepeatCell = new RepeatCellRequest
                {
                    Range = new GridRange
                    {
                        SheetId = sheetId,
                        StartColumnIndex = i,
                        EndColumnIndex = i + 1
                    },
                    Cell = new CellData
                    {
                        UserEnteredFormat = new CellFormat
                        {
                            BackgroundColor = color
                        }
                    },
                    Fields = "userEnteredFormat.backgroundColor"
                }
            };

            var updateCellsRequest2 = new Request
            {
                RepeatCell = new RepeatCellRequest
                {
                    Range = new GridRange
                    {
                        SheetId = sheetId,
                        StartColumnIndex = i + 1,
                        EndColumnIndex = i + 2
                    },
                    Cell = new CellData
                    {
                        UserEnteredFormat = new CellFormat
                        {
                            BackgroundColor = color
                        }
                    },
                    Fields = "userEnteredFormat.backgroundColor"
                }
            };

            requests.Add(updateCellsRequest1);
            requests.Add(updateCellsRequest2);
        }

        var batchUpdateRequest = new BatchUpdateSpreadsheetRequest
        {
            Requests = requests
        };

        service.Spreadsheets.BatchUpdate(batchUpdateRequest, spreadsheetId).Execute();

        logger.LogInformation("Alternating column colors set successfully.");
    }
}
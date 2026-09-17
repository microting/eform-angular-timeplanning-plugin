using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
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
using Microting.TimePlanningBase.Infrastructure.Helpers;
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
        using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(serviceAccountJson));
        var credential = ServiceAccountCredential.FromServiceAccountData(stream);

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

            // Matching a header by its exact text used to append a second
            // column whenever a header had been retyped with other spacing, a
            // different dash or other capitals. The planner matches the way the
            // import does, and only ever appends.
            var appends = PlanTimerSheetColumns.PlanAppends(existingHeaders, siteNames);
            foreach (var problem in appends.Problems)
            {
                logger.LogWarning("PlanTimer sheet: {Problem}", problem);
                SentrySdk.CaptureMessage($"PlanTimer sheet: {problem}", SentryLevel.Warning);
            }

            if (appends.Headers.Count > 0)
            {
                // Only the appended cells are written. Rewriting the whole row
                // would restate every existing header, so any header a human had
                // corrected would be silently reverted.
                var firstNewColumn = appends.FirstColumn;
                var range = $"{sheetName}!" +
                            $"{PlanTimerSheetColumns.ColumnLetter(firstNewColumn)}1:" +
                            $"{PlanTimerSheetColumns.ColumnLetter(firstNewColumn + appends.Headers.Count - 1)}1";
                var updateRequest = new ValueRange
                {
                    Values = new List<IList<object>> { appends.Headers.Cast<object>().ToList() }
                };
                var updateHeaderRequest =
                    service.Spreadsheets.Values.Update(updateRequest, googleSheetId, range);
                updateHeaderRequest.ValueInputOption =
                    SpreadsheetsResource.ValuesResource.UpdateRequest.ValueInputOptionEnum.RAW;
                await updateHeaderRequest.ExecuteAsync();

                logger.LogInformation("Appended {Count} header(s) to the PlanTimer sheet at {Range}.",
                    appends.Headers.Count, range);
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

                // SetAlternatingColumnColors(service, googleSheetId, sheetId!.Value, newHeaders.Count, logger);

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
        using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(serviceAccountJson));
        var credential = ServiceAccountCredential.FromServiceAccountData(stream);

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
            // Pre-load all sites and build a column-index-to-site mapping from headers
            var allSites = await sdkDbContext.Sites
                .Where(x => x.WorkflowState != Constants.WorkflowStates.Removed)
                .ToListAsync();

            // Columns are paired by header NAME, never by position: a stray or
            // reordered column used to shift every later worker onto a
            // neighbour's header, which then matched no site and was dropped
            // without a trace. Both sides of the match normalize the same way,
            // so a site called "Julius -" matches its "Julius - - timer" header.
            var layout = PlanTimerSheetColumns.Map(headerRows);
            var sitesByKey = allSites
                .Where(x => x.MicrotingUid != null)
                .ToLookup(x => PlanTimerSheetColumns.NormalizeName(x.Name));
            var assignedSites = await dbContext.AssignedSites
                .AsNoTracking()
                .Where(x => x.WorkflowState != Constants.WorkflowStates.Removed)
                .ToListAsync();
            var importingSiteIds = assignedSites
                .Where(x => x.UseGoogleSheetAsDefault && !x.Resigned)
                .Select(x => x.SiteId)
                .ToHashSet();

            var workers = new List<(PlanTimerSheetColumns.WorkerColumns Columns,
                Microting.eForm.Infrastructure.Data.Entities.Site Site)>();
            var problems = new List<string>(layout.Problems);
            foreach (var columns in layout.Workers)
            {
                var candidates = sitesByKey[columns.Key].ToList();
                if (candidates.Count == 0)
                {
                    problems.Add(
                        $"column {PlanTimerSheetColumns.ColumnLetter(columns.HoursColumn ?? columns.TextColumn!.Value)} \"{columns.Name}\" matches no site");
                    continue;
                }

                // This leg loads every site that has a column, not just the ones
                // set to import from the sheet, so a name collision still picks
                // one site rather than skipping both -- the site actually
                // importing wins, and the collision is reported.
                var site = candidates.FirstOrDefault(x => importingSiteIds.Contains(x.MicrotingUid!.Value))
                           ?? candidates[0];
                if (candidates.Count > 1)
                {
                    problems.Add(
                        $"column \"{columns.Name}\" matches {candidates.Count} sites ({string.Join(", ", candidates.Select(x => x.Name))}); \"{site.Name}\" is used");
                }

                if (columns.HoursColumn == null)
                {
                    problems.Add($"\"{columns.Name}\" has no \"- timer\" column; hours are left unchanged");
                }
                else if (columns.TextColumn == null)
                {
                    problems.Add($"\"{columns.Name}\" has no \"- tekst\" column; text is left unchanged");
                }

                workers.Add((columns, site));
            }

            foreach (var problem in problems)
            {
                Console.WriteLine($"[PullEverythingFromGoogleSheet] warn: PlanTimer sheet: {problem}");
                SentrySdk.CaptureMessage($"PlanTimer sheet: {problem}", SentryLevel.Warning);
            }

            // ONE timeline per mapped site, built BEFORE the row loop and never
            // per row. The update leg below may only clear a row's seconds
            // columns once it knows the row ran in five-minute mode — see the
            // INVERTED-SUMFLEX-SIGN note there for why clearing a one-minute
            // row here would be actively harmful.
            var oneMinuteTimelines = new Dictionary<int, OneMinuteModeTimeline>();
            foreach (var (_, mappedSite) in workers)
            {
                // A site without a MicrotingUid cannot be resolved; skip it here
                // rather than throwing, and let the lookup below fall through to
                // "mode unknown" (which does NOT clear).
                if (mappedSite.MicrotingUid == null
                    || oneMinuteTimelines.ContainsKey(mappedSite.MicrotingUid.Value))
                {
                    continue;
                }

                var mappedSiteUid = mappedSite.MicrotingUid.Value;

                var mappedAssignedSite = assignedSites.FirstOrDefault(x => x.SiteId == mappedSiteUid);
                oneMinuteTimelines[mappedSiteUid] =
                    await OneMinuteModeTimeline.BuildAsync(dbContext, mappedAssignedSite);
            }

            // This is a bulk re-sync over the sheet's whole history, so a locked
            // day is skipped, never rejected: frozen means frozen. ONE query for
            // every mapped site, built before the row loop and never per row.
            // The timeline keys ARE the mapped sites: one per distinct non-null
            // MicrotingUid in `workers`, and `workers` only ever holds sites with
            // a MicrotingUid (sitesByKey filters them), so every site the row loop
            // can reach has an entry here. That matters: DayLockHelper.IsLocked
            // treats a site missing from the map as having no boundary, i.e. open.
            var lockedThroughBySite = await DayLockHelper.LockedThroughForSitesAsync(
                dbContext, oneMinuteTimelines.Keys.ToList());
            // Observability only: the skips are silent otherwise.
            var lockedDaysSkipped = 0;
            var adminChangedSkipped = 0;

            // Skip the header row (first row)
            for (var i = 1; i < values.Count; i++)
            {
                var row = values[i];
                // Process each row
                string date = row[0].ToString();

                // Parse date and validate
                if (!DateTime.TryParseExact(date, "dd.MM.yyyy", CultureInfo.InvariantCulture,
                        DateTimeStyles.None, out var dateValue))
                {
                    continue;
                }

                Console.WriteLine($"Processing date: {dateValue}");

                if (dateValue > DateTime.Now.AddDays(180))
                {
                    continue;
                }

                foreach (var (columns, site) in workers)
                {
                    // The Sheets API drops trailing empty cells, so a short row
                    // simply does not reach this worker's columns. The old stride
                    // stopped at row.Count and left such a worker's day alone;
                    // treating those columns as empty instead would blank a
                    // planned day and create rows across the sheet's whole history.
                    // A worker whose tekst column precedes their timer column is
                    // still processed when the row ends between the two, and reads
                    // the missing hours cell as 0 -- the cell really is empty, and
                    // the old stride could not reach that layout at all.
                    if ((columns.HoursColumn ?? int.MaxValue) >= row.Count
                        && (columns.TextColumn ?? int.MaxValue) >= row.Count)
                    {
                        continue;
                    }

                    // Decided before the row is even loaded, so a locked row is
                    // never tracked and no later save in this loop can flush it.
                    if (site.MicrotingUid is { } lockSiteUid
                        && DayLockHelper.IsLocked(lockedThroughBySite, lockSiteUid, dateValue))
                    {
                        lockedDaysSkipped++;
                        continue;
                    }

                    Console.WriteLine($"Processing site: {site.Name}");

                    // null leaves the field untouched: the sheet has no such
                    // column for this worker, or the hours cell is not a number.
                    var planText = columns.TextColumn == null
                        ? null
                        : PlanTimerSheetColumns.CellAt(row, columns.TextColumn);
                    double? parsedPlanHours = null;
                    if (columns.HoursColumn != null)
                    {
                        var planHours = PlanTimerSheetColumns.CellAt(row, columns.HoursColumn);
                        parsedPlanHours = PlanTimerSheetColumns.ParseHours(planHours);
                        if (parsedPlanHours == null)
                        {
                            Console.WriteLine(
                                $"[PullEverythingFromGoogleSheet] warn: hours \"{planHours.Trim()}\" for site: {site.Name} and date: {dateValue} is not a number; hours left unchanged");
                        }
                    }

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
                            PlanText = planText ?? string.Empty,
                            PlanHours = parsedPlanHours ?? 0,
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

                        PlanTextHelper.ParsePlanText(planRegistration);

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
                        // An admin edited this day in the app, so the sheet does not
                        // win it back. ParsePlanText below re-derives PlanHours and
                        // every shift field from the text, so the row is left alone
                        // entirely rather than having one assignment guarded. Its
                        // flex chain is deliberately left as the app wrote it; the
                        // next row still seeds from this row's stored SumFlexEnd.
                        if (planRegistration.PlanChangedByAdmin)
                        {
                            adminChangedSkipped++;
                            continue;
                        }

                        if (planText != null)
                        {
                            // print to console if the current PlanText is different from the one in the database
                            if (planRegistration.PlanText != planText)
                            {
                                Console.WriteLine(
                                    $"PlanText for site: {site.Name} and date: {dateValue} has changed from {planRegistration.PlanText} to {planText}");
                            }

                            planRegistration.PlanText = planText;
                        }

                        // print to console if the current PlanHours is different from the one in the database
                        if (parsedPlanHours is { } newPlanHours)
                        {
                            if (planRegistration.PlanHours != newPlanHours)
                            {
                                Console.WriteLine(
                                    $"PlanHours for site: {site.Name} and date: {dateValue} has changed from {planRegistration.PlanHours} to {newPlanHours}");
                            }

                            planRegistration.PlanHours = newPlanHours;
                        }

                        planRegistration.UpdatedByUserId = 1;

                        PlanTextHelper.ParsePlanText(planRegistration);

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

                        // KNOWN BUG, UNFIXED AND OUT OF SCOPE HERE — search tag:
                        // INVERTED-SUMFLEX-SIGN.
                        // This leg computes
                        //     SumFlexEnd = SumFlexStart + PlanHours - NettoHours - PaiedOutFlex
                        // where the canonical chain (PlanRegistrationHelper
                        // .ApplyNettoFlexChainDecimal / ...SecondPrecision) is
                        //     SumFlexEnd = SumFlexStart + NettoHours - PlanHours - PaiedOutFlex
                        // — the NettoHours/PlanHours operands are the wrong way
                        // round, so the balance moves the wrong direction on any
                        // day where the two differ. Note its own Flex line just
                        // above uses the CORRECT order, so Flex and SumFlexEnd
                        // disagree with each other on the same row. The identical
                        // inversion exists in TimePlanningWorkingHoursService
                        // .Import's update leg. Deliberately NOT fixed in this
                        // change (which only alters which *InSeconds columns get
                        // written); fixing it restates historical balances and
                        // needs its own change, review and rollback path.
                        //
                        // What IS new here: this leg rewrites an EXISTING row's
                        // decimal balance, so a five-minute row's seconds columns
                        // must not keep an earlier one-minute write's value — the
                        // next row would seed its whole chain from it.
                        //
                        // The clear is MODE-GATED, and that gate is load-bearing
                        // BECAUSE of the inversion above: zeroing a genuine
                        // one-minute row's seconds would make every reader fall
                        // back to the decimal this leg just wrote with the wrong
                        // sign. A one-minute row keeps its seconds untouched here.
                        // Unresolvable mode => leave the row exactly as it was.
                        // Not clearing preserves the pre-existing behaviour;
                        // clearing a one-minute row would not.
                        if (site.MicrotingUid != null
                            && oneMinuteTimelines.TryGetValue(
                                site.MicrotingUid.Value, out var siteTimeline)
                            && !siteTimeline.WasOneMinuteForRow(planRegistration))
                        {
                            FlexChain.ClearSumFlexSeconds(planRegistration);
                        }

                        await planRegistration.Update(dbContext);
                    }
                }
            }

            Console.WriteLine(
                $"[PullEverythingFromGoogleSheet] summary: {workers.Count} worker(s) mapped, {problems.Count} sheet problem(s), {adminChangedSkipped} day(s) skipped because an admin changed them, {lockedDaysSkipped} day(s) skipped because they are locked.");
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
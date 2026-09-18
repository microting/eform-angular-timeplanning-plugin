using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;
using Microting.eForm.Infrastructure;
using Microting.eForm.Infrastructure.Constants;
using Microting.eFormApi.BasePn.Abstractions;
using Microting.eFormApi.BasePn.Infrastructure.Database.Entities;
using Microting.eFormApi.BasePn.Infrastructure.Helpers.PluginDbOptions;
using Microting.EformAngularFrontendBase.Infrastructure.Data;
using NSubstitute;
using NUnit.Framework;
using TimePlanning.Pn.Infrastructure.Helpers;
using TimePlanning.Pn.Infrastructure.Models.Settings;
using TimePlanning.Pn.Infrastructure.Models.WorkingHours.Index;
using TimePlanning.Pn.Services.TimePlanningLocalizationService;
using TimePlanning.Pn.Services.TimePlanningPlanningService;
using TimePlanning.Pn.Services.TimePlanningWorkingHoursService;
using AssignedSiteEntity = Microting.TimePlanningBase.Infrastructure.Data.Entities.AssignedSite;
using AssignedSiteManagingTagEntity = Microting.TimePlanningBase.Infrastructure.Data.Entities.AssignedSiteManagingTag;
using PlanRegistrationEntity = Microting.TimePlanningBase.Infrastructure.Data.Entities.PlanRegistration;
using SdkLanguage = Microting.eForm.Infrastructure.Data.Entities.Language;
using SdkSite = Microting.eForm.Infrastructure.Data.Entities.Site;
using SdkSiteTag = Microting.eForm.Infrastructure.Data.Entities.SiteTag;
using SdkSiteWorker = Microting.eForm.Infrastructure.Data.Entities.SiteWorker;
using SdkTag = Microting.eForm.Infrastructure.Data.Entities.Tag;
using SdkWorker = Microting.eForm.Infrastructure.Data.Entities.Worker;

namespace TimePlanning.Pn.Test;

/// <summary>
/// Coverage for making the "all workers" Excel export filterable by SDK site
/// tags, and for the site-to-tag lookup the export dialog uses to count how many
/// workers a tag selection covers.
///
/// Two contracts are under test, and they are the same contract seen from two
/// ends: the export's tag filter and the dialog's count must always agree, or
/// the count stops predicting the file. So a site is in when it carries ANY of
/// the selected tags; an EMPTY selection means no filter at all; a soft-deleted
/// Tag counts for neither; the caller's own site scoping applies to both; and
/// one site is one row no matter how many AssignedSite rows it has.
/// </summary>
[TestFixture]
public class ExportTagFilterAndSiteTagsTests : TestBaseSetup
{
    private TimePlanningWorkingHoursService _workingHoursService = null!;
    private IUserService _userService = null!;
    private IEFormCoreService _coreService = null!;
    private ITimePlanningLocalizationService _localizationService = null!;
    private IPluginDbOptions<TimePlanningBaseSettings> _options = null!;
    private ITimePlanningDbContextHelper _dbContextHelper = null!;

    [SetUp]
    public async Task SetUpTest()
    {
        await base.Setup();

        _userService = Substitute.For<IUserService>();
        _userService.UserId.Returns(1);

        _localizationService = Substitute.For<ITimePlanningLocalizationService>();
        _localizationService.GetString(Arg.Any<string>()).Returns(x => x[0]?.ToString());

        _coreService = Substitute.For<IEFormCoreService>();
        var core = await GetCore();
        _coreService.GetCore().Returns(core);

        var sdkDb = core.DbContextHelper.GetDbContext();
        var language = await sdkDb.Languages.FirstOrDefaultAsync(l => l.LanguageCode == "da");
        if (language == null)
        {
            language = new SdkLanguage { LanguageCode = "da", Name = "Danish" };
            await language.Create(sdkDb);
        }
        _userService.GetCurrentUserLanguage().Returns(language);

        _dbContextHelper = Substitute.For<ITimePlanningDbContextHelper>();
        _dbContextHelper.GetDbContext().Returns(TimePlanningPnDbContext);

        _options = Substitute.For<IPluginDbOptions<TimePlanningBaseSettings>>();
        _options.Value.Returns(new TimePlanningBaseSettings
        {
            AutoBreakCalculationActive = "0",
            DayOfPayment = 20,
            GpsEnabled = "0",
            SnapshotEnabled = "0"
        });

        // Both services scope their result to the signed-in caller, so every
        // test needs one. The default is an admin, for whom scoping is a no-op;
        // the scoping tests below seed a second, narrower caller into the same
        // context and re-point the substitute at it.
        var adminUserId = await GetBaseDbContextWithAdminAsync();
        _userService.GetCurrentUserAsync().Returns(new EformUser { Id = adminUserId });

        _workingHoursService = new TimePlanningWorkingHoursService(
            Substitute.For<ILogger<TimePlanningWorkingHoursService>>(),
            TimePlanningPnDbContext!,
            _userService,
            _localizationService,
            SeededBaseDbContext!,
            _options,
            _coreService);
    }

    // ------------------------------------------------------------------
    // 0. The integration seam: the client sends the tag selection as
    //    repeated query keys, and the action takes a plain complex type.
    // ------------------------------------------------------------------

    /// <summary>
    /// GenerateReportFileByAllWorkers is a GET whose parameter is a plain
    /// complex type with no [FromQuery], so MVC binds it from the value
    /// providers — the query string. This runs the real
    /// ComplexObjectModelBinder over the real model type to pin the part that
    /// is easy to get wrong: that `tagIds=1&amp;tagIds=2` becomes a two-element
    /// List&lt;int&gt;, and that omitting the key leaves an EMPTY list rather
    /// than null. A filter that silently failed to bind would export every
    /// worker, which is the worst available failure for an export.
    /// </summary>
    /// <remarks>
    /// That the query string is consulted at all (rather than the body) follows
    /// from there being no [ApiController] attribute and no MVC convention
    /// overriding binding sources anywhere in the plugin, the host app or
    /// BasePn — and from dateFrom/dateTo already binding this way in
    /// production today.
    /// </remarks>
    [TestCase("1", "2", new[] { 1, 2 }, Description = "repeated keys: tagIds=1&tagIds=2")]
    [TestCase("7", null, new[] { 7 }, Description = "one key: tagIds=7")]
    public void TagIdsQueryKeys_BindIntoTheAllWorkersRequestModel(
        string first, string? second, int[] expected)
    {
        var values = second == null ? new StringValues(first) : new StringValues([first, second]);
        var model = BindAllWorkersRequest(new Dictionary<string, StringValues>(StringComparer.OrdinalIgnoreCase)
        {
            ["dateFrom"] = "2026-07-01",
            ["dateTo"] = "2026-07-31",
            ["tagIds"] = values,
        });

        Assert.That(model.TagIds, Is.EqualTo(expected),
            "tagIds query keys must bind into List<int> TagIds");
        Assert.That(model.DateFrom, Is.EqualTo(new DateTime(2026, 7, 1)),
            "The dates must still bind alongside the new collection property");
    }

    [Test]
    public void OmittedTagIds_BindsToAnEmptyList_NotNull()
    {
        var model = BindAllWorkersRequest(new Dictionary<string, StringValues>(StringComparer.OrdinalIgnoreCase)
        {
            ["dateFrom"] = "2026-07-01",
            ["dateTo"] = "2026-07-31",
        });

        Assert.That(model.TagIds, Is.Not.Null,
            "The client omits tagIds when nothing is picked; the service reads .Count on it");
        Assert.That(model.TagIds, Is.Empty, "Omitting tagIds must mean 'no filter', not 'no sites'");
    }

    /// <summary>Runs the real MVC binder over the real request model. The
    /// dictionary is deliberately case-insensitive, as a genuine
    /// HttpRequest.Query is — a case-sensitive one silently binds nothing.</summary>
    private static TimePlanningWorkingHoursReportForAllWorkersRequestModel BindAllWorkersRequest(
        Dictionary<string, StringValues> query)
    {
        var provider = new ServiceCollection()
            .AddLogging()
            .AddMvcCore()
            .Services
            .BuildServiceProvider();

        var metadata = provider.GetRequiredService<IModelMetadataProvider>()
            .GetMetadataForType(typeof(TimePlanningWorkingHoursReportForAllWorkersRequestModel));
        var binder = provider.GetRequiredService<IModelBinderFactory>()
            .CreateBinder(new ModelBinderFactoryContext { Metadata = metadata });

        var bindingContext = DefaultModelBindingContext.CreateBindingContext(
            new ActionContext(new DefaultHttpContext(), new RouteData(), new ActionDescriptor()),
            new QueryStringValueProvider(BindingSource.Query, new QueryCollection(query), CultureInfo.InvariantCulture),
            metadata,
            bindingInfo: null,
            modelName: string.Empty);

        binder.BindModelAsync(bindingContext).GetAwaiter().GetResult();

        Assert.That(bindingContext.Result.IsModelSet, Is.True);
        return (TimePlanningWorkingHoursReportForAllWorkersRequestModel)bindingContext.Result.Model!;
    }

    // ------------------------------------------------------------------
    // 1. All-workers export: the tag selection decides which sites are in.
    // ------------------------------------------------------------------

    [Test]
    public async Task AllWorkersExport_TagIdsSelected_IncludesOnlySitesCarryingASelectedTag()
    {
        var date = new DateTime(2026, 7, 20);
        await SeedSiteAndPlanRegistration(siteUid: 9631, employeeNo: "1", date: date);
        await SeedSiteAndPlanRegistration(siteUid: 9632, employeeNo: "2", date: date);
        var elTagId = await TagSiteByUid(9631, "EL");
        await TagSiteByUid(9632, "Brand");

        var sheetNames = await ExportSheetNames(date, [elTagId]);

        Assert.That(sheetNames, Does.Contain("Site 9631"),
            "The site carrying the selected tag must be in the export");
        Assert.That(sheetNames, Does.Not.Contain("Site 9632"),
            "A site carrying only a non-selected tag must be filtered out");
    }

    [Test]
    public async Task AllWorkersExport_EmptyTagIds_IncludesEverySite()
    {
        var date = new DateTime(2026, 7, 21);
        await SeedSiteAndPlanRegistration(siteUid: 9633, employeeNo: "1", date: date);
        await SeedSiteAndPlanRegistration(siteUid: 9634, employeeNo: "2", date: date);
        await TagSiteByUid(9633, "EL");
        // Site 9634 stays untagged: an unfiltered export must not silently
        // start excluding sites that have no tags at all.

        var sheetNames = await ExportSheetNames(date, []);

        Assert.That(sheetNames, Does.Contain("Site 9633"));
        Assert.That(sheetNames, Does.Contain("Site 9634"),
            "An empty tag selection means no filter — every site stays in, tagged or not");
    }

    [Test]
    public async Task AllWorkersExport_SiteWithSeveralTags_MatchesWhenAnyOneIsSelected()
    {
        var date = new DateTime(2026, 7, 22);
        await SeedSiteAndPlanRegistration(siteUid: 9635, employeeNo: "1", date: date);
        await SeedSiteAndPlanRegistration(siteUid: 9636, employeeNo: "2", date: date);
        await TagSiteByUid(9635, "EL");
        var brandTagId = await TagSiteByUid(9635, "Brand");
        await TagSiteByUid(9636, "VVS");

        var sheetNames = await ExportSheetNames(date, [brandTagId]);

        Assert.That(sheetNames, Does.Contain("Site 9635"),
            "A multi-tagged site must match on ANY one of its tags, not only on all of them");
        Assert.That(sheetNames, Does.Not.Contain("Site 9636"));
    }

    /// <summary>
    /// The export filter is deliberately stricter than the planning grid's
    /// filter it was modelled on: it checks the Tag's workflow state too. Both
    /// ends of this feature must agree about a soft-deleted Tag — the dialog's
    /// count comes from GetSiteTags, which excludes it (asserted below), so the
    /// export must exclude it as well or the count stops predicting the file.
    /// </summary>
    [Test]
    public async Task AllWorkersExport_SoftDeletedTag_DoesNotPullItsSiteIntoTheExport()
    {
        var date = new DateTime(2026, 7, 23);
        await SeedSiteAndPlanRegistration(siteUid: 9637, employeeNo: "1", date: date);
        await SeedSiteAndPlanRegistration(siteUid: 9638, employeeNo: "2", date: date);

        // The SiteTag row survives; only the Tag itself is soft-deleted.
        var (deadTagId, _) = await TagSiteRaw(9637, "Nedlagt");
        await SoftDeleteTagAsync(deadTagId);
        var liveTagId = await TagSiteByUid(9638, "EL");

        // The dead tag id IS in the selection: that is what makes this test
        // discriminate on the Tag.WorkflowState predicate rather than merely on
        // the tag not being selected.
        var sheetNames = await ExportSheetNames(date, [deadTagId, liveTagId]);

        Assert.That(sheetNames, Does.Not.Contain("Site 9637"),
            "A soft-deleted Tag must not pull its site into the export, even though the SiteTag row survives");
        Assert.That(sheetNames, Does.Contain("Site 9638"));
    }

    /// <summary>
    /// The export is scoped to the caller by the same rule as the planning board
    /// and the dialog's count. Before this, a manager who saw three workers on
    /// the page downloaded a workbook containing the whole organisation.
    /// </summary>
    [Test]
    public async Task AllWorkersExport_ManagerUser_ContainsOnlyTheirScopedSites()
    {
        const string email = "manager9681@example.com";
        var date = new DateTime(2026, 7, 24);
        var managerAssignedSite = await SeedSiteAndPlanRegistration(
            siteUid: 9681, employeeNo: "1", date: date, email: email, isManager: true);
        await SeedSiteAndPlanRegistration(siteUid: 9682, employeeNo: "2", date: date);
        await SeedSiteAndPlanRegistration(siteUid: 9683, employeeNo: "3", date: date);

        // The manager manages "EL": site 9682 carries it, 9683 does not.
        var elTagId = await TagSiteByUid(9682, "EL");
        await TagSiteByUid(9683, "Brand");
        await new AssignedSiteManagingTagEntity
        {
            AssignedSiteId = managerAssignedSite.Id,
            TagId = elTagId,
            WorkflowState = Constants.WorkflowStates.Created,
            CreatedByUserId = 1,
            UpdatedByUserId = 1,
        }.Create(TimePlanningPnDbContext!);

        await SeedNonAdminCallerAsync(email);

        // No tag filter: the scoping alone must narrow the workbook.
        var sheetNames = await ExportSheetNames(date, []);

        Assert.That(sheetNames, Does.Contain("Site 9681"), "A manager's own site must be in their export");
        Assert.That(sheetNames, Does.Contain("Site 9682"), "A site in a tag the manager manages must be in");
        Assert.That(sheetNames, Does.Not.Contain("Site 9683"),
            "A site outside the manager's tags must NOT be in their export, as it is not on their page");
    }

    /// <summary>
    /// The counterpart, and the one that decides whether scoping is safe to
    /// ship: an admin's export must still contain every site.
    /// </summary>
    [Test]
    public async Task AllWorkersExport_AdminUser_StillContainsEverySite()
    {
        var date = new DateTime(2026, 7, 25);
        await SeedSiteAndPlanRegistration(siteUid: 9691, employeeNo: "1", date: date);
        await SeedSiteAndPlanRegistration(siteUid: 9692, employeeNo: "2", date: date);
        // A manager elsewhere in the system must not narrow an ADMIN's export.
        await SeedSiteAndPlanRegistration(
            siteUid: 9693, employeeNo: "3", date: date, email: "other@example.com", isManager: true);

        // The caller stays the admin seeded in SetUp.
        var sheetNames = await ExportSheetNames(date, []);

        Assert.That(sheetNames, Does.Contain("Site 9691"));
        Assert.That(sheetNames, Does.Contain("Site 9692"));
        Assert.That(sheetNames, Does.Contain("Site 9693"),
            "Scoping must be a no-op for an admin — every site stays in the workbook");
    }

    // ------------------------------------------------------------------
    // 2. site-tags lookup: one row per SiteId, scoped to the caller.
    // ------------------------------------------------------------------

    [Test]
    public async Task GetSiteTags_ReturnsTagIdsPerAssignedSite_WithResignedFlag()
    {
        // One site per case, so a failure names the rule that broke.
        foreach (var uid in new[] { 9641, 9642, 9643, 9644, 9645, 9646 })
        {
            await CreateSdkSite(uid);
        }

        await CreateAssignedSite(9641, resigned: false);
        await CreateAssignedSite(9642, resigned: true);
        await CreateAssignedSite(9643, resigned: false);
        await CreateAssignedSite(9645, resigned: false);
        await CreateAssignedSite(9646, resigned: false);
        var removedAssignedSite = await CreateAssignedSite(9644, resigned: false);
        await removedAssignedSite.Delete(TimePlanningPnDbContext!);

        var elTagId = await TagSiteByUid(9641, "EL");
        var brandTagId = await TagSiteByUid(9641, "Brand");
        var vvsTagId = await TagSiteByUid(9642, "VVS");
        // 9643 stays untagged. 9644 is absent for its own reason (removed
        // AssignedSite) and needs no tag — it previously carried a removed
        // SiteTag that proved nothing, because 9644 never reaches the result.
        // 9645: the SiteTag row survives, the Tag is soft-deleted.
        var (deadTagId, _) = await TagSiteRaw(9645, "Nedlagt");
        await SoftDeleteTagAsync(deadTagId);
        // 9646: the mirror image — the Tag lives, the SiteTag row is removed.
        var (_, removedSiteTagId) = await TagSiteRaw(9646, "Afkoblet");
        await SoftDeleteSiteTagAsync(removedSiteTagId);

        var service = BuildPlanningService();

        var result = await service.GetSiteTags();

        Assert.That(result.Success, Is.True, result.Message);
        var rows = result.Model!;
        Assert.That(rows.Select(x => x.SiteId), Is.EquivalentTo(new[] { 9641, 9642, 9643, 9645, 9646 }),
            "One row per non-removed AssignedSite — removed rows must not be listed");

        var tagged = rows.Single(x => x.SiteId == 9641);
        Assert.That(tagged.TagIds, Is.EquivalentTo(new[] { elTagId, brandTagId }));
        Assert.That(tagged.Resigned, Is.False);

        var resigned = rows.Single(x => x.SiteId == 9642);
        Assert.That(resigned.TagIds, Is.EquivalentTo(new[] { vvsTagId }));
        Assert.That(resigned.Resigned, Is.True,
            "Resigned must be reported so a count can drop workers the board hides by default");

        var untagged = rows.Single(x => x.SiteId == 9643);
        Assert.That(untagged.TagIds, Is.Empty, "An untagged site must come back with an empty list, not be omitted");

        var softDeletedTagOnly = rows.Single(x => x.SiteId == 9645);
        Assert.That(softDeletedTagOnly.TagIds, Is.Empty,
            "A soft-deleted Tag must not be reported, so the dialog's count matches the export's filter");

        var removedSiteTagOnly = rows.Single(x => x.SiteId == 9646);
        Assert.That(removedSiteTagOnly.TagIds, Is.Empty,
            "A removed SiteTag row must not be reported even though its Tag is still live");
    }

    /// <summary>
    /// A SiteId with two non-removed AssignedSite rows is still ONE worker. Per
    /// AssignedSite row, the dialog would count that worker twice and promise an
    /// export bigger than the one it produces (the export takes SiteIds
    /// Distinct()).
    /// </summary>
    [Test]
    public async Task GetSiteTags_DuplicateAssignedSiteRowsForOneSite_CollapseIntoOneRow()
    {
        await CreateSdkSite(9651);
        // Both rows resigned=false on purpose for the second site below; here the
        // duplicate pair disagrees, which is the case that decides the flag.
        await CreateAssignedSite(9651, resigned: true);
        await CreateAssignedSite(9651, resigned: false);

        var elTagId = await TagSiteByUid(9651, "EL");

        var service = BuildPlanningService();

        var result = await service.GetSiteTags();

        Assert.That(result.Success, Is.True, result.Message);
        var rows = result.Model!;
        Assert.That(rows.Count(x => x.SiteId == 9651), Is.EqualTo(1),
            "Two AssignedSite rows for one SiteId must collapse to a single row, or the worker is counted twice");

        var row = rows.Single(x => x.SiteId == 9651);
        Assert.That(row.TagIds, Is.EquivalentTo(new[] { elTagId }),
            "The merged row must still carry the site's tags");
        Assert.That(row.Resigned, Is.False,
            "A site with one active row is active — the export keeps it, so the count must too");
    }

    // ------------------------------------------------------------------
    // 3. Caller scoping: the count must never exceed what the page shows.
    // ------------------------------------------------------------------

    /// <summary>
    /// A non-admin whose own AssignedSite is not a manager sees only themselves
    /// on the planning board. Unscoped, the dialog would promise a whole-
    /// organisation export to a worker who can see exactly one row.
    /// </summary>
    [Test]
    public async Task GetSiteTags_PlainWorker_ReturnsOnlyTheirOwnSite()
    {
        const string email = "worker9661@example.com";
        await CreateSdkSite(9661);
        await CreateSdkSite(9662);
        await CreateAssignedSite(9661, resigned: false);
        await CreateAssignedSite(9662, resigned: false);
        await LinkWorkerToSite(9661, email, employeeNo: "1");
        await TagSiteByUid(9661, "EL");
        await TagSiteByUid(9662, "Brand");

        await SeedNonAdminCallerAsync(email);
        var service = BuildPlanningService();

        var result = await service.GetSiteTags();

        Assert.That(result.Success, Is.True, result.Message);
        Assert.That(result.Model!.Select(x => x.SiteId), Is.EquivalentTo(new[] { 9661 }),
            "A non-manager worker must be scoped to their own site, exactly as the planning board scopes them");
    }

    /// <summary>
    /// A manager sees the sites carrying the tags they manage, plus their own —
    /// the same rule Index applies, from the same code.
    /// </summary>
    [Test]
    public async Task GetSiteTags_ManagerUser_ReturnsOnlySitesInTheirManagedTags()
    {
        const string email = "manager9671@example.com";
        await CreateSdkSite(9671);
        await CreateSdkSite(9672);
        await CreateSdkSite(9673);
        var managerAssignedSite = await CreateAssignedSite(9671, resigned: false, isManager: true);
        await CreateAssignedSite(9672, resigned: false);
        await CreateAssignedSite(9673, resigned: false);
        await LinkWorkerToSite(9671, email, employeeNo: "1");

        // The manager manages "EL": site 9672 carries it, 9673 does not.
        var elTagId = await TagSiteByUid(9672, "EL");
        await TagSiteByUid(9673, "Brand");
        await new AssignedSiteManagingTagEntity
        {
            AssignedSiteId = managerAssignedSite.Id,
            TagId = elTagId,
            WorkflowState = Constants.WorkflowStates.Created,
            CreatedByUserId = 1,
            UpdatedByUserId = 1,
        }.Create(TimePlanningPnDbContext!);

        await SeedNonAdminCallerAsync(email);
        var service = BuildPlanningService();

        var result = await service.GetSiteTags();

        Assert.That(result.Success, Is.True, result.Message);
        Assert.That(result.Model!.Select(x => x.SiteId), Is.EquivalentTo(new[] { 9671, 9672 }),
            "A manager sees the sites in their managed tags plus their own — and nothing else");
    }

    // ------------------------------------------------------------------
    // Helpers
    // ------------------------------------------------------------------

    /// <summary>Seeds a second, non-admin eform user into the fixture's
    /// BaseDbContext and points the IUserService substitute at it, so the next
    /// service call resolves that caller instead of the default admin. Whether
    /// they read as a plain worker or a manager is decided by their own
    /// AssignedSite.IsManager, not here.</summary>
    private async Task SeedNonAdminCallerAsync(string email)
    {
        var user = new EformUser
        {
            UserName = email,
            Email = email,
            FirstName = "Test",
            LastName = "User"
        };
        var baseDb = SeededBaseDbContext!;
        baseDb.Users.Add(user);
        await baseDb.SaveChangesAsync();

        _userService.UserId.Returns(user.Id);
        _userService.GetCurrentUserAsync().Returns(new EformUser { Id = user.Id });
    }

    private ITimePlanningPlanningService BuildPlanningService() =>
        new TimePlanningPlanningService(
            Substitute.For<ILogger<TimePlanningPlanningService>>(),
            _options,
            TimePlanningPnDbContext!,
            _dbContextHelper,
            _userService,
            _localizationService,
            SeededBaseDbContext!,
            _coreService);

    /// <summary>Runs the all-workers export for a single day and returns the
    /// names of the sheets it produced. Per-site tabs are named after the site,
    /// so the sheet list IS the list of sites that survived the tag filter.</summary>
    private async Task<List<string>> ExportSheetNames(DateTime date, List<int> tagIds)
    {
        var result = await _workingHoursService.GenerateExcelDashboard(
            new TimePlanningWorkingHoursReportForAllWorkersRequestModel
            {
                DateFrom = date,
                DateTo = date,
                TagIds = tagIds,
            });

        Assert.That(result.Success, Is.True, result.Message);
        Assert.That(result.Model, Is.Not.Null);

        try
        {
            result.Model!.Position = 0;
            using var doc = SpreadsheetDocument.Open(result.Model!, false);
            return doc.WorkbookPart!.Workbook.Descendants<Sheet>()
                .Select(s => s.Name!.Value!)
                .ToList();
        }
        finally
        {
            await result.Model!.DisposeAsync();
        }
    }

    private async Task<SdkSite> CreateSdkSite(int siteUid)
    {
        await using var sdkDb = await SdkDbContext();
        var site = new SdkSite { Name = $"Site {siteUid}", MicrotingUid = siteUid };
        await site.Create(sdkDb);
        return site;
    }

    private async Task<AssignedSiteEntity> CreateAssignedSite(
        int siteUid, bool resigned, bool isManager = false)
    {
        var assignedSite = new AssignedSiteEntity
        {
            SiteId = siteUid,
            UseOneMinuteIntervals = false,
            Resigned = resigned,
            IsManager = isManager,
            WorkflowState = Constants.WorkflowStates.Created,
            CreatedByUserId = 1,
            UpdatedByUserId = 1,
        };
        await assignedSite.Create(TimePlanningPnDbContext!);
        return assignedSite;
    }

    /// <summary>Creates the SDK Worker/SiteWorker pair that lets the service
    /// resolve a signed-in user (matched by email) to a site, and that the
    /// exports read the worker name and employee number from.</summary>
    private async Task LinkWorkerToSite(int siteUid, string email, string employeeNo)
    {
        await using var sdkDb = await SdkDbContext();
        var site = await sdkDb.Sites.FirstAsync(x => x.MicrotingUid == siteUid);

        var worker = new SdkWorker
        {
            FirstName = "Test",
            LastName = "Worker",
            Email = email,
            MicrotingUid = 3000 + siteUid,
            EmployeeNo = employeeNo,
        };
        await worker.Create(sdkDb);

        await new SdkSiteWorker
        {
            SiteId = site.Id,
            WorkerId = worker.Id,
            MicrotingUid = 4000 + siteUid,
        }.Create(sdkDb);
    }

    /// <summary>Tags the site with the given MicrotingUid and returns the Tag id
    /// — the value the API takes in <c>TagIds</c>.</summary>
    private async Task<int> TagSiteByUid(int siteUid, string tagName)
    {
        var (tagId, _) = await TagSiteRaw(siteUid, tagName);
        return tagId;
    }

    /// <summary>Creates a Tag and links it to the site; returns both IDs so a
    /// test can soft-delete either end of the edge independently. Deliberately
    /// returns ids rather than entities — see <see cref="SoftDeleteTagAsync"/>
    /// for why handing a caller a detached entity to delete is a trap.</summary>
    private async Task<(int TagId, int SiteTagId)> TagSiteRaw(int siteUid, string tagName)
    {
        await using var sdkDb = await SdkDbContext();
        var site = await sdkDb.Sites.FirstAsync(x => x.MicrotingUid == siteUid);
        var tag = new SdkTag { Name = tagName };
        await tag.Create(sdkDb);
        var siteTag = new SdkSiteTag { SiteId = site.Id, TagId = tag.Id };
        await siteTag.Create(sdkDb);
        return (tag.Id, siteTag.Id);
    }

    /// <summary>A NEW SDK context — DbContextHelper.GetDbContext() builds one
    /// per call, it never hands back a shared instance.</summary>
    private async Task<MicrotingDbContext> SdkDbContext() =>
        (await GetCore()).DbContextHelper.GetDbContext();

    /// <summary>
    /// Soft-deletes an SDK Tag, and proves the row actually reached "removed".
    /// </summary>
    /// <remarks>
    /// PnBase.Delete sets WorkflowState on the in-memory entity and then saves
    /// ONLY if the context it was handed reports ChangeTracker.HasChanges().
    /// Since every GetDbContext() call returns a fresh context, deleting an
    /// entity that a DIFFERENT context created is a SILENT no-op: the row stays
    /// live, no exception, and the test then asserts against a tag that was
    /// never removed. That is exactly what made these tests fail in CI. Reading
    /// the row back through the context we delete on guarantees it is tracked,
    /// and the verification read — from a separate context, so it cannot be
    /// answered out of the change tracker — turns any future regression into a
    /// failure at the seeding step rather than a misleading one at the assert.
    /// </remarks>
    private async Task SoftDeleteTagAsync(int tagId)
    {
        await using (var sdkDb = await SdkDbContext())
        {
            var tag = await sdkDb.Tags.FirstAsync(x => x.Id == tagId);
            await tag.Delete(sdkDb);
        }

        await using var verifyDb = await SdkDbContext();
        var persisted = await verifyDb.Tags.AsNoTracking().FirstAsync(x => x.Id == tagId);
        Assert.That(persisted.WorkflowState, Is.EqualTo(Constants.WorkflowStates.Removed),
            "Seeding precondition: the Tag must really be soft-deleted in the database");
    }

    /// <summary>Soft-deletes an SDK SiteTag row, leaving its Tag alone. Same
    /// tracking trap as <see cref="SoftDeleteTagAsync"/>.</summary>
    private async Task SoftDeleteSiteTagAsync(int siteTagId)
    {
        await using (var sdkDb = await SdkDbContext())
        {
            var siteTag = await sdkDb.SiteTags.FirstAsync(x => x.Id == siteTagId);
            await siteTag.Delete(sdkDb);
        }

        await using var verifyDb = await SdkDbContext();
        var persisted = await verifyDb.SiteTags.AsNoTracking().FirstAsync(x => x.Id == siteTagId);
        Assert.That(persisted.WorkflowState, Is.EqualTo(Constants.WorkflowStates.Removed),
            "Seeding precondition: the SiteTag must really be soft-deleted in the database");
    }

    /// <summary>Seeds SDK Site/Worker/SiteWorker + AssignedSite + a prior-day
    /// registration (dropped via the export's Skip(1)) + a registration on
    /// <paramref name="date"/>. Returns the AssignedSite so a caller can hang
    /// managing tags off it.</summary>
    private async Task<AssignedSiteEntity> SeedSiteAndPlanRegistration(
        int siteUid, string employeeNo, DateTime date, string? email = null, bool isManager = false)
    {
        await CreateSdkSite(siteUid);
        await LinkWorkerToSite(siteUid, email ?? $"test{siteUid}@example.com", employeeNo);
        var assignedSite = await CreateAssignedSite(siteUid, resigned: false, isManager: isManager);

        foreach (var d in new[] { date.AddDays(-1), date })
        {
            await new PlanRegistrationEntity
            {
                SdkSitId = siteUid,
                Date = d,
                Start1Id = d == date ? 97 : 0,
                Stop1Id = d == date ? 121 : 0,
                Pause1Id = 0,
                PlanText = "",
                CommentOffice = "",
                CommentOfficeAll = "",
                WorkflowState = Constants.WorkflowStates.Created,
                CreatedByUserId = 1,
                UpdatedByUserId = 1,
            }.Create(TimePlanningPnDbContext!);
        }

        return assignedSite;
    }
}

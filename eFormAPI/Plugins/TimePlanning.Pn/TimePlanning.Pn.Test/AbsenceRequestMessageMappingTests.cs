using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microting.eForm.Infrastructure.Constants;
using Microting.eFormApi.BasePn.Abstractions;
using Microting.TimePlanningBase.Infrastructure.Data.Entities;
using NSubstitute;
using NUnit.Framework;
using TimePlanning.Pn.Infrastructure.Models.AbsenceRequest;
using TimePlanning.Pn.Services.AbsenceRequestService;
using TimePlanning.Pn.Services.TimePlanningLocalizationService;

namespace TimePlanning.Pn.Test;

/// <summary>
/// Regression coverage for kanban #5568: approval mapped the absence type from
/// Message.Name using the plugin seed's spelling ("TimeOff", "Children1stSick").
/// The base code-first seed names the same ids "Time off", "Children's 1st sick",
/// so on base-seeded DBs Tidsoff and Barns 1./2. sygedag got OtherAllowedAbsence.
/// The mapping is now keyed on Message.Id; each case runs under both spellings.
/// </summary>
[TestFixture]
public class AbsenceRequestMessageMappingTests : TestBaseSetup
{
    private IAbsenceRequestService _service;

    public enum ExpectedFlag
    {
        OnVacation,
        Sick,
        OtherAllowedAbsence
    }

    [SetUp]
    public async Task SetUpTest()
    {
        await base.Setup();

        var userService = Substitute.For<IUserService>();
        userService.UserId.Returns(1);

        var localizationService = Substitute.For<ITimePlanningLocalizationService>();
        localizationService.GetString(Arg.Any<string>()).Returns(x => x[0]?.ToString());

        var coreService = Substitute.For<IEFormCoreService>();
        var core = await GetCore();
        coreService.GetCore().Returns(Task.FromResult(core));

        // Site 2 is the signed-in manager of the worker.
        await SeedManagerCallerAsync(userService, managerSiteUid: 2, workerSiteUid: 1);

        _service = new AbsenceRequestService(
            Substitute.For<Microsoft.Extensions.Logging.ILogger<AbsenceRequestService>>(),
            TimePlanningPnDbContext,
            userService,
            localizationService,
            coreService,
            SeededBaseDbContext!,
            Substitute.For<Microsoft.Extensions.DependencyInjection.IServiceScopeFactory>());
    }

    // Plugin seed spelling (TimePlanningSeedMessages)
    [TestCase(1, "DayOff", ExpectedFlag.OtherAllowedAbsence)]
    [TestCase(2, "Vacation", ExpectedFlag.OnVacation)]
    [TestCase(3, "Sick", ExpectedFlag.Sick)]
    [TestCase(4, "Course", ExpectedFlag.OtherAllowedAbsence)]
    [TestCase(5, "LeaveOfAbsence", ExpectedFlag.OtherAllowedAbsence)]
    [TestCase(7, "Children1stSick", ExpectedFlag.Sick)]
    [TestCase(8, "Children2ndSick", ExpectedFlag.Sick)]
    [TestCase(9, "TimeOff", ExpectedFlag.OnVacation)]
    [TestCase(10, "Maternity", ExpectedFlag.OtherAllowedAbsence)]
    [TestCase(11, "VacationDayOff", ExpectedFlag.OnVacation)]
    [TestCase(12, "Holiday", ExpectedFlag.OtherAllowedAbsence)]
    [TestCase(13, "PregnancyLeave", ExpectedFlag.OtherAllowedAbsence)]
    // Base code-first seed spelling (MessagesCodeFirst)
    [TestCase(1, "Day Off", ExpectedFlag.OtherAllowedAbsence)]
    [TestCase(2, "Vacation", ExpectedFlag.OnVacation)]
    [TestCase(3, "Sick", ExpectedFlag.Sick)]
    [TestCase(4, "Course", ExpectedFlag.OtherAllowedAbsence)]
    [TestCase(5, "Leave of absence", ExpectedFlag.OtherAllowedAbsence)]
    [TestCase(6, "Care", ExpectedFlag.OtherAllowedAbsence)]
    [TestCase(7, "Children's 1st sick", ExpectedFlag.Sick)]
    [TestCase(8, "Children's 2nd sick", ExpectedFlag.Sick)]
    [TestCase(9, "Time off", ExpectedFlag.OnVacation)]
    // Unknown id falls back to OtherAllowedAbsence
    [TestCase(99, "SomethingNew", ExpectedFlag.OtherAllowedAbsence)]
    public async Task ApproveAsync_MapsAbsenceFlagByMessageId_RegardlessOfName(
        int messageId, string messageName, ExpectedFlag expected)
    {
        await EnsureMessageNamed(messageId, messageName);

        const int workerSdkSitId = 1;
        var date = new DateTime(2024, 3, 1);

        var request = new AbsenceRequest
        {
            RequestedBySdkSitId = workerSdkSitId,
            DateFrom = date,
            DateTo = date,
            Status = AbsenceRequestStatus.Pending,
            RequestedAtUtc = DateTime.UtcNow,
            CreatedByUserId = 1,
            UpdatedByUserId = 1
        };
        await request.Create(TimePlanningPnDbContext);

        var day = new AbsenceRequestDay
        {
            AbsenceRequestId = request.Id,
            Date = date,
            MessageId = messageId,
            CreatedByUserId = 1,
            UpdatedByUserId = 1
        };
        await day.Create(TimePlanningPnDbContext);

        var result = await _service.ApproveAsync(request.Id,
            new AbsenceRequestDecisionModel { DecisionComment = "Approved" });
        Assert.That(result.Success, Is.True, result.Message);

        var pr = await TimePlanningPnDbContext.PlanRegistrations
            .AsNoTracking()
            .FirstAsync(x => x.SdkSitId == workerSdkSitId && x.Date == date
                             && x.WorkflowState != Constants.WorkflowStates.Removed);

        Assert.That(pr.MessageId, Is.EqualTo(messageId));
        Assert.That(pr.OnVacation, Is.EqualTo(expected == ExpectedFlag.OnVacation), "OnVacation");
        Assert.That(pr.Sick, Is.EqualTo(expected == ExpectedFlag.Sick), "Sick");
        Assert.That(pr.OtherAllowedAbsence, Is.EqualTo(expected == ExpectedFlag.OtherAllowedAbsence),
            "OtherAllowedAbsence");
        Assert.That(pr.AbsenceWithoutPermission, Is.False, "AbsenceWithoutPermission");
    }

    /// <summary>
    /// Renames the seeded message row to the given spelling, or inserts it when the
    /// plugin seed does not contain that id (e.g. base-only id 6, unknown ids).
    /// </summary>
    private async Task EnsureMessageNamed(int messageId, string messageName)
    {
        var message = await TimePlanningPnDbContext.Messages.FirstOrDefaultAsync(x => x.Id == messageId);
        if (message == null)
        {
            TimePlanningPnDbContext.Messages.Add(new Message(messageId, messageName, messageName, messageName, messageName));
        }
        else
        {
            message.Name = messageName;
        }
        await TimePlanningPnDbContext.SaveChangesAsync();
        TimePlanningPnDbContext.ChangeTracker.Clear();
    }
}

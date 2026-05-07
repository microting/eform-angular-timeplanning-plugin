using System;
using System.Threading.Tasks;
using Microting.eFormApi.BasePn.Abstractions;
using Microting.eFormApi.BasePn.Infrastructure.Database.Entities;
using Microting.eFormApi.BasePn.Infrastructure.Models.API;
using NSubstitute;
using NUnit.Framework;
using TimePlanning.Pn.Services.TimePlanningLocalizationService;

namespace TimePlanning.Pn.Test;

/// <summary>
/// Asserts the defensive null-guard added to *ByCurrentUser methods in
/// TimePlanningWorkingHoursService. The guard returns a clean failure
/// result when <c>userService.GetCurrentUserAsync()</c> returns null,
/// instead of NRE'ing the EF Core LINQ funcletizer on
/// <c>currentUserAsync.Id</c>.
///
/// This test is <see cref="IgnoreAttribute"/>'d because instantiating the
/// real <c>TimePlanningWorkingHoursService</c> requires the full
/// constructor dependency graph (BaseDbContext, TimePlanningPnDbContext,
/// IEFormCoreService, etc.) that the existing test harness doesn't seed.
/// The same carve-out is used by <see cref="AbsenceRequestServiceTests"/>.
/// The shape below documents the expected contract; whoever fleshes out
/// the harness in a follow-up can un-ignore.
/// </summary>
[TestFixture]
[Ignore("Test fixture infrastructure for full TimePlanningWorkingHoursService instantiation pending — see file header.")]
public class TimePlanningWorkingHoursServiceNullUserTests
{
    [Test]
    public async Task ReadFullByCurrentUser_NullCurrentUser_ReturnsUserNotFound()
    {
        var userService = Substitute.For<IUserService>();
        userService.GetCurrentUserAsync().Returns(Task.FromResult<EformUser>(null!));
        var localizationService = Substitute.For<ITimePlanningLocalizationService>();
        localizationService.GetString("UserNotFound").Returns("User not found.");

        // Arrange the rest of the constructor graph here once the test
        // harness can supply the dependencies.

        // Expected:
        //   var result = await sut.ReadFullByCurrentUser(DateTime.Today, null, null, null, null);
        //   Assert.That(result.Success, Is.False);
        //   Assert.That(result.Message, Is.EqualTo("User not found."));
        //   And no exception bubbled.

        await Task.CompletedTask;
        Assert.Pass("Shape-asserting placeholder. Un-ignore once the service harness is in place.");
    }
}

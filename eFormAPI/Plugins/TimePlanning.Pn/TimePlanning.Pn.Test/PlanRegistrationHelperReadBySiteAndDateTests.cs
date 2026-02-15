using System;
using System.Threading.Tasks;

using NUnit.Framework;
using TimePlanning.Pn.Infrastructure.Helpers;
using Microting.TimePlanningBase.Infrastructure.Data.Entities;

namespace TimePlanning.Pn.Test;

[TestFixture]
public class PlanRegistrationHelperReadBySiteAndDateTests : TestBaseSetup
{

    [TestCase("2025-05-17 00:00:00.000")]
    [TestCase("2025-05-18 00:00:00.000")]
    public async Task ReadBySiteAndDate_FindsEntry_ForEachHour(string dateString)
    {
        // Arrange
        int sdkSiteId = 1;
        var date = DateTime.Parse(dateString);
        var planRegistration = new PlanRegistration
        {
            Date = date,
            SdkSitId = sdkSiteId,
        };
        await planRegistration.Create(TimePlanningPnDbContext).ConfigureAwait(false);

        // Act
        var result = await PlanRegistrationHelper.ReadBySiteAndDate(TimePlanningPnDbContext, sdkSiteId, date, null);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(date, Is.EqualTo(result.Date));
        Assert.That(dateString, Is.EqualTo(result.Date.ToString("yyyy-MM-dd HH:mm:ss.fff")));
        Assert.That(sdkSiteId, Is.EqualTo(result.SdkSiteId));
    }


    [TestCase("2025-05-17 00:00:00.000")]
    [TestCase("2025-05-18 00:00:00.000")]
    public async Task ReadBySiteAndDate_ReturnsNewModel_WhenNoEntryFound(string dateString)
    {
        // Arrange
        int sdkSiteId = 2;
        var date = DateTime.Parse(dateString);

        // Act
        var result = await PlanRegistrationHelper.ReadBySiteAndDate(TimePlanningPnDbContext, sdkSiteId, date, null);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(date, Is.EqualTo(result.Date));
        Assert.That(sdkSiteId, Is.EqualTo(result.SdkSiteId));
        Assert.That(dateString, Is.EqualTo(result.Date.ToString("yyyy-MM-dd HH:mm:ss.fff")));
        Assert.That(result.PlanHours, Is.EqualTo(0));
        Assert.That(result.Shift1Start, Is.EqualTo(0));
        Assert.That(result.Shift1Stop, Is.EqualTo(0));
    }
}
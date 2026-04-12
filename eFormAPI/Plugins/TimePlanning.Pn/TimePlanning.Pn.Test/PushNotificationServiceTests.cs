using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microting.TimePlanningBase.Infrastructure.Data;
using NSubstitute;
using NUnit.Framework;
using TimePlanning.Pn.Services.PushNotificationService;

namespace TimePlanning.Pn.Test;

[TestFixture]
public class PushNotificationServiceTests
{
    [Test]
    public void Constructor_WithoutFirebaseConfig_DoesNotThrow()
    {
        var options = new DbContextOptionsBuilder<TimePlanningPnDbContext>()
            .UseInMemoryDatabase("PushNotifTest_ctor")
            .Options;
        var dbContext = new TimePlanningPnDbContext(options);

        Assert.DoesNotThrow(() =>
        {
            _ = new PushNotificationService(
                dbContext,
                Substitute.For<ILogger<PushNotificationService>>());
        });

        dbContext.Dispose();
    }

    [Test]
    public async Task SendToSiteAsync_WhenFirebaseNotConfigured_IsNoOp()
    {
        var options = new DbContextOptionsBuilder<TimePlanningPnDbContext>()
            .UseInMemoryDatabase("PushNotifTest_noop")
            .Options;
        var dbContext = new TimePlanningPnDbContext(options);
        var service = new PushNotificationService(
            dbContext,
            Substitute.For<ILogger<PushNotificationService>>());

        await service.SendToSiteAsync(1, "Title", "Body");

        dbContext.Dispose();
    }
}

using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NUnit.Framework;
using TimePlanning.Pn.Services.PushNotificationService;

namespace TimePlanning.Pn.Test;

[TestFixture]
public class PushNotificationServiceTests : TestBaseSetup
{
    [SetUp]
    public async Task SetUp()
    {
        await base.Setup();
    }

    [Test]
    public void Constructor_WithoutFirebaseConfig_DoesNotThrow()
    {
        Assert.DoesNotThrow(() =>
        {
            _ = new PushNotificationService(
                TimePlanningPnDbContext!,
                Substitute.For<ILogger<PushNotificationService>>());
        });
    }

    [Test]
    public async Task SendToSiteAsync_WhenFirebaseNotConfigured_IsNoOp()
    {
        var service = new PushNotificationService(
            TimePlanningPnDbContext!,
            Substitute.For<ILogger<PushNotificationService>>());

        await service.SendToSiteAsync(1, "Title", "Body");
    }
}

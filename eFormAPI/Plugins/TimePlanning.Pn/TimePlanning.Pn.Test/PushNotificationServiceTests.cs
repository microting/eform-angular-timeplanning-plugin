using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NUnit.Framework;
using TimePlanning.Pn.Infrastructure.Models.DeviceToken;
using TimePlanning.Pn.Services.PushNotificationService;

namespace TimePlanning.Pn.Test;

[TestFixture]
public class PushNotificationServiceTests
{
    [Test]
    public void Constructor_WithoutFirebaseConfig_DoesNotThrow()
    {
        // Arrange — empty configuration, no Firebase:ServiceAccountPath key
        var config = new ConfigurationBuilder().Build();
        var options = new DbContextOptionsBuilder<DeviceTokenDbContext>()
            .UseInMemoryDatabase("PushNotifTest_ctor")
            .Options;
        var dbContext = new DeviceTokenDbContext(options);

        // Act & Assert — construction must not throw
        Assert.DoesNotThrow(() =>
        {
            _ = new PushNotificationService(
                dbContext,
                config,
                Substitute.For<ILogger<PushNotificationService>>());
        });

        dbContext.Dispose();
    }

    [Test]
    public async Task SendToSiteAsync_WhenFirebaseNotConfigured_IsNoOp()
    {
        // Arrange
        var config = new ConfigurationBuilder().Build();
        var options = new DbContextOptionsBuilder<DeviceTokenDbContext>()
            .UseInMemoryDatabase("PushNotifTest_noop")
            .Options;
        var dbContext = new DeviceTokenDbContext(options);
        var service = new PushNotificationService(
            dbContext,
            config,
            Substitute.For<ILogger<PushNotificationService>>());

        // Act & Assert — should complete without throwing
        await service.SendToSiteAsync(1, "Title", "Body");

        dbContext.Dispose();
    }

    [Test]
    public void Constructor_WithNonExistentServiceAccountPath_DoesNotThrow()
    {
        // Arrange — path set but file does not exist
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new[]
            {
                new System.Collections.Generic.KeyValuePair<string, string?>(
                    "Firebase:ServiceAccountPath", "/tmp/does-not-exist-firebase.json")
            })
            .Build();
        var options = new DbContextOptionsBuilder<DeviceTokenDbContext>()
            .UseInMemoryDatabase("PushNotifTest_badpath")
            .Options;
        var dbContext = new DeviceTokenDbContext(options);

        Assert.DoesNotThrow(() =>
        {
            _ = new PushNotificationService(
                dbContext,
                config,
                Substitute.For<ILogger<PushNotificationService>>());
        });

        dbContext.Dispose();
    }
}

using System.Collections.Generic;
using System.Threading.Tasks;
using FirebaseAdmin.Messaging;
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

    [Test]
    public void BuildMessage_DataOnly_OmitsNotificationAndSetsContentAvailable()
    {
        var msg = PushNotificationService.BuildMessage(
            "tok",
            "",
            "",
            new Dictionary<string, string> { { "type", "settings_changed" } });

        Assert.That(msg.Notification, Is.Null,
            "a data-only push must not attach a visible Notification block");
        Assert.That(msg.Apns, Is.Not.Null);
        Assert.That(msg.Apns.Aps.ContentAvailable, Is.True,
            "iOS needs content-available to wake the app for a silent data push");
        Assert.That(msg.Data["type"], Is.EqualTo("settings_changed"));
    }

    [Test]
    public void BuildMessage_WithTitleOrBody_SetsNotificationBlock()
    {
        var msg = PushNotificationService.BuildMessage("tok", "Hello", "World", null);

        Assert.That(msg.Notification, Is.Not.Null);
        Assert.That(msg.Notification.Title, Is.EqualTo("Hello"));
        Assert.That(msg.Notification.Body, Is.EqualTo("World"));
    }
}

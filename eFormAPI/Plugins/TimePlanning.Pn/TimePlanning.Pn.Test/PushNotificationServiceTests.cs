using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FirebaseAdmin.Messaging;
using Microsoft.Extensions.Logging;
using Microting.TimePlanningBase.Infrastructure.Data.Entities;
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

    // Firebase is not configured in tests, so SendToSiteAsync short-circuits
    // before it queries. The token-selection query it delegates to is exercised
    // directly via the internal ResolveTargetTokensAsync seam.

    [Test]
    public async Task ResolveTargetTokens_WithMinBuild_ExcludesOlderBuildsAndIncludesAtOrAbove()
    {
        await SeedToken("old-0", sdkSiteId: 7, buildNumber: 0);
        await SeedToken("old-below", sdkSiteId: 7, buildNumber: 31220);
        await SeedToken("exact", sdkSiteId: 7, buildNumber: 31221);
        await SeedToken("newer", sdkSiteId: 7, buildNumber: 40000);
        await SeedToken("other-site", sdkSiteId: 8, buildNumber: 40000);

        var service = new PushNotificationService(
            TimePlanningPnDbContext!,
            Substitute.For<ILogger<PushNotificationService>>());

        var tokens = await service.ResolveTargetTokensAsync(7, minBuild: 31221);
        var picked = tokens.Select(t => t.Token).ToList();

        Assert.That(picked, Is.EquivalentTo(new[] { "exact", "newer" }),
            "only same-site tokens reporting build >= minBuild must be targeted");
    }

    [Test]
    public async Task ResolveTargetTokens_DefaultMinBuildZero_IncludesEveryDevice()
    {
        await SeedToken("legacy-0", sdkSiteId: 7, buildNumber: 0);
        await SeedToken("modern", sdkSiteId: 7, buildNumber: 40000);

        var service = new PushNotificationService(
            TimePlanningPnDbContext!,
            Substitute.For<ILogger<PushNotificationService>>());

        var tokens = await service.ResolveTargetTokensAsync(7, minBuild: 0);

        Assert.That(tokens.Select(t => t.Token),
            Is.EquivalentTo(new[] { "legacy-0", "modern" }),
            "minBuild 0 must keep existing callers unaffected (all devices included)");
    }

    private async Task SeedToken(string token, int sdkSiteId, int buildNumber)
    {
        var deviceToken = new DeviceToken
        {
            SdkSiteId = sdkSiteId,
            Token = token,
            Platform = "android",
            AppBuildNumber = buildNumber
        };
        await deviceToken.Create(TimePlanningPnDbContext!);
    }
}

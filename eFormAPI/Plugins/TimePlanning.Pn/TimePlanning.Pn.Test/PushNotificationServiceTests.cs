using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FirebaseAdmin.Messaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microting.eForm.Infrastructure.Constants;
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
        var service = CreateService();

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

        var service = CreateService();

        var tokens = await service.ResolveTargetTokensAsync(7, minBuild: 31221);
        var picked = tokens.Select(t => t.FcmToken).ToList();

        Assert.That(picked, Is.EquivalentTo(new[] { "exact", "newer" }),
            "only same-site tokens reporting build >= minBuild must be targeted");
    }

    [Test]
    public async Task ResolveTargetTokens_DefaultMinBuildZero_IncludesEveryDevice()
    {
        await SeedToken("legacy-0", sdkSiteId: 7, buildNumber: 0);
        await SeedToken("modern", sdkSiteId: 7, buildNumber: 40000);

        var service = CreateService();

        var tokens = await service.ResolveTargetTokensAsync(7, minBuild: 0);

        Assert.That(tokens.Select(t => t.FcmToken),
            Is.EquivalentTo(new[] { "legacy-0", "modern" }),
            "minBuild 0 must keep existing callers unaffected (all devices included)");
    }

    [Test]
    public async Task ResolveTargetTokens_ExcludesSoftDeletedTokens()
    {
        await SeedToken("live", sdkSiteId: 9, buildNumber: 0);
        var dead = await SeedToken("dead", sdkSiteId: 9, buildNumber: 0);
        await dead.Delete(TimePlanningPnDbContext!);

        var tokens = await CreateService().ResolveTargetTokensAsync(9, minBuild: 0);

        Assert.That(tokens.Select(t => t.FcmToken), Is.EquivalentTo(new[] { "live" }));
    }

    // The DeviceTokens table is shared with no other app in this database
    // today, but the entity and its indexes are shared with
    // BackendConfiguration's. A foreign-app token belongs to a different
    // Firebase project: sending to it returns SENDER_ID_MISMATCH.
    [Test]
    public async Task ResolveTargetTokens_ExcludesForeignAppTokens()
    {
        await SeedToken("mine", sdkSiteId: 400, buildNumber: 0);
        await SeedToken("theirs", sdkSiteId: 400, buildNumber: 0, appId: "adhoc");

        var tokens = await CreateService().ResolveTargetTokensAsync(400, minBuild: 0);

        Assert.That(tokens.Select(t => t.FcmToken), Is.EquivalentTo(new[] { "mine" }),
            "the time sender must only ever see tokens minted by the time app");
    }

    // SENDER_ID_MISMATCH has two causes. A single mismatching token among
    // healthy ones is a foreign token and is pruned. EVERY targeted token
    // mismatching means this sender is holding the wrong Firebase credential,
    // and pruning would silently wipe the tenant's whole token set.

    [Test]
    public async Task PruneSenderIdMismatches_MixedResults_PrunesOnlyTheMismatchingToken()
    {
        var healthy = await SeedToken("healthy", sdkSiteId: 500, buildNumber: 0);
        var mismatching = await SeedToken("mismatching", sdkSiteId: 500, buildNumber: 0);

        await CreateService().PruneSenderIdMismatchesAsync(
            new List<DeviceToken> { mismatching }, targetedCount: 2, targetSdkSiteId: 500);

        var rows = await TimePlanningPnDbContext!.DeviceTokens.AsNoTracking()
            .ToDictionaryAsync(r => r.Id, r => r.WorkflowState);
        Assert.Multiple(() =>
        {
            Assert.That(rows[healthy.Id], Is.EqualTo(Constants.WorkflowStates.Created));
            Assert.That(rows[mismatching.Id], Is.EqualTo(Constants.WorkflowStates.Removed));
        });
    }

    [Test]
    public async Task PruneSenderIdMismatches_EveryTokenMismatched_PrunesNothing()
    {
        var first = await SeedToken("cred-1", sdkSiteId: 501, buildNumber: 0);
        var second = await SeedToken("cred-2", sdkSiteId: 501, buildNumber: 0);

        await CreateService().PruneSenderIdMismatchesAsync(
            new List<DeviceToken> { first, second }, targetedCount: 2, targetSdkSiteId: 501);

        var states = await TimePlanningPnDbContext!.DeviceTokens.AsNoTracking()
            .Select(r => r.WorkflowState).ToListAsync();
        Assert.That(states, Is.All.EqualTo(Constants.WorkflowStates.Created),
            "a wholesale mismatch is a credential fault; the tokens must survive it");
    }

    [Test]
    public async Task PruneSenderIdMismatches_SoleTargetMismatched_PrunesNothing()
    {
        var only = await SeedToken("cred-only", sdkSiteId: 502, buildNumber: 0);

        await CreateService().PruneSenderIdMismatchesAsync(
            new List<DeviceToken> { only }, targetedCount: 1, targetSdkSiteId: 502);

        var stored = await TimePlanningPnDbContext!.DeviceTokens.AsNoTracking().SingleAsync();
        Assert.That(stored.WorkflowState, Is.EqualTo(Constants.WorkflowStates.Created),
            "one device that mismatches on its own send is indistinguishable from a "
            + "credential fault, so it is kept");
    }

    private PushNotificationService CreateService() =>
        new(TimePlanningPnDbContext!, Substitute.For<ILogger<PushNotificationService>>());

    private async Task<DeviceToken> SeedToken(
        string token, int sdkSiteId, int buildNumber, string appId = "time")
    {
        var deviceToken = new DeviceToken
        {
            AppId = appId,
            InstallationId = $"inst-{appId}-{token}",
            SdkSiteId = sdkSiteId,
            FcmToken = token,
            Platform = "android",
            AppBuildNumber = buildNumber
        };
        await deviceToken.Create(TimePlanningPnDbContext!);
        return deviceToken;
    }
}

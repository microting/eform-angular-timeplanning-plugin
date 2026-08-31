using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FirebaseAdmin;
using FirebaseAdmin.Messaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microting.eForm.Infrastructure.Constants;
using Microting.TimePlanningBase.Infrastructure.Data.Entities;
using NSubstitute;
using NUnit.Framework;
using TimePlanning.Pn.Services.PushNotificationService;

#nullable enable
namespace TimePlanning.Pn.Test;

[TestFixture]
public class PushNotificationServiceTests : TestBaseSetup
{
    /// <summary>
    /// The Firebase app name this sender must own, pinned here as a literal on
    /// purpose. Reading the production constant back would make this test agree
    /// with any rename, including one that re-points the plugin at the app of
    /// another sender co-hosted in eFormAPI.Web. The name is a process-wide key,
    /// so it is treated like a wire value.
    /// </summary>
    private const string ExpectedFirebaseAppName = "microting-time";

    [SetUp]
    public async Task SetUp()
    {
        await base.Setup();
        DeleteFirebaseApps();
    }

    // FirebaseApp instances live in a process-wide registry that outlives the
    // fixture, so every test starts and ends with an empty one.
    [TearDown]
    public void DeleteFirebaseAppsAfterTest() => DeleteFirebaseApps();

    private static void DeleteFirebaseApps()
    {
        FirebaseApp.GetInstance(ExpectedFirebaseAppName)?.Delete();
        FirebaseApp.DefaultInstance?.Delete();
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

    // ---- Firebase app ownership -------------------------------------------
    //
    // TimePlanning.Pn and BackendConfiguration.Pn are loaded as plugins into
    // the SAME eFormAPI.Web process, and each holds the credential for a
    // DIFFERENT Firebase project. FirebaseApp.DefaultInstance is process-wide:
    // whichever plugin initialises first owns it, and every later sender then
    // pushes through the first one's project. Every token then comes back
    // SENDER_ID_MISMATCH, which this service reads as a credential fault and
    // retries forever without surfacing an error. Hence: a named app, never
    // the default one.

    [Test]
    public async Task Initialisation_CreatesTheNamedApp_AndNeverTheProcessWideDefault()
    {
        await ConfigureFirebaseServiceAccount();
        var logger = new RecordingLogger<PushNotificationService>();

        _ = new PushNotificationService(TimePlanningPnDbContext!, logger);

        Assert.Multiple(() =>
        {
            Assert.That(FirebaseApp.GetInstance(ExpectedFirebaseAppName), Is.Not.Null,
                $"this sender must own a Firebase app named '{ExpectedFirebaseAppName}'");
            Assert.That(FirebaseApp.DefaultInstance, Is.Null,
                "FirebaseApp.DefaultInstance is shared with every other plugin in "
                + "eFormAPI.Web; claiming it cross-contaminates Firebase credentials");
            Assert.That(logger.Errors, Is.Empty, "initialisation must not have failed");
        });
    }

    // The loser of the concurrent-first-request race, made deterministic:
    // FirebaseApp.Create THROWS when the name is already taken, and the
    // constructor swallows that into "push disabled", so the second
    // initialisation must find the existing app instead of creating one.
    [Test]
    public async Task Initialisation_WhenTheNamedAppAlreadyExists_ReusesItAndKeepsPushEnabled()
    {
        await ConfigureFirebaseServiceAccount();
        _ = new PushNotificationService(
            TimePlanningPnDbContext!, new RecordingLogger<PushNotificationService>());
        var firstApp = FirebaseApp.GetInstance(ExpectedFirebaseAppName);

        var secondLogger = new RecordingLogger<PushNotificationService>();
        _ = new PushNotificationService(TimePlanningPnDbContext!, secondLogger);

        Assert.Multiple(() =>
        {
            Assert.That(firstApp, Is.Not.Null);
            Assert.That(FirebaseApp.GetInstance(ExpectedFirebaseAppName), Is.SameAs(firstApp),
                "the second initialisation must reuse the app, not replace or duplicate it");
            Assert.That(secondLogger.Errors, Is.Empty,
                "a failed re-initialisation is swallowed and disables push for that "
                + "scoped request, which then silently sends nothing");
            Assert.That(FirebaseApp.DefaultInstance, Is.Null);
        });
    }

    [Test]
    public async Task Initialisation_UnderConcurrentFirstRequests_NeverDisablesPush()
    {
        await ConfigureFirebaseServiceAccount();

        const int racers = 8;
        var loggers = Enumerable.Range(0, racers)
            .Select(_ => new RecordingLogger<PushNotificationService>()).ToList();
        var contexts = Enumerable.Range(0, racers)
            .Select(_ => CreateTimePlanningPnDbContext()).ToList();
        var startLine = new Barrier(racers);

        // Real threads, not the pool: a Barrier only releases once every
        // participant has arrived, and Parallel.For gives no guarantee that
        // all of them are running at once.
        var threads = Enumerable.Range(0, racers)
            .Select(i => new Thread(() =>
            {
                startLine.SignalAndWait();
                _ = new PushNotificationService(contexts[i], loggers[i]);
            }))
            .ToList();

        foreach (var thread in threads)
        {
            thread.Start();
        }

        foreach (var thread in threads)
        {
            thread.Join();
        }

        foreach (var context in contexts)
        {
            await context.DisposeAsync();
        }

        Assert.Multiple(() =>
        {
            Assert.That(loggers.SelectMany(l => l.Errors), Is.Empty,
                "two concurrent first requests must not race each other into a "
                + "FirebaseAppAlreadyExistsException that disables push");
            Assert.That(FirebaseApp.GetInstance(ExpectedFirebaseAppName), Is.Not.Null);
            Assert.That(FirebaseApp.DefaultInstance, Is.Null);
        });
    }

    /// <summary>
    /// Points the plugin configuration at a syntactically valid but entirely
    /// synthetic service-account key. Creating a FirebaseApp only parses the
    /// credential, so this reaches the real initialisation path without any
    /// network call or real secret.
    /// </summary>
    private async Task ConfigureFirebaseServiceAccount()
    {
        using var rsa = RSA.Create(2048);
        var serviceAccountJson = JsonSerializer.Serialize(new Dictionary<string, string>
        {
            ["type"] = "service_account",
            ["project_id"] = "microting-time-test",
            ["private_key_id"] = "test-key-id",
            ["private_key"] = rsa.ExportPkcs8PrivateKeyPem(),
            ["client_email"] = "time-test@microting-time-test.iam.gserviceaccount.com",
            ["client_id"] = "1234567890",
            ["token_uri"] = "https://oauth2.googleapis.com/token"
        });

        var configurationValue = await TimePlanningPnDbContext!.PluginConfigurationValues
            .FirstAsync(x => x.Name == "TimePlanningBaseSettings:FirebaseServiceAccountJson");
        configurationValue.Value = serviceAccountJson;
        await TimePlanningPnDbContext.SaveChangesAsync();
    }

    private sealed class RecordingLogger<T> : ILogger<T>
    {
        private readonly ConcurrentQueue<string> _errors = new();

        public IReadOnlyCollection<string> Errors => _errors;

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (logLevel >= LogLevel.Error)
            {
                _errors.Enqueue($"{formatter(state, exception)} :: {exception}");
            }
        }
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

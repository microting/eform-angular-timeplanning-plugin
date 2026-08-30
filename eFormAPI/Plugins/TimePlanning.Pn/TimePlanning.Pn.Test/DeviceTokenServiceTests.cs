using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microting.eForm.Infrastructure.Constants;
using Microting.eFormApi.BasePn.Abstractions;
using Microting.eFormApi.BasePn.Infrastructure.Database.Entities;
using Microting.EformAngularFrontendBase.Infrastructure.Data;
using Microting.TimePlanningBase.Infrastructure.Data.Entities;
using NSubstitute;
using NUnit.Framework;
using TimePlanning.Pn.Services.DeviceTokenService;

namespace TimePlanning.Pn.Test;

[TestFixture]
public class DeviceTokenServiceTests : TestBaseSetup
{
    private DeviceTokenService _service = null!;
    private IUserService _userService = null!;

    [SetUp]
    public async Task SetUp()
    {
        await base.Setup();

        _userService = Substitute.For<IUserService>();
        var baseDbContext = Substitute.For<BaseDbContext>(
            new DbContextOptions<BaseDbContext>());
        var coreService = Substitute.For<IEFormCoreService>();

        _service = new DeviceTokenService(
            TimePlanningPnDbContext!,
            Substitute.For<ILogger<DeviceTokenService>>(),
            _userService,
            baseDbContext,
            coreService);
    }

    [Test]
    public async Task RegisterAsync_NewInstall_IsStored()
    {
        var result = await _service.RegisterAsync(
            42, "fcm-token-abc", "android", 0, "time", "inst-new");

        Assert.That(result.Success, Is.True);

        var stored = await TimePlanningPnDbContext!.DeviceTokens.SingleAsync();
        Assert.Multiple(() =>
        {
            Assert.That(stored.SdkSiteId, Is.EqualTo(42));
            Assert.That(stored.FcmToken, Is.EqualTo("fcm-token-abc"));
            Assert.That(stored.Platform, Is.EqualTo("android"));
            Assert.That(stored.AppId, Is.EqualTo("time"));
            Assert.That(stored.InstallationId, Is.EqualTo("inst-new"));
        });
    }

    // Was RegisterAsync_SameTokenTwice_UpsertsWithoutDuplicate, which pinned
    // the old token-keyed identity. The protective intent - a re-register must
    // never leave two live rows for one device - is preserved, re-expressed
    // against the (AppId, InstallationId) key that now carries identity.
    [Test]
    public async Task RegisterAsync_SameInstallTwice_UpsertsWithoutDuplicate()
    {
        await _service.RegisterAsync(1, "dup-token", "android", 0, "time", "inst-dup");

        var result = await _service.RegisterAsync(2, "dup-token", "ios", 0, "time", "inst-dup");

        Assert.That(result.Success, Is.True);
        Assert.That(await TimePlanningPnDbContext!.DeviceTokens.CountAsync(), Is.EqualTo(1));

        var stored = await TimePlanningPnDbContext.DeviceTokens.SingleAsync();
        Assert.That(stored.SdkSiteId, Is.EqualTo(2));
        Assert.That(stored.Platform, Is.EqualTo("ios"));
    }

    [Test]
    public async Task RegisterAsync_NewInstall_PersistsAppBuildNumber()
    {
        var result = await _service.RegisterAsync(
            42, "build-token", "android", 31221, "time", "inst-build");

        Assert.That(result.Success, Is.True);

        var stored = await TimePlanningPnDbContext!.DeviceTokens.SingleAsync();
        Assert.That(stored.AppBuildNumber, Is.EqualTo(31221));
    }

    [Test]
    public async Task RegisterAsync_ReRegister_UpdatesAppBuildNumber()
    {
        await _service.RegisterAsync(42, "build-token", "android", 31221, "time", "inst-build");

        var result = await _service.RegisterAsync(
            42, "build-token", "android", 40000, "time", "inst-build");

        Assert.That(result.Success, Is.True);
        Assert.That(await TimePlanningPnDbContext!.DeviceTokens.CountAsync(), Is.EqualTo(1));

        var stored = await TimePlanningPnDbContext.DeviceTokens.SingleAsync();
        Assert.That(stored.AppBuildNumber, Is.EqualTo(40000),
            "a re-register must refresh the stored app build number");
    }

    [Test]
    public async Task RegisterAsync_DefaultBuildNumber_PersistsZero()
    {
        await _service.RegisterAsync(
            42, "legacy-token", "android", installationId: "inst-nobuild");

        var stored = await TimePlanningPnDbContext!.DeviceTokens.SingleAsync();
        Assert.That(stored.AppBuildNumber, Is.EqualTo(0),
            "an old client that omits the build number is stored as 0");
    }

    [Test]
    public async Task RegisterAsync_OmittedAppId_DefaultsToTime()
    {
        await _service.RegisterAsync(
            42, "default-app-token", "android", installationId: "inst-defaultapp");

        var stored = await TimePlanningPnDbContext!.DeviceTokens.SingleAsync();
        Assert.That(stored.AppId, Is.EqualTo(DeviceTokenService.TimePlanningAppId),
            "this service only ever mints tokens for the time app");
    }

    [Test]
    public async Task UnregisterAsync_ExistingToken_IsRemoved()
    {
        await _service.RegisterAsync(1, "remove-me", "android", 0, "time", "inst-remove");
        Assert.That(await TimePlanningPnDbContext!.DeviceTokens.CountAsync(), Is.EqualTo(1));

        var result = await _service.UnregisterAsync("remove-me");

        Assert.That(result.Success, Is.True);
        var stored = await TimePlanningPnDbContext.DeviceTokens.SingleAsync();
        Assert.That(stored.WorkflowState, Is.EqualTo(Constants.WorkflowStates.Removed),
            "unregister must find the row by FcmToken and soft-delete it");
    }

    [Test]
    public async Task UnregisterAsync_NonExistentToken_SucceedsWithoutError()
    {
        var result = await _service.UnregisterAsync("does-not-exist");

        Assert.That(result.Success, Is.True);
    }

    [Test]
    public async Task RegisterForCallerAsync_NoAuthenticatedUser_RejectsWithoutStoring()
    {
        _userService.GetCurrentUserAsync().Returns(Task.FromResult<EformUser?>(null));

        var result = await _service.RegisterForCallerAsync(
            "dead-token", "android", 0, "time", "inst-noauth");

        Assert.That(result.Success, Is.False);
        Assert.That(await TimePlanningPnDbContext!.DeviceTokens.CountAsync(), Is.EqualTo(0));
    }

    // Was RegisterAsync_ReRegisteringDeadToken_RepairsSdkSiteId. The historical
    // SdkSiteId=0 bug is still worth pinning: a row stranded on site 0 must be
    // repaired by the next register from the same install.
    [Test]
    public async Task RegisterAsync_ReRegisteringDeadRow_RepairsSdkSiteId()
    {
        await _service.RegisterAsync(0, "legacy-token", "android", 0, "time", "inst-dead");

        var result = await _service.RegisterAsync(
            77, "legacy-token", "android", 0, "time", "inst-dead");

        Assert.That(result.Success, Is.True);
        Assert.That(await TimePlanningPnDbContext!.DeviceTokens.CountAsync(), Is.EqualTo(1));
        var stored = await TimePlanningPnDbContext.DeviceTokens.SingleAsync();
        Assert.That(stored.SdkSiteId, Is.EqualTo(77));
    }

    [Test]
    public async Task RegisterAsync_SameInstall_RotatedToken_UpdatesInPlace()
    {
        await _service.RegisterAsync(300, "tok-old", "android", 1, "time", "inst-rot");
        await _service.RegisterAsync(300, "tok-new", "android", 1, "time", "inst-rot");

        var rows = await TimePlanningPnDbContext!.DeviceTokens.AsNoTracking().ToListAsync();
        Assert.That(rows, Has.Count.EqualTo(1));
        Assert.That(rows[0].FcmToken, Is.EqualTo("tok-new"));
    }

    [Test]
    public async Task RegisterAsync_AfterSoftDelete_RevivesTheRow()
    {
        await _service.RegisterAsync(301, "tok-rev", "android", 1, "time", "inst-rev");

        var row = await TimePlanningPnDbContext!.DeviceTokens.SingleAsync();
        await row.Delete(TimePlanningPnDbContext);

        var result = await _service.RegisterAsync(301, "tok-rev", "android", 1, "time", "inst-rev");

        Assert.That(result.Success, Is.True);
        var revived = await TimePlanningPnDbContext.DeviceTokens.AsNoTracking().SingleAsync();
        Assert.That(revived.WorkflowState, Is.EqualTo(Constants.WorkflowStates.Created),
            "a pruned device that registers again must become visible to the send path");
    }

    [Test]
    public async Task RegisterAsync_SameInstall_DifferentUser_ReassignsOwner()
    {
        await _service.RegisterAsync(310, "tok-own", "android", 1, "time", "inst-own");
        await _service.RegisterAsync(311, "tok-own", "android", 1, "time", "inst-own");

        var rows = await TimePlanningPnDbContext!.DeviceTokens.AsNoTracking().ToListAsync();
        Assert.That(rows, Has.Count.EqualTo(1));
        Assert.That(rows[0].SdkSiteId, Is.EqualTo(311));
    }

    [Test]
    public async Task RegisterAsync_SecondDeviceForSameUser_CreatesASecondRow()
    {
        await _service.RegisterAsync(320, "tok-phone", "android", 1, "time", "inst-phone");
        await _service.RegisterAsync(320, "tok-tablet", "android", 1, "time", "inst-tablet");

        var rows = await TimePlanningPnDbContext!.DeviceTokens.AsNoTracking().ToListAsync();
        Assert.That(rows.Select(r => r.FcmToken),
            Is.EquivalentTo(new[] { "tok-phone", "tok-tablet" }),
            "one user with two devices must be two rows");
    }

    // The migration backfills pre-existing rows with InstallationId
    // 'legacy:<Id>'. When that device registers with its real install id the
    // register path must ADOPT the backfilled row, not add a second one: both
    // rows would be live, both would be selected by the sender, and the user
    // would get every push twice forever (FCM never prunes either - the token
    // is still valid).
    [Test]
    public async Task RegisterAsync_LegacyBackfilledRow_IsAdoptedNotDuplicated()
    {
        var legacy = new DeviceToken
        {
            AppId = "time",
            InstallationId = "legacy:1",
            FcmToken = "tok-legacy",
            SdkSiteId = 330,
            Platform = "android",
            AppBuildNumber = 0,
        };
        await legacy.Create(TimePlanningPnDbContext!);

        var result = await _service.RegisterAsync(
            330, "tok-legacy", "android", 42000, "time", "inst-real");

        Assert.That(result.Success, Is.True);
        var rows = await TimePlanningPnDbContext!.DeviceTokens.AsNoTracking().ToListAsync();
        Assert.That(rows, Has.Count.EqualTo(1), "the legacy row must be reused, not duplicated");
        Assert.Multiple(() =>
        {
            Assert.That(rows[0].Id, Is.EqualTo(legacy.Id));
            Assert.That(rows[0].InstallationId, Is.EqualTo("inst-real"));
            Assert.That(rows[0].AppBuildNumber, Is.EqualTo(42000));
        });
    }

    [Test]
    public async Task RegisterAsync_SoftDeletedLegacyRow_IsAdoptedAndRevived()
    {
        var legacy = new DeviceToken
        {
            AppId = "time",
            InstallationId = "legacy:2",
            FcmToken = "tok-legacy-dead",
            SdkSiteId = 331,
            Platform = "android",
        };
        await legacy.Create(TimePlanningPnDbContext!);
        await legacy.Delete(TimePlanningPnDbContext!);

        await _service.RegisterAsync(
            331, "tok-legacy-dead", "android", 0, "time", "inst-real-2");

        var rows = await TimePlanningPnDbContext!.DeviceTokens.AsNoTracking().ToListAsync();
        Assert.That(rows, Has.Count.EqualTo(1));
        Assert.Multiple(() =>
        {
            Assert.That(rows[0].InstallationId, Is.EqualTo("inst-real-2"));
            Assert.That(rows[0].WorkflowState, Is.EqualTo(Constants.WorkflowStates.Created));
        });
    }

    [Test]
    public async Task RegisterAsync_Adoption_DoesNotCrossAppId()
    {
        var foreign = new DeviceToken
        {
            AppId = "adhoc",
            InstallationId = "legacy:3",
            FcmToken = "tok-shared",
            SdkSiteId = 340,
            Platform = "android",
        };
        await foreign.Create(TimePlanningPnDbContext!);

        await _service.RegisterAsync(340, "tok-shared", "android", 0, "time", "inst-time");

        var rows = await TimePlanningPnDbContext!.DeviceTokens.AsNoTracking()
            .OrderBy(r => r.Id).ToListAsync();
        Assert.That(rows, Has.Count.EqualTo(2), "adoption must never take another app's row");
        Assert.Multiple(() =>
        {
            Assert.That(rows[0].AppId, Is.EqualTo("adhoc"));
            Assert.That(rows[0].InstallationId, Is.EqualTo("legacy:3"));
            Assert.That(rows[1].AppId, Is.EqualTo("time"));
            Assert.That(rows[1].InstallationId, Is.EqualTo("inst-time"));
        });
    }

    [Test]
    public async Task RegisterAsync_EmptyInstallationId_IsRejectedWithoutStoring()
    {
        var result = await _service.RegisterAsync(350, "tok-x", "android", 0, "time", "");

        Assert.That(result.Success, Is.False);
        Assert.That(await TimePlanningPnDbContext!.DeviceTokens.CountAsync(), Is.EqualTo(0));
    }

    [Test]
    public async Task RegisterAsync_EmptyAppId_IsRejectedWithoutStoring()
    {
        var result = await _service.RegisterAsync(351, "tok-y", "android", 0, "", "inst-noapp");

        Assert.That(result.Success, Is.False);
        Assert.That(await TimePlanningPnDbContext!.DeviceTokens.CountAsync(), Is.EqualTo(0));
    }
}

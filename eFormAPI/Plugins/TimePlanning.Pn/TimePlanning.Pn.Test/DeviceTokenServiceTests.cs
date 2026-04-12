using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microting.TimePlanningBase.Infrastructure.Data;
using NSubstitute;
using NUnit.Framework;
using TimePlanning.Pn.Services.DeviceTokenService;

namespace TimePlanning.Pn.Test;

[TestFixture]
public class DeviceTokenServiceTests
{
    private TimePlanningPnDbContext _dbContext = null!;
    private DeviceTokenService _service = null!;

    [SetUp]
    public void SetUp()
    {
        var options = new DbContextOptionsBuilder<TimePlanningPnDbContext>()
            .UseInMemoryDatabase(databaseName: $"DeviceTokenTestDb_{TestContext.CurrentContext.Test.Name}")
            .Options;
        _dbContext = new TimePlanningPnDbContext(options);
        _service = new DeviceTokenService(
            _dbContext,
            Substitute.For<ILogger<DeviceTokenService>>());
    }

    [TearDown]
    public void TearDown()
    {
        _dbContext.Database.EnsureDeleted();
        _dbContext.Dispose();
    }

    [Test]
    public async Task RegisterAsync_NewToken_IsStored()
    {
        var result = await _service.RegisterAsync(42, "fcm-token-abc", "android");

        Assert.That(result.Success, Is.True);

        var stored = await _dbContext.DeviceTokens.SingleAsync();
        Assert.That(stored.SdkSiteId, Is.EqualTo(42));
        Assert.That(stored.Token, Is.EqualTo("fcm-token-abc"));
        Assert.That(stored.Platform, Is.EqualTo("android"));
    }

    [Test]
    public async Task RegisterAsync_SameTokenTwice_UpsertsWithoutDuplicate()
    {
        await _service.RegisterAsync(1, "dup-token", "android");

        var result = await _service.RegisterAsync(2, "dup-token", "ios");

        Assert.That(result.Success, Is.True);
        Assert.That(await _dbContext.DeviceTokens.CountAsync(), Is.EqualTo(1));

        var stored = await _dbContext.DeviceTokens.SingleAsync();
        Assert.That(stored.SdkSiteId, Is.EqualTo(2));
        Assert.That(stored.Platform, Is.EqualTo("ios"));
    }

    [Test]
    public async Task UnregisterAsync_ExistingToken_IsRemoved()
    {
        await _service.RegisterAsync(1, "remove-me", "android");
        Assert.That(await _dbContext.DeviceTokens.CountAsync(), Is.EqualTo(1));

        var result = await _service.UnregisterAsync("remove-me");

        Assert.That(result.Success, Is.True);
    }

    [Test]
    public async Task UnregisterAsync_NonExistentToken_SucceedsWithoutError()
    {
        var result = await _service.UnregisterAsync("does-not-exist");

        Assert.That(result.Success, Is.True);
    }
}

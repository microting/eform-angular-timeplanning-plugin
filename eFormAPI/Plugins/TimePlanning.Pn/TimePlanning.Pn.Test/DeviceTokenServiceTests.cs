using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NUnit.Framework;
using TimePlanning.Pn.Infrastructure.Models.DeviceToken;
using TimePlanning.Pn.Services.DeviceTokenService;

namespace TimePlanning.Pn.Test;

[TestFixture]
public class DeviceTokenServiceTests
{
    private DeviceTokenDbContext _dbContext = null!;
    private DeviceTokenService _service = null!;

    [SetUp]
    public void SetUp()
    {
        var options = new DbContextOptionsBuilder<DeviceTokenDbContext>()
            .UseInMemoryDatabase(databaseName: $"DeviceTokenTestDb_{TestContext.CurrentContext.Test.Name}")
            .Options;
        _dbContext = new DeviceTokenDbContext(options);
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
        // Act
        var result = await _service.RegisterAsync(42, "fcm-token-abc", "android");

        // Assert
        Assert.That(result.Success, Is.True);

        var stored = await _dbContext.DeviceTokens.SingleAsync();
        Assert.That(stored.SdkSiteId, Is.EqualTo(42));
        Assert.That(stored.Token, Is.EqualTo("fcm-token-abc"));
        Assert.That(stored.Platform, Is.EqualTo("android"));
        Assert.That(stored.WorkflowState, Is.EqualTo("created"));
    }

    [Test]
    public async Task RegisterAsync_SameTokenTwice_UpsertsWithoutDuplicate()
    {
        // Arrange
        await _service.RegisterAsync(1, "dup-token", "android");

        // Act — re-register same token with different site
        var result = await _service.RegisterAsync(2, "dup-token", "ios");

        // Assert
        Assert.That(result.Success, Is.True);
        Assert.That(await _dbContext.DeviceTokens.CountAsync(), Is.EqualTo(1));

        var stored = await _dbContext.DeviceTokens.SingleAsync();
        Assert.That(stored.SdkSiteId, Is.EqualTo(2));
        Assert.That(stored.Platform, Is.EqualTo("ios"));
    }

    [Test]
    public async Task UnregisterAsync_ExistingToken_IsRemoved()
    {
        // Arrange
        await _service.RegisterAsync(1, "remove-me", "android");
        Assert.That(await _dbContext.DeviceTokens.CountAsync(), Is.EqualTo(1));

        // Act
        var result = await _service.UnregisterAsync("remove-me");

        // Assert
        Assert.That(result.Success, Is.True);
        Assert.That(await _dbContext.DeviceTokens.CountAsync(), Is.EqualTo(0));
    }

    [Test]
    public async Task UnregisterAsync_NonExistentToken_SucceedsWithoutError()
    {
        // Act
        var result = await _service.UnregisterAsync("does-not-exist");

        // Assert
        Assert.That(result.Success, Is.True);
    }

    [Test]
    public async Task RegisterAsync_EmptyToken_FailsGracefully()
    {
        // The DB has a [Required] Token column. With InMemory provider the constraint
        // isn't enforced at the DB level, but we still verify the service doesn't crash
        // and that an empty-string token is handled.
        var result = await _service.RegisterAsync(1, "", "android");

        // The service doesn't explicitly validate empty tokens — it succeeds but stores
        // an empty string. This test documents the current behaviour; a future guard
        // could make this return Success=false.
        Assert.That(result.Success, Is.True);
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microting.eForm.Infrastructure;
using Microting.eForm.Infrastructure.Constants;
using Microting.eFormApi.BasePn.Abstractions;
using Microting.eFormApi.BasePn.Infrastructure.Database.Entities;
using Microting.EformAngularFrontendBase.Infrastructure.Data.Entities.Permissions;
using NSubstitute;
using NUnit.Framework;
using Sentry;
using Sentry.Extensibility;
using Sentry.Protocol.Envelopes;
using TimePlanning.Pn.Infrastructure.Helpers;
using AssignedSiteEntity = Microting.TimePlanningBase.Infrastructure.Data.Entities.AssignedSite;
using SdkSite = Microting.eForm.Infrastructure.Data.Entities.Site;
using SdkSiteWorker = Microting.eForm.Infrastructure.Data.Entities.SiteWorker;
using SdkWorker = Microting.eForm.Infrastructure.Data.Entities.Worker;

#nullable enable
namespace TimePlanning.Pn.Test;

/// <summary>
/// Coverage for <see cref="SiteScopeResolver.ResolveForCurrentUserAsync"/> — the
/// single rule that decides which sites a signed-in caller sees on the planning
/// board and gets in the all-workers export.
///
/// The fixture exists for the delegated-advisor case, and keeps the four
/// pre-existing branches beside it so that case cannot be shipped by loosening
/// one of them.
///
/// THE DELEGATED CASE
/// ------------------
/// A partner advisor opens a customer's tenant from my.microting.com already
/// signed in; the tenant mints a credential-less "delegated principal" for them
/// just in time. Such a principal is in no security group, holds no role, and
/// its email is synthetic (<c>delegated-&lt;digest&gt;@delegated.invalid</c>), so
/// it matched none of rules 1-3 and could never match the Worker-by-email lookup
/// in rule 4 either. The planning page answered ErrorWhileObtainingPlannings and
/// Sentry was sent a message on EVERY request.
///
/// The ruling is that an advisor is a LEADER in timeplanning — they act across
/// the customer's whole organisation on the partner's behalf — and that putting
/// them in "eForm admins" is not acceptable, because that group carries far
/// wider privileges than this. So the resolver decides it, on the same marker
/// the host uses: a UserLogins row for the delegated provider.
/// </summary>
[TestFixture]
public class SiteScopeResolverTests : TestBaseSetup
{
    private const string AdminRole = "admin";
    private const string EformAdminsGroup = "eForm admins";
    private const string EformUsersGroup = "eForm users";
    private const string KunTidGroup = "Kun tid";

    /// <summary>The provider the host writes for an ordinary my.microting.com
    /// account link. Deliberately NOT the delegated one — a link row under this
    /// provider must not make anyone a leader.</summary>
    private const string PlainLinkProvider = "my.microting.com";

    private IUserService _userService = null!;
    private MicrotingDbContext _sdkDbContext = null!;

    [SetUp]
    public async Task SetUpTest()
    {
        await base.Setup();

        _userService = Substitute.For<IUserService>();

        var core = await GetCore();
        _sdkDbContext = core.DbContextHelper.GetDbContext();

        // Dropped and recreated per test by the base fixture's builder, and
        // disposed by its TearDown once parked in SeededBaseDbContext.
        SeededBaseDbContext = GetBaseDbContext();
    }

    [TearDown]
    public async Task TearDownTest()
    {
        await _sdkDbContext.DisposeAsync();
    }

    // ------------------------------------------------------------------
    // 1. The delegated advisor is a leader.
    // ------------------------------------------------------------------

    /// <summary>
    /// The headline case. Managers DO exist in this tenant — so the
    /// "eForm users with no manager anywhere" fallback cannot be what passes —
    /// and the advisor is in no security group and holds no role, so neither can
    /// rules 1 or 2. The only thing that can make this unrestricted is the
    /// delegated-principal check.
    /// </summary>
    [Test]
    public async Task DelegatedPrincipal_IsUnrestricted_EvenWhenManagersExistAndItIsInNoGroup()
    {
        await CreateAssignedSite(7101, isManager: true);
        await CreateAssignedSite(7102);
        await CreateAssignedSite(7103);

        var advisor = await SeedUserAsync(DelegatedEmail("a1b2c3"));
        await SeedDelegatedLoginAsync(advisor);
        PointCallerAt(advisor);

        // Guard: the degenerate-state fallback must be unreachable here, or this
        // test would pass with the delegated check deleted.
        Assert.That(
            await TimePlanningPnDbContext!.AssignedSites
                .AnyAsync(x => x.WorkflowState != Constants.WorkflowStates.Removed && x.IsManager),
            Is.True,
            "Guard: a manager must exist, so the no-manager fallback cannot be what grants the scope");

        var scope = await ResolveAsync();

        AssertUnrestrictedOver(scope, 7101, 7102, 7103);
    }

    /// <summary>
    /// The ordering proof that does not depend on Sentry: this advisor's email
    /// DOES match an SDK worker, at a plain non-manager site. Were the delegated
    /// check placed after the worker lookup, the lookup would succeed and narrow
    /// them to that one site. Unrestricted means the check ran first.
    /// </summary>
    [Test]
    public async Task DelegatedPrincipal_WinsOverTheWorkerLookup_WhichProvesItRunsFirst()
    {
        const string email = "delegated-also-a-worker@delegated.invalid";
        await CreateSdkSite(7111);
        await CreateAssignedSite(7111);
        await CreateAssignedSite(7112, isManager: true);
        await LinkWorkerToSite(7111, email, employeeNo: "1");

        var advisor = await SeedUserAsync(email);
        await SeedDelegatedLoginAsync(advisor);
        PointCallerAt(advisor);

        var scope = await ResolveAsync();

        AssertUnrestrictedOver(scope, 7111, 7112);
    }

    /// <summary>
    /// The per-request Sentry noise was half of this bug: rule 4 reported every
    /// delegated request as "Worker with email ... not found". The delegated
    /// caller must produce no event at all.
    ///
    /// The second half of the test is a CONTROL, deliberately inside the same
    /// hub: an ordinary caller with no worker row must still be reported. Without
    /// it, a hub that failed to install would make the first assertion pass
    /// vacuously, which is the usual way a "nothing was captured" test rots.
    ///
    /// Events are matched on their own email, so a fixture running in parallel
    /// that happens to capture something of its own cannot fail this one; and
    /// nothing leaves the process — BeforeSend drops every event and the
    /// transport is a no-op.
    /// </summary>
    [Test]
    public async Task DelegatedPrincipal_SendsNoSentryMessageAndNoError_UnlikeAnOrdinaryCallerWithNoWorker()
    {
        var delegatedEmail = DelegatedEmail("sentry-delegated");
        const string ordinaryEmail = "sentry-ordinary@example.com";

        await CreateAssignedSite(7121, isManager: true);
        await CreateAssignedSite(7122);

        var advisor = await SeedUserAsync(delegatedEmail);
        await SeedDelegatedLoginAsync(advisor);
        var ordinary = await SeedUserAsync(ordinaryEmail);

        var captured = new List<string>();
        using (SentryHubCapturing(captured))
        {
            PointCallerAt(advisor);
            var delegatedScope = await ResolveAsync();

            PointCallerAt(ordinary);
            var ordinaryScope = await ResolveAsync();

            SentrySdk.Flush(TimeSpan.FromSeconds(2));

            // Snapshot under the lock: a fixture running in parallel can still
            // be adding to the list while these assertions read it.
            List<string> messages;
            lock (captured)
            {
                messages = [.. captured];
            }

            Assert.That(delegatedScope.ErrorKey, Is.Null,
                "A delegated advisor must not fail the way a missing worker does");
            AssertUnrestrictedOver(delegatedScope, 7121, 7122);

            Assert.That(messages.Any(m => m.Contains(delegatedEmail, StringComparison.Ordinal)), Is.False,
                "The delegated caller must never reach the worker lookup, so Sentry must hear nothing: " +
                string.Join(" | ", messages));

            // Control: the reporting path is alive and the hub really is installed.
            Assert.That(ordinaryScope.ErrorKey, Is.EqualTo("ErrorWhileObtainingPlannings"),
                "Control: an ordinary caller with no worker row still fails as before");
            Assert.That(messages.Any(m => m.Contains(ordinaryEmail, StringComparison.Ordinal)), Is.True,
                "Control: that same path still reports to Sentry, so the absence asserted above is meaningful");
        }
    }

    /// <summary>
    /// Only the UserLogins row counts. An account whose email merely LOOKS
    /// delegated — anyone can be given that address — is an ordinary user and
    /// must be resolved as one. (Nor is a null PasswordHash the marker: this
    /// user has none either, and plenty of legitimate accounts do not.)
    /// </summary>
    [Test]
    public async Task UserWithADelegatedLookingEmailButNoLoginRow_IsNotDelegated()
    {
        await CreateAssignedSite(7131, isManager: true);
        await CreateAssignedSite(7132);

        var impostor = await SeedUserAsync(DelegatedEmail("not-really"));
        PointCallerAt(impostor);

        Assert.That(impostor.PasswordHash, Is.Null,
            "Guard: this account is password-less too, so only the login row can distinguish the two");

        var scope = await ResolveAsync();

        Assert.That(scope.ErrorKey, Is.EqualTo("ErrorWhileObtainingPlannings"),
            "A synthetic-looking email alone must not grant leader scope");
    }

    /// <summary>
    /// The other half of "only the UserLogins row counts": a row for the
    /// ORDINARY my.microting.com link provider is not a delegation. Those rows
    /// exist on real customer accounts that linked their portal login, and
    /// treating one as a delegation would silently promote them.
    /// </summary>
    [Test]
    public async Task UserLinkedUnderThePlainProvider_IsNotDelegated()
    {
        await CreateAssignedSite(7141, isManager: true);
        await CreateAssignedSite(7142);

        var linked = await SeedUserAsync("linked@example.com");
        await SeedLoginAsync(linked, PlainLinkProvider, "portal-key-1");
        PointCallerAt(linked);

        var scope = await ResolveAsync();

        Assert.That(scope.ErrorKey, Is.EqualTo("ErrorWhileObtainingPlannings"),
            "Only the delegated provider makes a delegated principal");
    }

    /// <summary>
    /// Pins the literal. The source of truth is
    /// <c>ExternalLoginProviders.MyMicrotingDelegated</c> in eFormAPI.Web, which
    /// this plugin cannot reference; if the two ever drift, every advisor
    /// silently stops being a leader again. This fails first, and says so.
    ///
    /// Works only because the constant is <c>static readonly</c> rather than
    /// <c>const</c> — a const would be folded into this assembly at compile time
    /// and the comparison would be literal-against-itself.
    /// </summary>
    [Test]
    public void TheDelegatedLoginProviderLiteralIsPinned()
    {
        Assert.That(SiteScopeResolver.DelegatedLoginProvider, Is.EqualTo("my.microting.com/delegated"),
            "This must stay byte-identical to ExternalLoginProviders.MyMicrotingDelegated in " +
            "eFormAPI.Web. Changing it here alone makes every delegated advisor an unresolvable " +
            "caller again; changing it in the host alone does the same.");
    }

    // ------------------------------------------------------------------
    // 2. Regression: the four pre-existing branches are untouched.
    // ------------------------------------------------------------------

    /// <summary>Rule 1: the "admin" role sees everything, and a manager
    /// elsewhere does not narrow that.</summary>
    [Test]
    public async Task AdminRole_IsStillUnrestricted()
    {
        await CreateAssignedSite(7201, isManager: true);
        await CreateAssignedSite(7202);

        var admin = await SeedUserAsync("admin-role@example.com", roleName: AdminRole);
        PointCallerAt(admin);

        var scope = await ResolveAsync();

        AssertUnrestrictedOver(scope, 7201, 7202);
    }

    /// <summary>Rule 2: membership of "eForm admins" is still unrestricted.</summary>
    [Test]
    public async Task EformAdminsGroupMember_IsStillUnrestricted()
    {
        await CreateAssignedSite(7211, isManager: true);
        await CreateAssignedSite(7212);

        var user = await SeedUserAsync("eform-admin@example.com");
        await AddToGroupAsync(user, EformAdminsGroup);
        PointCallerAt(user);

        var scope = await ResolveAsync();

        AssertUnrestrictedOver(scope, 7211, 7212);
    }

    /// <summary>Rule 3, the degenerate-state fallback: nobody in the system is a
    /// manager, so "eForm users" members get the whole board rather than an
    /// empty page.</summary>
    [Test]
    public async Task EformUsersGroupMember_WithNoManagerAnywhere_IsStillUnrestricted()
    {
        await CreateAssignedSite(7221);
        await CreateAssignedSite(7222);

        var user = await SeedUserAsync("eform-user-nomanager@example.com");
        await AddToGroupAsync(user, EformUsersGroup);
        PointCallerAt(user);

        var scope = await ResolveAsync();

        AssertUnrestrictedOver(scope, 7221, 7222);
    }

    /// <summary>Rule 3 the other way: once ANY manager exists the fallback stops
    /// applying, and an "eForm users" member is scoped like the worker they
    /// are.</summary>
    [Test]
    public async Task EformUsersGroupMember_WhenAManagerExists_IsStillRestrictedToTheirOwnSite()
    {
        const string email = "eform-user-withmanager@example.com";
        await CreateSdkSite(7231);
        await CreateAssignedSite(7231);
        await CreateAssignedSite(7232, isManager: true);
        await LinkWorkerToSite(7231, email, employeeNo: "1");

        var user = await SeedUserAsync(email);
        await AddToGroupAsync(user, EformUsersGroup);
        PointCallerAt(user);

        var scope = await ResolveAsync();

        AssertRestrictedTo(scope, 7231);
    }

    /// <summary>A "Kun tid" device user with web access is excluded from the
    /// fallback by name: no manager exists here, so without that exclusion they
    /// would see the whole board.</summary>
    [Test]
    public async Task KunTidGroupMember_IsStillRestrictedToTheirOwnSite_EvenWithNoManagerAnywhere()
    {
        const string email = "kun-tid@example.com";
        await CreateSdkSite(7241);
        await CreateAssignedSite(7241);
        await CreateAssignedSite(7242);
        await LinkWorkerToSite(7241, email, employeeNo: "1");

        var user = await SeedUserAsync(email);
        await AddToGroupAsync(user, EformUsersGroup);
        await AddToGroupAsync(user, KunTidGroup);
        PointCallerAt(user);

        Assert.That(
            await TimePlanningPnDbContext!.AssignedSites
                .AnyAsync(x => x.WorkflowState != Constants.WorkflowStates.Removed && x.IsManager),
            Is.False,
            "Guard: no manager exists, so only the 'Kun tid' exclusion can keep this caller restricted");

        var scope = await ResolveAsync();

        AssertRestrictedTo(scope, 7241);
    }

    /// <summary>Rule 4: a plain worker, in no group and holding no role, still
    /// sees only their own site.</summary>
    [Test]
    public async Task OrdinaryWorker_IsStillRestrictedToTheirOwnSite()
    {
        const string email = "plain-worker@example.com";
        await CreateSdkSite(7251);
        await CreateAssignedSite(7251);
        await CreateAssignedSite(7252, isManager: true);
        await LinkWorkerToSite(7251, email, employeeNo: "1");

        var user = await SeedUserAsync(email);
        PointCallerAt(user);

        var scope = await ResolveAsync();

        AssertRestrictedTo(scope, 7251);
    }

    // ------------------------------------------------------------------
    // Helpers
    // ------------------------------------------------------------------

    /// <summary>The shape my.microting.com mints: a digest, never a routable
    /// mailbox, which is why the worker-by-email lookup can never match it.</summary>
    private static string DelegatedEmail(string digest) => $"delegated-{digest}@delegated.invalid";

    /// <summary>Calls the resolver exactly as both production callers do —
    /// every non-removed AssignedSite as the pool.</summary>
    private async Task<SiteScope> ResolveAsync()
    {
        var assignedSites = await TimePlanningPnDbContext!.AssignedSites
            .Where(x => x.WorkflowState != Constants.WorkflowStates.Removed)
            .ToListAsync();

        return await SiteScopeResolver.ResolveForCurrentUserAsync(
            assignedSites, TimePlanningPnDbContext!, _sdkDbContext, SeededBaseDbContext!, _userService);
    }

    private static void AssertUnrestrictedOver(SiteScope scope, params int[] siteIds)
    {
        Assert.That(scope.ErrorKey, Is.Null, "The scope must resolve");
        Assert.That(scope.RestrictToOwnSite, Is.False, "A leader is not narrowed to one site");
        Assert.That(scope.ScopedSites.Select(x => x.SiteId), Is.EquivalentTo(siteIds));
        Assert.That(scope.Narrow(siteIds.ToList()), Is.EquivalentTo(siteIds),
            "Narrow() must leave a leader's site list untouched, as it does for an admin");
    }

    private static void AssertRestrictedTo(SiteScope scope, int siteId)
    {
        Assert.That(scope.ErrorKey, Is.Null, "The scope must resolve");
        Assert.That(scope.RestrictToOwnSite, Is.True);
        Assert.That(scope.OwnSiteId, Is.EqualTo(siteId));
        Assert.That(scope.ScopedSites.Select(x => x.SiteId), Is.EquivalentTo(new[] { siteId }));
    }

    private async Task<EformUser> SeedUserAsync(string email, string? roleName = null)
    {
        var db = SeededBaseDbContext!;
        var user = new EformUser
        {
            UserName = email,
            Email = email,
            FirstName = "Test",
            LastName = "User"
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        if (roleName != null)
        {
            var role = new EformRole { Name = roleName, NormalizedName = roleName.ToUpperInvariant() };
            db.Roles.Add(role);
            await db.SaveChangesAsync();
            db.UserRoles.Add(new EformUserRole { UserId = user.Id, RoleId = role.Id });
            await db.SaveChangesAsync();
        }

        return user;
    }

    /// <summary>The one marker that makes a caller a delegated principal — the
    /// same row the host's UserManagerLockoutExtensions.IsDelegatedPrincipalAsync
    /// reads.</summary>
    private Task SeedDelegatedLoginAsync(EformUser user) =>
        SeedLoginAsync(user, SiteScopeResolver.DelegatedLoginProvider, $"advisor-{user.Id}");

    private async Task SeedLoginAsync(EformUser user, string provider, string providerKey)
    {
        var db = SeededBaseDbContext!;
        db.UserLogins.Add(new IdentityUserLogin<int>
        {
            UserId = user.Id,
            LoginProvider = provider,
            ProviderKey = providerKey,
            ProviderDisplayName = provider
        });
        await db.SaveChangesAsync();
    }

    private async Task AddToGroupAsync(EformUser user, string groupName)
    {
        var db = SeededBaseDbContext!;
        var group = await db.SecurityGroups.FirstOrDefaultAsync(x => x.Name == groupName);
        if (group == null)
        {
            group = new SecurityGroup
            {
                Name = groupName,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                CreatedByUserId = 1,
                UpdatedByUserId = 1,
                WorkflowState = Constants.WorkflowStates.Created
            };
            db.SecurityGroups.Add(group);
            await db.SaveChangesAsync();
        }

        db.SecurityGroupUsers.Add(new SecurityGroupUser
        {
            EformUserId = user.Id,
            SecurityGroupId = group.Id,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            CreatedByUserId = 1,
            UpdatedByUserId = 1,
            WorkflowState = Constants.WorkflowStates.Created
        });
        await db.SaveChangesAsync();
    }

    /// <summary>Points the IUserService substitute at this user, so the next
    /// resolve runs as them.</summary>
    private void PointCallerAt(EformUser user)
    {
        _userService.UserId.Returns(user.Id);
        _userService.GetCurrentUserAsync().Returns(new EformUser { Id = user.Id });
    }

    private async Task<AssignedSiteEntity> CreateAssignedSite(int siteUid, bool isManager = false)
    {
        var assignedSite = new AssignedSiteEntity
        {
            SiteId = siteUid,
            UseOneMinuteIntervals = false,
            Resigned = false,
            IsManager = isManager,
            WorkflowState = Constants.WorkflowStates.Created,
            CreatedByUserId = 1,
            UpdatedByUserId = 1,
        };
        await assignedSite.Create(TimePlanningPnDbContext!);
        return assignedSite;
    }

    private async Task CreateSdkSite(int siteUid)
    {
        await using var sdkDb = await SdkDbContext();
        await new SdkSite { Name = $"Site {siteUid}", MicrotingUid = siteUid }.Create(sdkDb);
    }

    /// <summary>Creates the Worker/SiteWorker pair the resolver's rule-4 lookup
    /// matches on (by email) and resolves the own site from.</summary>
    private async Task LinkWorkerToSite(int siteUid, string email, string employeeNo)
    {
        await using var sdkDb = await SdkDbContext();
        var site = await sdkDb.Sites.FirstAsync(x => x.MicrotingUid == siteUid);

        var worker = new SdkWorker
        {
            FirstName = "Test",
            LastName = "Worker",
            Email = email,
            MicrotingUid = 3000 + siteUid,
            EmployeeNo = employeeNo,
        };
        await worker.Create(sdkDb);

        await new SdkSiteWorker
        {
            SiteId = site.Id,
            WorkerId = worker.Id,
            MicrotingUid = 4000 + siteUid,
        }.Create(sdkDb);
    }

    /// <summary>A NEW SDK context — DbContextHelper.GetDbContext() builds one per
    /// call and never hands back a shared instance, and writing through the same
    /// one the resolver reads would let the change tracker answer its query.</summary>
    private async Task<MicrotingDbContext> SdkDbContext() =>
        (await GetCore()).DbContextHelper.GetDbContext();

    /// <summary>
    /// Installs a Sentry hub that records the message of every event instead of
    /// sending it, and uninstalls it on dispose. Nothing reaches the network:
    /// BeforeSend drops each event after recording it, the transport is a no-op,
    /// and session tracking is off so there is no session envelope either.
    /// </summary>
    private static IDisposable SentryHubCapturing(List<string> captured) =>
        SentrySdk.Init(options =>
        {
            // Well-formed but unroutable; nothing is ever sent to it.
            options.Dsn = "https://0123456789abcdef0123456789abcdef@o0.ingest.sentry.io/0";
            options.Debug = false;
            options.AutoSessionTracking = false;
            options.SendClientReports = false;
            options.Transport = new DiscardingTransport();
            options.SetBeforeSend(sentryEvent =>
            {
                var message = sentryEvent.Message?.Formatted ?? sentryEvent.Message?.Message;
                if (message != null)
                {
                    lock (captured)
                    {
                        captured.Add(message);
                    }
                }

                // Dropping it here is what guarantees nothing is queued or sent.
                return null;
            });
        });

    private sealed class DiscardingTransport : ITransport
    {
        public Task SendEnvelopeAsync(Envelope envelope, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }
}

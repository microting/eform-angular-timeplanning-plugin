using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microting.EformAngularFrontendBase.Infrastructure.Const;
using NSubstitute;
using NUnit.Framework;
using TimePlanning.Pn.Controllers;
using TimePlanning.Pn.Infrastructure.Const;
using TimePlanning.Pn.Infrastructure.Data.Seed.Data;
using TimePlanning.Pn.Services.PlannerService;
using TimePlanning.Pn.Services.TimePlanningLocalizationService;

#nullable enable
namespace TimePlanning.Pn.Test;

/// <summary>
/// #5489: every planner endpoint, the menu entry and the route are gated by
/// the new permission <c>time_planning_planner</c>. The host turns every
/// seeded plugin permission into a policy named after its claim that requires
/// the claim with value <see cref="AuthConsts.ClaimDefaultValue"/>
/// (AuthServiceCollectionExtensions); these tests build that same policy and
/// run the real ASP.NET authorization evaluator against the controller's
/// own attributes.
/// </summary>
[TestFixture]
public class PlannerAccessWiringTests
{
    private const string Claim = "time_planning_planner";

    [Test]
    public void TheClaimName_IsTimePlanningPlanner()
    {
        Assert.That(TimePlanningPlannerClaims.Planner, Is.EqualTo(Claim));
    }

    [Test]
    public void Controller_LivesUnderPlannerRoute()
    {
        var route = typeof(TimePlanningPlannerController).GetCustomAttribute<RouteAttribute>();
        Assert.That(route?.Template, Is.EqualTo("api/time-planning-pn/planner"));
    }

    [Test]
    public void EveryAction_RequiresThePlannerPolicy_AndNoneIsAnonymous()
    {
        var classPolicies = typeof(TimePlanningPlannerController).GetCustomAttributes<AuthorizeAttribute>()
            .Select(a => a.Policy).ToList();
        Assert.That(classPolicies, Does.Contain(Claim));

        var actions = Actions();
        Assert.That(actions, Has.Count.EqualTo(12), "the planner endpoints were not found");
        foreach (var action in actions)
        {
            Assert.That(action.GetCustomAttribute<AllowAnonymousAttribute>(), Is.Null, action.Name);
        }
    }

    [Test]
    public async Task Policy_RefusesACallerWithoutTheClaim_AndAdmitsOneWithIt()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAuthorization(options =>
            options.AddPolicy(Claim, policy => policy.RequireClaim(Claim, AuthConsts.ClaimDefaultValue)));
        await using var provider = services.BuildServiceProvider();
        var policyProvider = provider.GetRequiredService<IAuthorizationPolicyProvider>();
        var authorization = provider.GetRequiredService<IAuthorizationService>();

        foreach (var action in Actions())
        {
            var attributes = typeof(TimePlanningPlannerController).GetCustomAttributes<AuthorizeAttribute>()
                .Concat(action.GetCustomAttributes<AuthorizeAttribute>())
                .ToList();
            var policy = await AuthorizationPolicy.CombineAsync(policyProvider, attributes);
            Assert.That(policy, Is.Not.Null, action.Name);

            var without = await authorization.AuthorizeAsync(User(("time_planning_plugin_access", "True")), policy!);
            var with = await authorization.AuthorizeAsync(User((Claim, AuthConsts.ClaimDefaultValue)), policy!);

            Assert.That(without.Succeeded, Is.False, $"{action.Name} must be forbidden without {Claim}");
            Assert.That(with.Succeeded, Is.True, $"{action.Name} must be allowed with {Claim}");
        }
    }

    [Test]
    public void Permission_IsSeeded()
    {
        var permission = TimePlanningPermissionsSeedData.Data.SingleOrDefault(p => p.ClaimName == Claim);
        Assert.That(permission, Is.Not.Null);
        Assert.That(permission!.PermissionName, Is.Not.Empty);
    }

    [Test]
    public void HeaderMenu_HasThePlannerEntry_GuardedByThePermission()
    {
        var localization = Substitute.For<ITimePlanningLocalizationService>();
        localization.GetString(Arg.Any<string>()).Returns(x => x[0]?.ToString());
        var serviceProvider = Substitute.For<IServiceProvider>();
        serviceProvider.GetService(typeof(ITimePlanningLocalizationService)).Returns(localization);

        var menu = new EformTimePlanningPlugin().HeaderMenu(serviceProvider);

        var entry = menu.LeftMenu.Single().MenuItems.SingleOrDefault(x => x.Link == "/plugins/time-planning-pn/planner");
        Assert.That(entry, Is.Not.Null);
        Assert.That(entry!.Guards, Is.EqualTo(new List<string> { Claim }));
        Assert.That(entry.Name, Is.EqualTo("Planner (beta)"));
    }

    [Test]
    public void NavigationMenu_HasThePlannerEntry_WithItsPermissionAndTranslations()
    {
        var menu = new EformTimePlanningPlugin().GetNavigationMenu(Substitute.For<IServiceProvider>());

        var entry = menu.Single().ChildItems.SingleOrDefault(x => x.Link == "/plugins/time-planning-pn/planner");
        Assert.That(entry, Is.Not.Null);
        Assert.That(entry!.MenuTemplate.DefaultLink, Is.EqualTo("/plugins/time-planning-pn/planner"));
        Assert.That(entry.MenuTemplate.Permissions.Select(p => p.ClaimName), Is.EqualTo(new[] { Claim }));
        Assert.That(entry.Translations.Single(t => t.LocaleName == "da").Name, Is.EqualTo("Planlægning (beta)"));
        Assert.That(entry.Position, Is.GreaterThan(menu.Single().ChildItems.Where(x => x != entry).Max(x => x.Position)));
    }

    [Test]
    public void PlannerService_IsRegistered()
    {
        var services = new ServiceCollection();
        new EformTimePlanningPlugin().ConfigureServices(services);

        Assert.That(services.Any(d => d.ServiceType == typeof(IPlannerService)
                                      && d.ImplementationType == typeof(PlannerService)), Is.True);
    }

    private static List<MethodInfo> Actions() =>
        typeof(TimePlanningPlannerController)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(m => m.GetCustomAttributes<HttpMethodAttribute>().Any())
            .ToList();

    private static ClaimsPrincipal User(params (string Type, string Value)[] claims) =>
        new(new ClaimsIdentity(claims.Select(c => new Claim(c.Type, c.Value)), "test"));
}

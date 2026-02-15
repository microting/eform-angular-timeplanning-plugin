/*
The MIT License (MIT)

Copyright (c) 2007 - 2021 Microting A/S

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
*/

using System.Runtime.InteropServices;
using Microting.eForm.Infrastructure.Models;
using Microting.EformAngularFrontendBase.Infrastructure.Data;
using Sentry;
using TimePlanning.Pn.Infrastructure.Helpers;
using TimePlanning.Pn.Services.TimePlanningRegistrationDeviceService;
using TimePlanning.Pn.Services.TimePlanningGpsCoordinateService;
using TimePlanning.Pn.Services.TimePlanningPictureSnapshotService;
using TimePlanning.Pn.Services.AbsenceRequestService;
using TimePlanning.Pn.Services.ContentHandoverService;
using TimePlanning.Pn.Services.BreakPolicyService;
using Constants = Microting.eForm.Infrastructure.Constants.Constants;

namespace TimePlanning.Pn;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Infrastructure.Data.Seed;
using Infrastructure.Data.Seed.Data;
using Infrastructure.Models.Settings;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microting.eFormApi.BasePn;
using Microting.eFormApi.BasePn.Abstractions;
using Microting.eFormApi.BasePn.Infrastructure.Consts;
using Microting.eFormApi.BasePn.Infrastructure.Database.Extensions;
using Microting.eFormApi.BasePn.Infrastructure.Helpers;
using Microting.eFormApi.BasePn.Infrastructure.Helpers.PluginDbOptions;
using Microting.eFormApi.BasePn.Infrastructure.Models.Application;
using Microting.eFormApi.BasePn.Infrastructure.Models.Application.NavigationMenu;
using Microting.eFormApi.BasePn.Infrastructure.Settings;
using Microting.TimePlanningBase.Infrastructure.Const;
using Microting.TimePlanningBase.Infrastructure.Data;
using Microting.TimePlanningBase.Infrastructure.Data.Factories;
using Services.TimePlanningFlexService;
using Services.TimePlanningSettingService;
using Services.TimePlanningLocalizationService;
using Services.TimePlanningPlanningService;
using Services.TimePlanningWorkingHoursService;

public class EformTimePlanningPlugin : IEformPlugin
{
    public string Name => "Microting Time Planning Plugin";
    public string PluginId => "eform-angular-time-planning-plugin";
    public string PluginPath => PluginAssembly().Location;
    public string PluginBaseUrl => "time-planning-pn";
    private string _connectionString;
    //private IBus _bus;

    public Assembly PluginAssembly()
    {
        return typeof(EformTimePlanningPlugin).GetTypeInfo().Assembly;
    }

    public void ConfigureServices(IServiceCollection services)
    {
        services.AddTransient<ITimePlanningLocalizationService, TimePlanningLocalizationService>();
        services.AddTransient<ITimePlanningPlanningService, TimePlanningPlanningService>();
        services.AddTransient<ITimePlanningWorkingHoursService, TimePlanningWorkingHoursService>();
        services.AddTransient<ITimePlanningFlexService, TimePlanningFlexService>();
        services.AddTransient<ITimePlanningRegistrationDeviceService, TimePlanningRegistrationDeviceService>();
        services.AddTransient<ITimePlanningGpsCoordinateService, TimePlanningGpsCoordinateService>();
        services.AddTransient<ITimePlanningPictureSnapshotService, TimePlanningPictureSnapshotService>();
        services.AddTransient<ISettingService, TimeSettingService>();
        services.AddTransient<IAbsenceRequestService, AbsenceRequestService>();
        services.AddTransient<IContentHandoverService, ContentHandoverService>();
        services.AddTransient<IBreakPolicyService, BreakPolicyService>();
        services.AddControllers();
    }

    public void AddPluginConfig(IConfigurationBuilder builder, string connectionString)
    {
        var seedData = new TimePlanningConfigurationSeedData();
        var contextFactory = new TimePlanningPnContextFactory();
        builder.AddPluginConfiguration(
            connectionString,
            seedData,
            contextFactory);
    }

    public void ConfigureOptionsServices(
        IServiceCollection services,
        IConfiguration configuration)
    {
        services.ConfigurePluginDbOptions<TimePlanningBaseSettings>(
            configuration.GetSection("TimePlanningBaseSettings"));
    }

    public void ConfigureDbContext(IServiceCollection services, string connectionString)
    {
        SentrySdk.Init(options =>
        {
            // A Sentry Data Source Name (DSN) is required.
            // See https://docs.sentry.io/product/sentry-basics/dsn-explainer/
            // You can set it in the SENTRY_DSN environment variable, or you can set it in code here.
            options.Dsn = "https://5e8442b08e1f6f30346dc6996be48b37@o4506241219428352.ingest.sentry.io/4506330566098944";

            // When debug is enabled, the Sentry client will emit detailed debugging information to the console.
            // This might be helpful, or might interfere with the normal operation of your application.
            // We enable it here for demonstration purposes when first trying Sentry.
            // You shouldn't do this in your applications unless you're troubleshooting issues with Sentry.
            options.Debug = false;

            // This option is recommended. It enables Sentry's "Release Health" feature.
            options.AutoSessionTracking = true;

            // This option is recommended for client applications only. It ensures all threads use the same global scope.
            // If you're writing a background service of any kind, you should remove this.
            options.IsGlobalModeEnabled = true;
        });

        string pattern = @"Database=(\d+)_eform-angular-time-planning-plugin;";
        Match match = Regex.Match(connectionString!, pattern);

        if (match.Success)
        {
            string numberString = match.Groups[1].Value;
            int number = int.Parse(numberString);
            SentrySdk.ConfigureScope(scope =>
            {
                scope.SetTag("customerNo", number.ToString());
                Console.WriteLine("customerNo: " + number);
                scope.SetTag("osVersion", Environment.OSVersion.ToString());
                Console.WriteLine("osVersion: " + Environment.OSVersion);
                scope.SetTag("osArchitecture", RuntimeInformation.OSArchitecture.ToString());
                Console.WriteLine("osArchitecture: " + RuntimeInformation.OSArchitecture);
                scope.SetTag("osName", RuntimeInformation.OSDescription);
                Console.WriteLine("osName: " + RuntimeInformation.OSDescription);
            });
        }


        var frontendBaseConnectionString = connectionString.Replace(
            "eform-angular-time-planning-plugin",
            "Angular");

        _connectionString = connectionString;
        services.AddSingleton<ITimePlanningDbContextHelper>(provider => new TimePlanningDbContextHelper(_connectionString));
        services.AddDbContext<TimePlanningPnDbContext>(o =>
            o.UseMySql(connectionString, new MariaDbServerVersion(
                ServerVersion.AutoDetect(connectionString)), mySqlOptionsAction: builder =>
            {
                builder.EnableRetryOnFailure();
                builder.MigrationsAssembly(PluginAssembly().FullName);
            }));


        services.AddDbContext<BaseDbContext>(
            o => o.UseMySql(frontendBaseConnectionString, new MariaDbServerVersion(
                ServerVersion.AutoDetect(frontendBaseConnectionString)), mySqlOptionsAction: builder =>
            {
                builder.EnableRetryOnFailure();
                builder.MigrationsAssembly(PluginAssembly().FullName);
            }));

        var contextFactory = new TimePlanningPnContextFactory();
        var context = contextFactory.CreateDbContext(new[] { connectionString });
        Console.WriteLine("Starting to migrate TimePlanningPnDbContext to latest version");
        context.Database.Migrate();
        Console.WriteLine("TimePlanningPnDbContext migrated to latest version");

        // Seed database
        SeedDatabase(connectionString);
    }

    public void Configure(IApplicationBuilder appBuilder)
    {
    }

    public List<PluginMenuItemModel> GetNavigationMenu(IServiceProvider serviceProvider)
    {
        List<PluginMenuItemModel> pluginMenu =
        [
            new()
            {
                Name = "Dropdown",
                E2EId = "time-planning-pn",
                Link = "",
                Type = MenuItemTypeEnum.Dropdown,
                Position = 0,
                Translations =
                [
                    new()
                    {
                        LocaleName = LocaleNames.English,
                        Name = "Time Planning",
                        Language = LanguageNames.English
                    },

                    new()
                    {
                        LocaleName = LocaleNames.German,
                        Name = "Time Planning",
                        Language = LanguageNames.German
                    },

                    new()
                    {
                        LocaleName = LocaleNames.Danish,
                        Name = "Timeregistrering",
                        Language = LanguageNames.Danish
                    }
                ],
                ChildItems =
                [
                    new()
                    {
                        Name = "Working hours",
                        E2EId = "time-planning-pn-working-hours",
                        Link = "/plugins/time-planning-pn/working-hours",
                        Type = MenuItemTypeEnum.Link,
                        MenuTemplate = new()
                        {
                            Name = "Working hours",
                            E2EId = "time-planning-pn-working-hours",
                            DefaultLink = "/plugins/time-planning-pn/working-hours",
                            Permissions = [],
                            Translations =
                            [
                                new()
                                {
                                    LocaleName = LocaleNames.English,
                                    Name = "Working hours",
                                    Language = LanguageNames.English
                                },

                                new()
                                {
                                    LocaleName = LocaleNames.German,
                                    Name = "Working hours",
                                    Language = LanguageNames.German
                                },

                                new()
                                {
                                    LocaleName = LocaleNames.Danish,
                                    Name = "Timeregistrering",
                                    Language = LanguageNames.Danish
                                }
                            ]
                        },
                        Translations =
                        [
                            new()
                            {
                                LocaleName = LocaleNames.English,
                                Name = "Working hours",
                                Language = LanguageNames.English
                            },

                            new()
                            {
                                LocaleName = LocaleNames.German,
                                Name = "Working hours",
                                Language = LanguageNames.German
                            },

                            new()
                            {
                                LocaleName = LocaleNames.Danish,
                                Name = "Timeregistrering",
                                Language = LanguageNames.Danish
                            }
                        ]
                    },

                    new()
                    {
                        Name = "Flex",
                        E2EId = "time-planning-pn-flex",
                        Link = "/plugins/time-planning-pn/flex",
                        Type = MenuItemTypeEnum.Link,
                        Position = 2,
                        MenuTemplate = new()
                        {
                            Name = "Flex",
                            E2EId = "time-planning-pn-flex",
                            DefaultLink = "/plugins/time-planning-pn/flex",
                            Permissions = [],
                            Translations =
                            [
                                new()
                                {
                                    LocaleName = LocaleNames.English,
                                    Name = "Flex",
                                    Language = LanguageNames.English
                                },

                                new()
                                {
                                    LocaleName = LocaleNames.German,
                                    Name = "Flex",
                                    Language = LanguageNames.German
                                },

                                new()
                                {
                                    LocaleName = LocaleNames.Danish,
                                    Name = "Flex",
                                    Language = LanguageNames.Danish
                                }
                            ]
                        },
                        Translations =
                        [
                            new()
                            {
                                LocaleName = LocaleNames.English,
                                Name = "Flex",
                                Language = LanguageNames.English
                            },

                            new()
                            {
                                LocaleName = LocaleNames.German,
                                Name = "Flex",
                                Language = LanguageNames.German
                            },

                            new()
                            {
                                LocaleName = LocaleNames.Danish,
                                Name = "Flex",
                                Language = LanguageNames.Danish
                            }
                        ]
                    },

                    new()
                    {
                        Name = "RegistrationDevice",
                        E2EId = "time-planning-pn-registration-devices",
                        Link = "/plugins/time-planning-pn/registration-devices",
                        Type = MenuItemTypeEnum.Link,
                        Position = 2,
                        MenuTemplate = new()
                        {
                            Name = "RegistrationDevice",
                            E2EId = "time-planning-pn-registration-devices",
                            DefaultLink = "/plugins/time-planning-pn/registration-devices",
                            Permissions = [],
                            Translations =
                            [
                                new()
                                {
                                    LocaleName = LocaleNames.English,
                                    Name = "Registration devices",
                                    Language = LanguageNames.English
                                },

                                new()
                                {
                                    LocaleName = LocaleNames.German,
                                    Name = "Registrierungsgeräte",
                                    Language = LanguageNames.German
                                },

                                new()
                                {
                                    LocaleName = LocaleNames.Danish,
                                    Name = "Registreringsenheder",
                                    Language = LanguageNames.Danish
                                }
                            ]
                        },
                        Translations =
                        [
                            new()
                            {
                                LocaleName = LocaleNames.English,
                                Name = "Registration devices",
                                Language = LanguageNames.English
                            },

                            new()
                            {
                                LocaleName = LocaleNames.German,
                                Name = "Registrierungsgeräte",
                                Language = LanguageNames.German
                            },

                            new()
                            {
                                LocaleName = LocaleNames.Danish,
                                Name = "Registreringsenheder",
                                Language = LanguageNames.Danish
                            }
                        ]
                    },

                    new()
                    {
                        Name = "Dashboard",
                        E2EId = "time-planning-pn-planning",
                        Link = "/plugins/time-planning-pn/planning",
                        Type = MenuItemTypeEnum.Link,
                        Position = 3,
                        MenuTemplate = new PluginMenuTemplateModel
                        {
                            Name = "Dashboard",
                            E2EId = "time-planning-pn-planning",
                            DefaultLink = "/plugins/time-planning-pn/planning",
                            Permissions = [],
                            Translations =
                            [
                                new()
                                {
                                    LocaleName = LocaleNames.English,
                                    Name = "Dashboard",
                                    Language = LanguageNames.English
                                },

                                new()
                                {
                                    LocaleName = LocaleNames.German,
                                    Name = "Dashboard",
                                    Language = LanguageNames.German
                                },

                                new()
                                {
                                    LocaleName = LocaleNames.Danish,
                                    Name = "Dashboard",
                                    Language = LanguageNames.Danish
                                }
                            ]
                        },
                        Translations =
                        [
                            new()
                            {
                                LocaleName = LocaleNames.English,
                                Name = "Dashboard",
                                Language = LanguageNames.English
                            },

                            new()
                            {
                                LocaleName = LocaleNames.German,
                                Name = "Dashboard",
                                Language = LanguageNames.German
                            },

                            new()
                            {
                                LocaleName = LocaleNames.Danish,
                                Name = "Dashboard",
                                Language = LanguageNames.Danish
                            }
                        ]
                    },

                    new()
                    {
                        Name = "Timer",
                        E2EId = "time-planning-pn-mobile-working-hours",
                        Link = "/plugins/time-planning-pn/working-hours/mobile-working-hours",
                        Type = MenuItemTypeEnum.Link,
                        Position = 4,
                        MenuTemplate = new PluginMenuTemplateModel
                        {
                            Name = "Timer",
                            E2EId = "time-planning-pn-mobile-working-hours",
                            DefaultLink = "/plugins/time-planning-pn/working-hours/mobile-working-hours",
                            Permissions = [],
                            Translations =
                            [
                                new()
                                {
                                    LocaleName = LocaleNames.English,
                                    Name = "Timer",
                                    Language = LanguageNames.English
                                },

                                new()
                                {
                                    LocaleName = LocaleNames.German,
                                    Name = "Timer",
                                    Language = LanguageNames.German
                                },

                                new()
                                {
                                    LocaleName = LocaleNames.Danish,
                                    Name = "Timer",
                                    Language = LanguageNames.Danish
                                }
                            ]
                        },
                        Translations =
                        [
                            new()
                            {
                                LocaleName = LocaleNames.English,
                                Name = "Timer",
                                Language = LanguageNames.English
                            },

                            new()
                            {
                                LocaleName = LocaleNames.German,
                                Name = "Timer",
                                Language = LanguageNames.German
                            },

                            new()
                            {
                                LocaleName = LocaleNames.Danish,
                                Name = "Timer",
                                Language = LanguageNames.Danish
                            }
                        ]
                    }
                ]
            }
        ];

        return pluginMenu;
    }

    public MenuModel HeaderMenu(IServiceProvider serviceProvider)
    {
        var localizationService = serviceProvider
            .GetService<ITimePlanningLocalizationService>();

        var result = new MenuModel();
        result.LeftMenu.Add(new MenuItemModel
        {
            Name = localizationService.GetString("Time Planning"),
            E2EId = "time-planning-pn",
            Link = "",
            Guards = [TimePlanningClaims.AccessTimePlanningPlugin],
            MenuItems =
            [
                new()
                {
                    Name = localizationService.GetString("Plannings"),
                    E2EId = "time-planning-pn-planning",
                    Link = "/plugins/time-planning-pn/planning",
                    Guards = [TimePlanningClaims.GetPlanning],
                    Position = 0
                },

                new()
                {
                    Name = localizationService.GetString("Working hours"),
                    E2EId = "time-planning-pn-working-hours",
                    Link = "/plugins/time-planning-pn/working-hours",
                    Guards = [TimePlanningClaims.GetWorkingHours],
                    Position = 1
                },

                new()
                {
                    Name = localizationService.GetString("Flex"),
                    E2EId = "time-planning-pn-flex",
                    Link = "/plugins/time-planning-pn/flex",
                    Position = 2,
                    Guards = [TimePlanningClaims.GetFlex]
                },

                new()
                {
                    Name = localizationService.GetString("Dashboard"),
                    E2EId = "time-planning-pn-planning",
                    Link = "/plugins/time-planning-pn/planning",
                    Position = 3,
                    Guards = [TimePlanningClaims.GetFlex]
                },

                new()
                {
                    Name = localizationService.GetString("Timer"),
                    E2EId = "time-planning-pn-mobile-working-hours",
                    Link = "/plugins/time-planning-pn/working-hours/mobile-working-hours",
                    Position = 4,
                    Guards = [TimePlanningClaims.GetFlex]
                }
            ]
        });
        return result;
    }

    public void SeedDatabase(string connectionString)
    {
        var contextFactory = new TimePlanningPnContextFactory();
        using var dbContext = contextFactory.CreateDbContext([connectionString]);

        var activeAssignedSites = dbContext.AssignedSites
            .Where(x => x.WorkflowState != Constants.WorkflowStates.Removed).ToList();

        foreach (var activeAssignedSite in activeAssignedSites)
        {
            if (activeAssignedSite.AllowPersonalTimeRegistration)
            {
                activeAssignedSite.EnableMobileAccess = true;
                activeAssignedSite.Update(dbContext).GetAwaiter().GetResult();
            }
        }

        TimePlanningPluginSeed.SeedData(dbContext);
    }

    public PluginPermissionsManager GetPermissionsManager(string connectionString)
    {
        var contextFactory = new TimePlanningPnContextFactory();
        var context = contextFactory.CreateDbContext([connectionString]);

        return new PluginPermissionsManager(context);
    }
}
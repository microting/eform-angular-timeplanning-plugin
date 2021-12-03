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

namespace TimePlanning.Pn
{
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
    using Microting.eFormApi.BasePn.Infrastructure.Database.Entities;
    using Microting.eFormApi.BasePn.Infrastructure.Database.Extensions;
    using Microting.eFormApi.BasePn.Infrastructure.Helpers;
    using Microting.eFormApi.BasePn.Infrastructure.Helpers.PluginDbOptions;
    using Microting.eFormApi.BasePn.Infrastructure.Models.Application;
    using Microting.eFormApi.BasePn.Infrastructure.Models.Application.NavigationMenu;
    using Microting.eFormApi.BasePn.Infrastructure.Settings;
    using Microting.TimePlanningBase.Infrastructure.Const;
    using Microting.TimePlanningBase.Infrastructure.Data;
    using Microting.TimePlanningBase.Infrastructure.Data.Factories;
    using Services.RebusService;
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
            services.AddSingleton<IRebusService, RebusService>();
            services.AddTransient<ITimePlanningLocalizationService, TimePlanningLocalizationService>();
            services.AddTransient<ITimePlanningPlanningService, TimePlanningPlanningService>();
            services.AddTransient<ITimePlanningWorkingHoursService, TimePlanningWorkingHoursService>();
            services.AddTransient<ITimePlanningFlexService, TimePlanningFlexService>();
            services.AddTransient<ISettingService, TimeSettingService>();
            services.AddControllers();
            SeedEForms(services);
        }

        public void AddPluginConfig(IConfigurationBuilder builder, string connectionString)
        {
            var seedData = new TimePlanningConfigurationSeedData();
            var contextFactory = new TimePlanningPnContextFactory();
            builder.AddPluginConfiguration(
                connectionString,
                seedData,
                contextFactory);

            //CaseUpdateDelegates.CaseUpdateDelegate += UpdateRelatedCase;
        }

        public void ConfigureOptionsServices(
            IServiceCollection services,
            IConfiguration configuration)
        {
            services.ConfigurePluginDbOptions<TimePlanningBaseSettings>(
                configuration.GetSection("TimePlanningBaseSettings"));
        }

        private static async void SeedEForms(IServiceCollection services)
        {
            var serviceProvider = services.BuildServiceProvider();
            var core = await serviceProvider.GetRequiredService<IEFormCoreService>().GetCore();
            var eform = TimePlanningSeedEforms.GetForms().FirstOrDefault();
            var lasteForm = TimePlanningSeedEforms.GetForms().LastOrDefault();
            var sdkDbContext = core.DbContextHelper.GetDbContext();
            var context = serviceProvider.GetRequiredService<TimePlanningPnDbContext>();
            var options = serviceProvider.GetRequiredService<IPluginDbOptions<TimePlanningBaseSettings>>();
            var user = serviceProvider.GetRequiredService<IUserService>();
            // seed eforms
            var assembly = Assembly.GetExecutingAssembly();

            var resourceStream = assembly.GetManifestResourceStream($"TimePlanning.Pn.Resources.eForms.{eform.Key}.xml");
            if (resourceStream == null)
            {
                Console.WriteLine(eform.Key);
            }
            else
            {
                string contents;
                using (var sr = new StreamReader(resourceStream))
                {
                    contents = await sr.ReadToEndAsync();
                }
                var newTemplate = await core.TemplateFromXml(contents);
                var originalId = await sdkDbContext.CheckLists
                    .Where(x => x.OriginalId == newTemplate.OriginalId)
                    .Select(x => x.Id)
                    .FirstOrDefaultAsync();
                if (originalId == 0)
                {
                    int clId = await core.TemplateCreate(newTemplate);
                    var cl = await sdkDbContext.CheckLists.SingleOrDefaultAsync(x => x.Id == clId);
                    cl.IsLocked = true;
                    cl.IsEditable = false;
                    cl.ReportH1 = eform.Value[0];
                    cl.ReportH2 = eform.Value[1];
                    await cl.Update(sdkDbContext);
                }

                await options.UpdateDb(settings =>
                {
                    settings.EformId = originalId;
                }, context, user.UserId);
            }
            resourceStream = assembly.GetManifestResourceStream($"TimePlanning.Pn.Resources.eForms.{lasteForm.Key}.xml");
            if (resourceStream == null)
            {
                Console.WriteLine(eform.Key);
            }
            else
            {
                string contents;
                using (var sr = new StreamReader(resourceStream))
                {
                    contents = await sr.ReadToEndAsync();
                }
                var newTemplate = await core.TemplateFromXml(contents);
                var originalId = await sdkDbContext.CheckLists
                    .Where(x => x.OriginalId == newTemplate.OriginalId)
                    .Select(x => x.Id)
                    .FirstOrDefaultAsync();
                if (originalId == 0)
                {
                    int clId = await core.TemplateCreate(newTemplate);
                    var cl = await sdkDbContext.CheckLists.SingleOrDefaultAsync(x => x.Id == clId);
                    cl.IsLocked = true;
                    cl.IsEditable = false;
                    cl.ReportH1 = eform.Value[0];
                    cl.ReportH2 = eform.Value[1];
                    await cl.Update(sdkDbContext);
                }

                await options.UpdateDb(settings =>
                {
                    settings.InfoeFormId = originalId;
                }, context, user.UserId);
            }
        }

        public void ConfigureDbContext(IServiceCollection services, string connectionString)
        {
            _connectionString = connectionString;
            services.AddDbContext<TimePlanningPnDbContext>(o =>
                o.UseMySql(connectionString, new MariaDbServerVersion(
                    new Version(10, 4, 0)), mySqlOptionsAction: builder =>
                {
                    builder.EnableRetryOnFailure();
                    builder.MigrationsAssembly(PluginAssembly().FullName);
                }));

            var contextFactory = new TimePlanningPnContextFactory();
            var context = contextFactory.CreateDbContext(new[] { connectionString });
            context.Database.Migrate();

            // Seed database
            SeedDatabase(connectionString);
        }

        public void Configure(IApplicationBuilder appBuilder)
        {
            var serviceProvider = appBuilder.ApplicationServices;

            var rabbitMqHost = "localhost";

            if (_connectionString.Contains("frontend"))
            {
                var dbPrefix = Regex.Match(_connectionString, @"atabase=(\d*)_").Groups[1].Value;
                rabbitMqHost = $"frontend-{dbPrefix}-rabbitmq";
            }

            var rebusService = serviceProvider.GetService<IRebusService>();
            rebusService.Start(_connectionString, "admin", "password", rabbitMqHost);

            //_bus = rebusService.GetBus();
        }

        public List<PluginMenuItemModel> GetNavigationMenu(IServiceProvider serviceProvider)
        {
            var pluginMenu = new List<PluginMenuItemModel>
            {
                    new()
                    {
                        Name = "Dropdown",
                        E2EId = "time-planning-pn",
                        Link = "",
                        Type = MenuItemTypeEnum.Dropdown,
                        Position = 0,
                        Translations = new List<PluginMenuTranslationModel>
                        {
                            new()
                            {
                                 LocaleName = LocaleNames.English,
                                 Name = "Time Planning",
                                 Language = LanguageNames.English,
                            },
                            new()
                            {
                                 LocaleName = LocaleNames.German,
                                 Name = "Time Planning",
                                 Language = LanguageNames.German,
                            },
                            new()
                            {
                                 LocaleName = LocaleNames.Danish,
                                 Name = "Time Planning",
                                 Language = LanguageNames.Danish,
                            },
                        },
                        ChildItems = new List<PluginMenuItemModel>
                        {
                            // new()
                            // {
                            //     Name = "Plannings",
                            //     E2EId = "time-planning-pn-plannings",
                            //     Link = "/plugins/time-planning-pn/planning",
                            //     Type = MenuItemTypeEnum.Link,
                            //     Position = 0,
                            //     MenuTemplate = new PluginMenuTemplateModel
                            //     {
                            //         Name = "Plannings",
                            //         E2EId = "time-planning-pn-plannings",
                            //         DefaultLink = "/plugins/time-planning-pn/planning",
                            //         Permissions = new List<PluginMenuTemplatePermissionModel>
                            //         {
                            //             new()
                            //             {
                            //                 ClaimName = TimePlanningClaims.GetPlanning,
                            //                 PermissionName = "Obtain planning",
                            //                 PermissionTypeName = "Plannings",
                            //             },
                            //         },
                            //         Translations = new List<PluginMenuTranslationModel>
                            //         {
                            //             new()
                            //             {
                            //                 LocaleName = LocaleNames.English,
                            //                 Name = "Plannings",
                            //                 Language = LanguageNames.English,
                            //             },
                            //             new()
                            //             {
                            //                 LocaleName = LocaleNames.German,
                            //                 Name = "Planungs",
                            //                 Language = LanguageNames.German,
                            //             },
                            //             new()
                            //             {
                            //                 LocaleName = LocaleNames.Danish,
                            //                 Name = "Planlægning",
                            //                 Language = LanguageNames.Danish,
                            //             },
                            //         }
                            //     },
                            //     Translations = new List<PluginMenuTranslationModel>
                            //     {
                            //         new()
                            //         {
                            //             LocaleName = LocaleNames.English,
                            //             Name = "Plannings",
                            //             Language = LanguageNames.English,
                            //         },
                            //         new()
                            //         {
                            //             LocaleName = LocaleNames.German,
                            //             Name = "Planungs",
                            //             Language = LanguageNames.German,
                            //         },
                            //         new()
                            //         {
                            //             LocaleName = LocaleNames.Danish,
                            //             Name = "Planlægning",
                            //             Language = LanguageNames.Danish,
                            //         },
                            //     }
                            // },
                            new()
                            {
                                Name = "Working hours",
                                E2EId = "time-planning-pn-working-hours",
                                Link = "/plugins/time-planning-pn/working-hours",
                                Type = MenuItemTypeEnum.Link,
                                MenuTemplate = new PluginMenuTemplateModel
                                {
                                    Name = "Working hours",
                                    E2EId = "time-planning-pn-working-hours",
                                    DefaultLink = "/plugins/time-planning-pn/working-hours",
                                    Permissions = new List<PluginMenuTemplatePermissionModel>(),
                                    Translations = new List<PluginMenuTranslationModel>
                                    {
                                        new()
                                        {
                                            LocaleName = LocaleNames.English,
                                            Name = "Working hours",
                                            Language = LanguageNames.English,
                                        },
                                        new()
                                        {
                                            LocaleName = LocaleNames.German,
                                            Name = "Working hours",
                                            Language = LanguageNames.German,
                                        },
                                        new()
                                        {
                                            LocaleName = LocaleNames.Danish,
                                            Name = "Working hours",
                                            Language = LanguageNames.Danish,
                                        },
                                    }
                                },
                                Translations = new List<PluginMenuTranslationModel>
                                {
                                    new()
                                    {
                                        LocaleName = LocaleNames.English,
                                        Name = "Working hours",
                                        Language = LanguageNames.English,
                                    },
                                    new()
                                    {
                                        LocaleName = LocaleNames.German,
                                        Name = "Working hours",
                                        Language = LanguageNames.German,
                                    },
                                    new()
                                    {
                                        LocaleName = LocaleNames.Danish,
                                        Name = "Working hours",
                                        Language = LanguageNames.Danish,
                                    },
                                }
                            },
                            new()
                            {
                                Name = "Flex",
                                E2EId = "time-planning-pn-flex",
                                Link = "/plugins/time-planning-pn/flex",
                                Type = MenuItemTypeEnum.Link,
                                Position = 2,
                                MenuTemplate = new PluginMenuTemplateModel
                                {
                                    Name = "Flex",
                                    E2EId = "time-planning-pn-flex",
                                    DefaultLink = "/plugins/time-planning-pn/flex",
                                    Permissions = new List<PluginMenuTemplatePermissionModel>(),
                                    Translations = new List<PluginMenuTranslationModel>
                                    {
                                        new()
                                        {
                                            LocaleName = LocaleNames.English,
                                            Name = "Flex",
                                            Language = LanguageNames.English,
                                        },
                                        new()
                                        {
                                            LocaleName = LocaleNames.German,
                                            Name = "Flex",
                                            Language = LanguageNames.German,
                                        },
                                        new()
                                        {
                                            LocaleName = LocaleNames.Danish,
                                            Name = "Flex",
                                            Language = LanguageNames.Danish,
                                        },
                                    }
                                },
                                Translations = new List<PluginMenuTranslationModel>
                                {
                                    new()
                                    {
                                        LocaleName = LocaleNames.English,
                                        Name = "Flex",
                                        Language = LanguageNames.English,
                                    },
                                    new()
                                    {
                                        LocaleName = LocaleNames.German,
                                        Name = "Flex",
                                        Language = LanguageNames.German,
                                    },
                                    new()
                                    {
                                        LocaleName = LocaleNames.Danish,
                                        Name = "Flex",
                                        Language = LanguageNames.Danish,
                                    },
                                }
                            }
                        }
                    }
                };

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
                Guards = new List<string> { TimePlanningClaims.AccessTimePlanningPlugin },
                MenuItems = new List<MenuItemModel>
                {
                    new()
                    {
                        Name = localizationService.GetString("Plannings"),
                        E2EId = "time-planning-pn-planning",
                        Link = "/plugins/time-planning-pn/planning",
                        Guards = new List<string> { TimePlanningClaims.GetPlanning },
                        Position = 0,
                    },
                    new()
                    {
                        Name = localizationService.GetString("Working hours"),
                        E2EId = "time-planning-pn-working-hours",
                        Link = "/plugins/time-planning-pn/working-hours",
                        Guards = new List<string> { TimePlanningClaims.GetWorkingHours },
                        Position = 1,
                    },
                    new()
                    {
                        Name = localizationService.GetString("Flex"),
                        E2EId = "time-planning-pn-flex",
                        Link = "/plugins/time-planning-pn/flex",
                        Position = 2,
                        Guards = new List<string> { TimePlanningClaims.GetFlex },
                    }
                }
            });
            return result;
        }

        public void SeedDatabase(string connectionString)
        {
            // Get DbContext
            var contextFactory = new TimePlanningPnContextFactory();
            using var context = contextFactory.CreateDbContext(new[] { connectionString });
            // Seed configuration
            TimePlanningPluginSeed.SeedData(context);
        }

        public PluginPermissionsManager GetPermissionsManager(string connectionString)
        {
            var contextFactory = new TimePlanningPnContextFactory();
            var context = contextFactory.CreateDbContext(new[] { connectionString });

            return new PluginPermissionsManager(context);
        }
    }
}

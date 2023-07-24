using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using Hangfire;
using Hangfire.Dashboard;
using Hangfire.PostgreSql;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Cors;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using IIASA.WorldCereal.Rdm.EntityFrameworkCore;
using IIASA.WorldCereal.Rdm.ServiceConfigs;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.AspNetCore.Routing;
using Volo.Abp.AspNetCore.Mvc.UI.Theme.Basic;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;
using Volo.Abp;
using Volo.Abp.Account;
using Volo.Abp.Account.Web;
using Volo.Abp.AspNetCore.Authentication.JwtBearer;
using Volo.Abp.AspNetCore.MultiTenancy;
using Volo.Abp.AspNetCore.Mvc;
using Volo.Abp.AspNetCore.Mvc.AntiForgery;
using Volo.Abp.AspNetCore.Mvc.UI.Bundling;
using Volo.Abp.AspNetCore.Mvc.UI.Theme.Basic.Bundling;
using Volo.Abp.AspNetCore.Mvc.UI.Theme.Shared;
using Volo.Abp.AspNetCore.Serilog;
using Volo.Abp.Autofac;
using Volo.Abp.BackgroundJobs.Hangfire;
using Volo.Abp.Localization;
using Volo.Abp.Modularity;
using Volo.Abp.Swashbuckle;
using Volo.Abp.UI.Navigation.Urls;
using Volo.Abp.VirtualFileSystem;

namespace IIASA.WorldCereal.Rdm
{
    [DependsOn(
        typeof(RdmHttpApiModule),
        typeof(AbpAutofacModule),
        typeof(AbpAspNetCoreMultiTenancyModule),
        typeof(RdmApplicationModule),
        typeof(RdmEntityFrameworkCoreDbMigrationsModule),
        typeof(AbpBackgroundJobsHangfireModule),
        typeof(AbpAspNetCoreMvcUiBasicThemeModule),
        typeof(AbpAspNetCoreAuthenticationJwtBearerModule),
        typeof(AbpAccountWebIdentityServerModule),
        typeof(AbpAspNetCoreSerilogModule),
        typeof(AbpSwashbuckleModule)
    )]
    public class RdmHttpApiHostModule : AbpModule
    {
        private IConfiguration _configuration;
        private const string DefaultCorsPolicyName = "Default";

        public override void ConfigureServices(ServiceConfigurationContext context)
        {
            _configuration = context.Services.GetConfiguration();
            var hostingEnvironment = context.Services.GetHostingEnvironment();

            ConfigureBundles();
            ConfigureUrls(_configuration);
            ConfigureConventionalControllers();
            ConfigureAuthentication(context, _configuration);
            ConfigureLocalization();
            ConfigureVirtualFileSystem(context);
            ConfigureCors(context, _configuration);
            ConfigureSwaggerServices(context, _configuration);
            ConfigureWorldCerealAppConfig(context, _configuration);

            Configure<AbpAntiForgeryOptions>(options =>
            {
                options.TokenCookie.Expiration = TimeSpan.FromDays(365);
                options.AutoValidateIgnoredHttpMethods.Remove("GET");
                options.AutoValidateFilter =
                    type => !type.Namespace.StartsWith("IIASA.WorldCereal.Rdm");
            });
        }

        private void ConfigureBundles()
        {
            Configure<AbpBundlingOptions>(options =>
            {
                options.StyleBundles.Configure(
                    BasicThemeBundles.Styles.Global,
                    bundle => { bundle.AddFiles("/global-styles.css"); }
                );
            });
        }

        private void ConfigureUrls(IConfiguration configuration)
        {
            Configure<AppUrlOptions>(options =>
            {
                options.Applications["MVC"].RootUrl = configuration["App:SelfUrl"];
                options.RedirectAllowedUrls.AddRange(configuration["App:RedirectAllowedUrls"].Split(','));

                options.Applications["Angular"].RootUrl = configuration["App:ClientUrl"];
                options.Applications["Angular"].Urls[AccountUrlNames.PasswordReset] = "account/reset-password";
            });
        }

        private void ConfigureWorldCerealAppConfig(ServiceConfigurationContext context, IConfiguration configuration)
        {
            var connectionString = configuration.GetConnectionString("Default");
            context.Services.AddHangfire(config =>
            {
                config.UseStorage(new PostgreSqlStorage(connectionString,
                    new PostgreSqlStorageOptions {InvisibilityTimeout = TimeSpan.FromDays(1)}));
            });

            var apiConfig = new CollectionApiConfig();
            configuration.GetSection("CollectionApiConfig").Bind(apiConfig);
            context.Services.AddSingleton(apiConfig);

            var userDatasetConfig = new UserDatasetConfig();
            configuration.GetSection("UserDatasetConfig").Bind(userDatasetConfig);
            context.Services.AddSingleton(userDatasetConfig);
            
            var ewocConfig = new EwocConfig();
            configuration.GetSection("EwocConfig").Bind(ewocConfig);
            context.Services.AddSingleton(ewocConfig);
        }

        private void ConfigureVirtualFileSystem(ServiceConfigurationContext context)
        {
            var hostingEnvironment = context.Services.GetHostingEnvironment();

            if (hostingEnvironment.IsDevelopment())
            {
                Configure<AbpVirtualFileSystemOptions>(options =>
                {
                    options.FileSets.ReplaceEmbeddedByPhysical<RdmDomainSharedModule>(
                        Path.Combine(hostingEnvironment.ContentRootPath,
                            $"..{Path.DirectorySeparatorChar}IIASA.WorldCereal.Rdm.Domain.Shared"));
                    options.FileSets.ReplaceEmbeddedByPhysical<RdmDomainModule>(
                        Path.Combine(hostingEnvironment.ContentRootPath,
                            $"..{Path.DirectorySeparatorChar}IIASA.WorldCereal.Rdm.Domain"));
                    options.FileSets.ReplaceEmbeddedByPhysical<RdmApplicationContractsModule>(
                        Path.Combine(hostingEnvironment.ContentRootPath,
                            $"..{Path.DirectorySeparatorChar}IIASA.WorldCereal.Rdm.Application.Contracts"));
                    options.FileSets.ReplaceEmbeddedByPhysical<RdmApplicationModule>(
                        Path.Combine(hostingEnvironment.ContentRootPath,
                            $"..{Path.DirectorySeparatorChar}IIASA.WorldCereal.Rdm.Application"));
                });
            }
        }

        private void ConfigureConventionalControllers()
        {
            Configure<RouteOptions>(opt =>
            {
                opt.LowercaseUrls = true;
                opt.LowercaseQueryStrings = true;
            });

            Configure<AbpAspNetCoreMvcOptions>(options =>
            {
                options.ConventionalControllers.Create(typeof(RdmApplicationModule).Assembly);
            });
        }

        private void ConfigureAuthentication(ServiceConfigurationContext context, IConfiguration configuration)
        {
            context.Services.AddAuthentication()
                .AddJwtBearer(options =>
                {
                    options.Authority = configuration["AuthServer:Authority"];
                    options.RequireHttpsMetadata = Convert.ToBoolean(configuration["AuthServer:RequireHttpsMetadata"]);
                    options.Audience = "Rdm";
                    options.BackchannelHttpHandler = new HttpClientHandler
                    {
                        ServerCertificateCustomValidationCallback =
                            HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
                    };
                });
        }

        private static void ConfigureSwaggerServices(ServiceConfigurationContext context, IConfiguration configuration)
        {
            context.Services.AddSwaggerGen(
                options =>
                {
                    options.SwaggerDoc("OGC", new OpenApiInfo {Title = "WorldCereal OGC Data APIs", Version = "v1"});
                    options.SwaggerDoc("v1", new OpenApiInfo {Title = "WorldCereal Application APIs", Version = "v1"});
                    options.DocInclusionPredicate((docName, description) =>
                    {
                        return SelectActions(docName, description);
                    });
                    AddXml("IIASA.WorldCereal.Rdm.Application.Contracts.xml", options);
                    AddXml("IIASA.WorldCereal.Rdm.Application.xml", options);
                    //options.OperationFilter<AddRequiredHeaderParameter>();
                });
        }

        private static bool SelectActions(string docName, ApiDescription description)
        {
            var ogc = "OGC";
            if (docName != ogc)
            {
                if (description.GroupName == ogc)
                {
                    return false;
                }

                return true;
            }

            if (docName == description.GroupName)
            {
                return true;
            }

            // apply your swagger filters here, eg "OGC" = controllers decorated with
            return false;
        }

        private static void AddXml(string assemblyName, SwaggerGenOptions options)
        {
            var filePath = $"{AppContext.BaseDirectory}{Path.DirectorySeparatorChar}{assemblyName}";
            options.IncludeXmlComments(filePath);
        }

        private void ConfigureLocalization()
        {
            Configure<AbpLocalizationOptions>(options =>
            {
                options.Languages.Add(new LanguageInfo("ar", "ar", "العربية"));
                options.Languages.Add(new LanguageInfo("cs", "cs", "Čeština"));
                options.Languages.Add(new LanguageInfo("en", "en", "English"));
                options.Languages.Add(new LanguageInfo("en-GB", "en-GB", "English (UK)"));
                options.Languages.Add(new LanguageInfo("fr", "fr", "Français"));
                options.Languages.Add(new LanguageInfo("hu", "hu", "Magyar"));
                options.Languages.Add(new LanguageInfo("pt-BR", "pt-BR", "Português"));
                options.Languages.Add(new LanguageInfo("ru", "ru", "Русский"));
                options.Languages.Add(new LanguageInfo("tr", "tr", "Türkçe"));
                options.Languages.Add(new LanguageInfo("zh-Hans", "zh-Hans", "简体中文"));
                options.Languages.Add(new LanguageInfo("zh-Hant", "zh-Hant", "繁體中文"));
                options.Languages.Add(new LanguageInfo("de-DE", "de-DE", "Deutsch", "de"));
                options.Languages.Add(new LanguageInfo("es", "es", "Español", "es"));
            });
        }

        private void ConfigureCors(ServiceConfigurationContext context, IConfiguration configuration)
        {
            context.Services.AddCors(options =>
            {
                options.AddPolicy(DefaultCorsPolicyName, builder =>
                {
                    builder
                        .WithOrigins(
                            configuration["App:CorsOrigins"]
                                .Split(",", StringSplitOptions.RemoveEmptyEntries)
                                .Select(o => o.RemovePostFix("/"))
                                .ToArray()
                        )
                        .WithAbpExposedHeaders()
                        .SetIsOriginAllowedToAllowWildcardSubdomains()
                        .AllowAnyHeader()
                        .AllowAnyMethod()
                        .AllowCredentials();
                });
            });
        }

        public override void OnApplicationInitialization(ApplicationInitializationContext context)
        {
            var app = context.GetApplicationBuilder();
            var env = context.GetEnvironment();

            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            app.UseAbpRequestLocalization();

            if (!env.IsDevelopment())
            {
                app.UseErrorPage();
            }

            app.UseCorrelationId();
            app.UseStaticFiles();
            app.UseRouting();
            app.UseCors(DefaultCorsPolicyName);

            app.UseMultiTenancy();

            app.UseUnitOfWork();

            ConfigureSwaggerInApp(app);

            app.UseAuditing();
            app.UseAbpSerilogEnrichers();
            app.UseConfiguredEndpoints();

            var backgroundJobServerOptions = new BackgroundJobServerOptions();
            app.UseHangfireServer(backgroundJobServerOptions);
            app.UseHangfireDashboard(pathMatch: "/jobs",
                new DashboardOptions
                {
                    DisplayStorageConnectionString = true,
                    Authorization = new[] {new JobDashboardAuthorizationFilter()}
                });
        }

        private void ConfigureSwaggerInApp(IApplicationBuilder app)
        {
            var jobUrl = $"{_configuration["App:SelfUrl"]}/jobs";

            app.UseSwagger();
            app.UseSwaggerUI(options =>
            {
                options.SwaggerEndpoint("/swagger/OGC/swagger.json", "WorldCereal OGC Data API");
                options.SwaggerEndpoint("/swagger/v1/swagger.json", "WorldCereal Application APIs");
                options.HeadContent =
                    $"<h4><a href=\"{jobUrl}\" target=\"_blank\">Check Jobs Here</a></h4>";
            });
        }
    }

    public class JobDashboardAuthorizationFilter : IDashboardAuthorizationFilter
    {
        public bool Authorize(DashboardContext context)
        {
            // need to change this if hangfire url exposed public
            return true;
        }
    }
}
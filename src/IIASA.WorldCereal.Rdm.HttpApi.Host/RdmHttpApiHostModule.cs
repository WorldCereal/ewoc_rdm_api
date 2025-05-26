using System;
using System.IdentityModel.Tokens.Jwt;
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
using IIASA.WorldCereal.Rdm.MultiTenancy;
using IIASA.WorldCereal.Rdm.ServiceConfigs;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;
using Volo.Abp;
using Volo.Abp.Account;
using Volo.Abp.AspNetCore.Authentication.JwtBearer;
using Volo.Abp.AspNetCore.MultiTenancy;
using Volo.Abp.AspNetCore.Mvc;
using Volo.Abp.AspNetCore.Mvc.AntiForgery;
using Volo.Abp.AspNetCore.Mvc.UI.Theme.Basic;
using Volo.Abp.AspNetCore.Mvc.UI.Theme.Shared;
using Volo.Abp.AspNetCore.Serilog;
using Volo.Abp.Autofac;
using Volo.Abp.BackgroundJobs.Hangfire;
using Volo.Abp.Modularity;
using Volo.Abp.Swashbuckle;
using Volo.Abp.UI.Navigation.Urls;
using Volo.Abp.VirtualFileSystem;

namespace IIASA.WorldCereal.Rdm;

[DependsOn(
    typeof(RdmHttpApiModule),
    typeof(AbpAutofacModule),
    typeof(AbpAspNetCoreMultiTenancyModule),
    typeof(RdmApplicationModule),
    typeof(RdmEntityFrameworkCoreModule),
    typeof(AbpBackgroundJobsHangfireModule),
    typeof(AbpAspNetCoreMvcUiBasicThemeModule),
    typeof(AbpAspNetCoreAuthenticationJwtBearerModule),
    //typeof(AbpAccountWebOpenIddictModule),
    typeof(AbpAspNetCoreSerilogModule),
    typeof(AbpSwashbuckleModule)
)]
public class RdmHttpApiHostModule : AbpModule
{
    private IConfiguration _configuration;

    public override void PreConfigureServices(ServiceConfigurationContext context)
    {
        PreConfigure<OpenIddictBuilder>(builder =>
        {
            builder.AddValidation(options =>
            {
                options.AddAudiences("Rdm");
                options.UseLocalServer();
                options.UseAspNetCore();
            });
        });
    }

    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        _configuration = context.Services.GetConfiguration();
        var hostingEnvironment = context.Services.GetHostingEnvironment();

        ConfigureAuthentication(context, _configuration);
        ConfigureUrls(_configuration);
        ConfigureConventionalControllers();
        ConfigureVirtualFileSystem(context);
        ConfigureCors(context, _configuration);
        ConfigureSwaggerServices(context, _configuration);
        ConfigureWorldCerealAppConfig(context, _configuration);
        
        context.Services.Configure<FormOptions>(options =>
        {
            options.MultipartBodyLengthLimit = 1000 * 1024 * 1024; // 1000 MB
        });

        context.Services.AddHangfireServer();

        var azureConfig = new AzureConfig();
        _configuration.GetSection("AzureConfig").Bind(azureConfig);
        context.Services.AddSingleton(azureConfig);

        Configure<AbpAntiForgeryOptions>(options =>
        {
            options.TokenCookie.Expiration = TimeSpan.FromDays(365);
            options.AutoValidateIgnoredHttpMethods.Remove("GET");
            options.AutoValidateFilter =
                type => !type.Namespace!.StartsWith("IIASA.WorldCereal.Rdm");
        });
    }

    private void ConfigureAuthentication(ServiceConfigurationContext context, IConfiguration configuration)
    {
        context.Services
            .AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
                //options.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
            })
            .AddJwtBearer(options =>
            {
                options.SaveToken = true;
                options.UseSecurityTokenValidators = true;
                options.TokenValidationParameters.ValidateIssuer = false;
                options.TokenValidationParameters.ValidateIssuerSigningKey = false;
                options.TokenValidationParameters.SignatureValidator =
                    (token, _) => new JwtSecurityToken(token);
                options.Authority = configuration["AuthServer:Authority"];
                options.RequireHttpsMetadata = Convert.ToBoolean(configuration["AuthServer:RequireHttpsMetadata"]);
                options.Audience = "account";
                options.BackchannelHttpHandler = new HttpClientHandler
                {
                    ServerCertificateCustomValidationCallback =
                        HttpClientHandler.DangerousAcceptAnyServerCertificateValidator,
                };
            });
        var multiSchemePolicy = new AuthorizationPolicyBuilder(
                JwtBearerDefaults.AuthenticationScheme)
            .RequireAuthenticatedUser()
            .Build();

        context.Services.AddAuthorization(o => o.DefaultPolicy = multiSchemePolicy);
    }

    private void ConfigureUrls(IConfiguration configuration)
    {
        Configure<AppUrlOptions>(options =>
        {
            options.Applications["MVC"].RootUrl = configuration["App:SelfUrl"];
            options.RedirectAllowedUrls.AddRange(configuration["App:RedirectAllowedUrls"]?.Split(',') ??
                                                 Array.Empty<string>());

            options.Applications["Angular"].RootUrl = configuration["App:ClientUrl"];
            options.Applications["Angular"].Urls[AccountUrlNames.PasswordReset] = "account/reset-password";
        });
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

    private void ConfigureWorldCerealAppConfig(ServiceConfigurationContext context, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Default");
        context.Services.AddHangfire(config =>
        {
            config.UseStorage(new PostgreSqlStorage(connectionString,
                new PostgreSqlStorageOptions { InvisibilityTimeout = TimeSpan.FromDays(1) }));
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

    private void ConfigureConventionalControllers()
    {
        Configure<AbpAspNetCoreMvcOptions>(options =>
        {
            options.ConventionalControllers.Create(typeof(RdmApplicationModule).Assembly);
        });
    }

    private static void ConfigureSwaggerServices(ServiceConfigurationContext context, IConfiguration configuration)
    {
        /*context.Services.AddAbpSwaggerGenWithOAuth(
            configuration["AuthServer:Authority"]!,
            new Dictionary<string, string>
            {
                    {"Rdm", "Rdm API"}
            },
            options =>
            {
                options.SwaggerDoc("v1", new OpenApiInfo { Title = "Rdm API", Version = "v1" });
                options.DocInclusionPredicate((docName, description) => true);
                options.CustomSchemaIds(type => type.FullName);
            });*/


        context.Services.AddSwaggerGen(
            options =>
            {
                // Include 'SecurityScheme' to use JWT Authentication
                var jwtSecurityScheme = new OpenApiSecurityScheme
                {
                    BearerFormat = "JWT",
                    Name = "JWT Authentication",
                    In = ParameterLocation.Header,
                    Type = SecuritySchemeType.Http,
                    Scheme = JwtBearerDefaults.AuthenticationScheme,
                    Description =
                        "Put **_ONLY_** your JWT Bearer token below. 'Bearer' text will be appended automatically",

                    Reference = new OpenApiReference
                    {
                        Id = JwtBearerDefaults.AuthenticationScheme,
                        Type = ReferenceType.SecurityScheme
                    }
                };
                options.AddSecurityDefinition(jwtSecurityScheme.Reference.Id, jwtSecurityScheme);

                options.AddSecurityRequirement(new OpenApiSecurityRequirement
                {
                    { jwtSecurityScheme, Array.Empty<string>() }
                });
                options.SwaggerDoc("OGC", new OpenApiInfo { Title = "WorldCereal OGC Data APIs", Version = "v1", Description = "<p>Explore and download harmonized reference data <a href='https://github.com/WorldCereal/worldcereal-classification/blob/main/notebooks/worldcereal_RDM_demo.ipynb'> Python Notebook </a></p>"});
                options.SwaggerDoc("v1", new OpenApiInfo { Title = "WorldCereal Application APIs", Version = "v1" });
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

    private void ConfigureCors(ServiceConfigurationContext context, IConfiguration configuration)
    {
        context.Services.AddCors(options =>
        {
            options.AddDefaultPolicy(builder =>
            {
                builder
                    .WithOrigins(configuration["App:CorsOrigins"]?
                        .Split(",", StringSplitOptions.RemoveEmptyEntries)
                        .Select(o => o.RemovePostFix("/"))
                        .ToArray() ?? Array.Empty<string>())
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
        app.UseCors();
        app.UseAuthentication();
        //app.UseAbpOpenIddictValidation();

        if (MultiTenancyConsts.IsEnabled)
        {
            app.UseMultiTenancy();
        }

        app.UseUnitOfWork();
        app.UseDynamicClaims();
        app.UseAuthorization();

        app.UseSwagger();
        /*app.UseAbpSwaggerUI(c =>
        {
            c.SwaggerEndpoint("/swagger/v1/swagger.json", "Rdm API");

            var configuration = context.ServiceProvider.GetRequiredService<IConfiguration>();
            c.OAuthClientId(configuration["AuthServer:SwaggerClientId"]);
            c.OAuthScopes("Rdm");
        });*/

        ConfigureSwaggerInApp(app);
        app.UseAuditing();
        app.UseAbpSerilogEnrichers();
        app.UseConfiguredEndpoints();

        /*var backgroundJobServerOptions = new BackgroundJobServerOptions();
        app.UseHangfireServer(backgroundJobServerOptions);*/
        app.UseHangfireDashboard(pathMatch: "/jobs",
            new DashboardOptions
            {
                DisplayStorageConnectionString = true,
                Authorization = new[] { new JobDashboardAuthorizationFilter() }
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
using IIASA.WorldCereal.Rdm.Core;
using IIASA.WorldCereal.Rdm.Core.Ai;
using IIASA.WorldCereal.Rdm.Core.Vector;
using Microsoft.Extensions.DependencyInjection;
using Volo.Abp.Account;
using Volo.Abp.AutoMapper;
using Volo.Abp.FeatureManagement;
using Volo.Abp.Identity;
using Volo.Abp.Modularity;
using Volo.Abp.PermissionManagement;
using Volo.Abp.SettingManagement;
using Volo.Abp.TenantManagement;

namespace IIASA.WorldCereal.Rdm;

[DependsOn(
    typeof(RdmDomainModule),
    typeof(AbpAccountApplicationModule),
    typeof(RdmApplicationContractsModule),
    typeof(AbpIdentityApplicationModule),
    typeof(AbpPermissionManagementApplicationModule),
    typeof(AbpTenantManagementApplicationModule),
    typeof(AbpFeatureManagementApplicationModule),
    typeof(AbpSettingManagementApplicationModule)
)]
public class RdmApplicationModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        Configure<AbpAutoMapperOptions>(options => { options.AddMaps<RdmApplicationModule>(); });
        ConfigureAppServices(context.Services);
    }

    private void ConfigureAppServices(IServiceCollection services)
    {
        services.AddScoped<IAddCollectionHelper, AddCollectionHelper>();
        services.AddScoped<IItemsCodeStatsHelper, ItemsCodeStatsHelper>();
        services.AddScoped<IGeoJsonHelper, GeoJsonHelper>();
        services.AddHttpContextAccessor();
        services.AddScoped<IAzureFileUploader, AzureFileUploader>();
        services.AddScoped<IAccessProvider, AccessProvider>();
        services.AddScoped<IDownloadCountTableHelper, DownloadCountTableHelper>();
        services.AddScoped<MapPredictionService, MapPredictionService>();
        services.AddScoped<EmailHelper, EmailHelper>();
    }
}
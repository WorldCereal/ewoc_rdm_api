using IIASA.WorldCereal.Rdm;
using IIASA.WorldCereal.Rdm.EntityFrameworkCore;
using Volo.Abp.Autofac;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.Modularity;

namespace ConsortiumDbUpdate
{
    [DependsOn(
        typeof(AbpAutofacModule),
        typeof(RdmEntityFrameworkCoreDbMigrationsModule),
        typeof(RdmApplicationContractsModule),
        typeof(RdmApplicationModule)
    )]
    public class RdmDbConsortiumModule : AbpModule
    {
        public override void ConfigureServices(ServiceConfigurationContext context)
        {
            Configure<AbpBackgroundJobOptions>(options => options.IsJobExecutionEnabled = false);
        }
    }
}
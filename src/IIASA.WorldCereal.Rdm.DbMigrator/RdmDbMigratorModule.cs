using IIASA.WorldCereal.Rdm.EntityFrameworkCore;
using Volo.Abp.Autofac;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.Modularity;

namespace IIASA.WorldCereal.Rdm.DbMigrator
{
    [DependsOn(
        typeof(AbpAutofacModule),
        typeof(RdmEntityFrameworkCoreDbMigrationsModule),
        typeof(RdmApplicationContractsModule)
        )]
    public class RdmDbMigratorModule : AbpModule
    {
        public override void ConfigureServices(ServiceConfigurationContext context)
        {
            Configure<AbpBackgroundJobOptions>(options => options.IsJobExecutionEnabled = false);
        }
    }
}

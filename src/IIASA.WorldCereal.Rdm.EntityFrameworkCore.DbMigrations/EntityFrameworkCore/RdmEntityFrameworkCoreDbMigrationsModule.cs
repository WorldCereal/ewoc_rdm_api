using Microsoft.Extensions.DependencyInjection;
using Volo.Abp.Modularity;

namespace IIASA.WorldCereal.Rdm.EntityFrameworkCore
{
    [DependsOn(
        typeof(RdmEntityFrameworkCoreModule)
        )]
    public class RdmEntityFrameworkCoreDbMigrationsModule : AbpModule
    {
        public override void ConfigureServices(ServiceConfigurationContext context)
        {
            context.Services.AddAbpDbContext<RdmMigrationsDbContext>();
        }
    }
}

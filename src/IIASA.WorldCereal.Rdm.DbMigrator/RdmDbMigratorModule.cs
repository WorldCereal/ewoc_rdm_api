using IIASA.WorldCereal.Rdm.EntityFrameworkCore;
using Volo.Abp.Autofac;
using Volo.Abp.Modularity;

namespace IIASA.WorldCereal.Rdm.DbMigrator;

[DependsOn(
    typeof(AbpAutofacModule),
    typeof(RdmEntityFrameworkCoreModule),
    typeof(RdmApplicationContractsModule)
    )]
public class RdmDbMigratorModule : AbpModule
{
}

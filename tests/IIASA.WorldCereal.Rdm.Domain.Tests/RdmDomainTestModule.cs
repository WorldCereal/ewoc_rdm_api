using Volo.Abp.Modularity;

namespace IIASA.WorldCereal.Rdm;

[DependsOn(
    typeof(RdmDomainModule),
    typeof(RdmTestBaseModule)
)]
public class RdmDomainTestModule : AbpModule
{

}

using Volo.Abp.Modularity;

namespace IIASA.WorldCereal.Rdm
{
    [DependsOn(
        typeof(RdmApplicationModule),
        typeof(RdmDomainTestModule)
        )]
    public class RdmApplicationTestModule : AbpModule
    {

    }
}
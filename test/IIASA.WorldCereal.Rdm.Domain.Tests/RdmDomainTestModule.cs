using IIASA.WorldCereal.Rdm.EntityFrameworkCore;
using Volo.Abp.Modularity;

namespace IIASA.WorldCereal.Rdm
{
    [DependsOn(
        typeof(RdmEntityFrameworkCoreTestModule)
        )]
    public class RdmDomainTestModule : AbpModule
    {

    }
}
using Volo.Abp.Modularity;

namespace IIASA.WorldCereal.Rdm;

public abstract class RdmApplicationTestBase<TStartupModule> : RdmTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{

}

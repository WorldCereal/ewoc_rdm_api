using Volo.Abp.Modularity;

namespace IIASA.WorldCereal.Rdm;

/* Inherit from this class for your domain layer tests. */
public abstract class RdmDomainTestBase<TStartupModule> : RdmTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{

}

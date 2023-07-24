using Volo.Abp.DependencyInjection;
using Volo.Abp.Ui.Branding;

namespace IIASA.WorldCereal.Rdm
{
    [Dependency(ReplaceServices = true)]
    public class RdmBrandingProvider : DefaultBrandingProvider
    {
        public override string AppName => "Rdm";
    }
}

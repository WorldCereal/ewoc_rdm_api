using IIASA.WorldCereal.Rdm.Localization;
using Volo.Abp.Application.Services;

namespace IIASA.WorldCereal.Rdm;

/* Inherit your application services from this class.
 */
public abstract class RdmAppService : ApplicationService
{
    protected RdmAppService()
    {
        LocalizationResource = typeof(RdmResource);
    }
}

using IIASA.WorldCereal.Rdm.Localization;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp.Application.Services;

namespace IIASA.WorldCereal.Rdm
{
    /* Inherit your application services from this class.
     */
    [Route("data")]
    public abstract class RdmAppService : ApplicationService
    {
        protected RdmAppService()
        {
            LocalizationResource = typeof(RdmResource);
        }
    }
}

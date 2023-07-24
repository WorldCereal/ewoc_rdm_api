using IIASA.WorldCereal.Rdm.Localization;
using Volo.Abp.AspNetCore.Mvc;

namespace IIASA.WorldCereal.Rdm.Controllers
{
    /* Inherit your controllers from this class.
     */
    public abstract class RdmController : AbpController
    {
        protected RdmController()
        {
            LocalizationResource = typeof(RdmResource);
        }
    }
}
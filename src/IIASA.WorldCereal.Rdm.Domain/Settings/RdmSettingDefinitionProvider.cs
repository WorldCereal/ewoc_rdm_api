using Volo.Abp.Settings;

namespace IIASA.WorldCereal.Rdm.Settings
{
    public class RdmSettingDefinitionProvider : SettingDefinitionProvider
    {
        public override void Define(ISettingDefinitionContext context)
        {
            //Define your own settings here. Example:
            //context.Add(new SettingDefinition(RdmSettings.MySetting1));
        }
    }
}

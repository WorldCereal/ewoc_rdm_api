using IIASA.WorldCereal.Rdm.Localization;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.Localization;

namespace IIASA.WorldCereal.Rdm.Permissions
{
    public class RdmPermissionDefinitionProvider : PermissionDefinitionProvider
    {
        public override void Define(IPermissionDefinitionContext context)
        {
            var myGroup = context.AddGroup(RdmPermissions.GroupName);

            //Define your own permissions here. Example:
            //myGroup.AddPermission(RdmPermissions.MyPermission1, L("Permission:MyPermission1"));
        }

        private static LocalizableString L(string name)
        {
            return LocalizableString.Create<RdmResource>(name);
        }
    }
}

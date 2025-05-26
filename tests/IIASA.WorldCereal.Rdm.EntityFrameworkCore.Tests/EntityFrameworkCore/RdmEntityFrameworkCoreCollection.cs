using Xunit;

namespace IIASA.WorldCereal.Rdm.EntityFrameworkCore;

[CollectionDefinition(RdmTestConsts.CollectionDefinitionName)]
public class RdmEntityFrameworkCoreCollection : ICollectionFixture<RdmEntityFrameworkCoreFixture>
{

}

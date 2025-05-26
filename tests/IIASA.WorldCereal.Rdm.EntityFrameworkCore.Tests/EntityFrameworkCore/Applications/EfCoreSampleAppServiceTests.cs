using IIASA.WorldCereal.Rdm.Samples;
using Xunit;

namespace IIASA.WorldCereal.Rdm.EntityFrameworkCore.Applications;

[Collection(RdmTestConsts.CollectionDefinitionName)]
public class EfCoreSampleAppServiceTests : SampleAppServiceTests<RdmEntityFrameworkCoreTestModule>
{

}

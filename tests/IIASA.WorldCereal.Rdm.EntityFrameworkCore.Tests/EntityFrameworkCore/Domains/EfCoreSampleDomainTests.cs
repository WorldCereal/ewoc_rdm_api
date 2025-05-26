using IIASA.WorldCereal.Rdm.Samples;
using Xunit;

namespace IIASA.WorldCereal.Rdm.EntityFrameworkCore.Domains;

[Collection(RdmTestConsts.CollectionDefinitionName)]
public class EfCoreSampleDomainTests : SampleDomainTests<RdmEntityFrameworkCoreTestModule>
{

}

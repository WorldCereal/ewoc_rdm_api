using System.Threading.Tasks;
using Volo.Abp.DependencyInjection;

namespace IIASA.WorldCereal.Rdm.Data
{
    /* This is used if database provider does't define
     * IRdmDbSchemaMigrator implementation.
     */
    public class NullRdmDbSchemaMigrator : IRdmDbSchemaMigrator, ITransientDependency
    {
        public Task MigrateAsync()
        {
            return Task.CompletedTask;
        }
    }
}
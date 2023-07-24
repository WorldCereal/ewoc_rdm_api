using System.Threading.Tasks;

namespace IIASA.WorldCereal.Rdm.Data
{
    public interface IRdmDbSchemaMigrator
    {
        Task MigrateAsync();
    }
}

using IIASA.WorldCereal.Rdm.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace IIASA.Db.UpdateTools.core
{
    public class UpdaterContext : RdmDbContext
    {
        public UpdaterContext(Configuration configuration,bool loadMaster) : base(GetDbContextOptions(configuration, loadMaster))
        {
        }
    
        private static DbContextOptions<RdmDbContext> GetDbContextOptions(Configuration configuration, bool loadMaster)
        {
            DbContextOptionsBuilder<RdmDbContext> picturePileDbContext =
                new DbContextOptionsBuilder<RdmDbContext>();
            var connectionString = loadMaster
                ? GetMasterConnectionString(configuration)
                : GetCollectionConnectionString(configuration);
            picturePileDbContext.UseNpgsql(connectionString, x =>
            {
                x.UseNetTopologySuite();
            });
            return picturePileDbContext.Options;
        }

        private static string GetCollectionConnectionString(Configuration configuration)
        {
            return configuration.ConnectionString+$";database={configuration.CollectionId};";
        }

        private static string GetMasterConnectionString(Configuration configuration)
        {
            return configuration.ConnectionString + $";database={configuration.MasterDb};";
        }
    }
}
using IIASA.WorldCereal.Rdm.Entity;
using Microsoft.EntityFrameworkCore;
using IIASA.WorldCereal.Rdm.Users;
using Volo.Abp.Data;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore.Modeling;
using Volo.Abp.Identity;
using Volo.Abp.Users.EntityFrameworkCore;

namespace IIASA.WorldCereal.Rdm.EntityFrameworkCore
{
    /* This is your actual DbContext used on runtime.
     * It includes only your entities.
     * It does not include entities of the used modules, because each module has already
     * its own DbContext class. If you want to share some database tables with the used modules,
     * just create a structure like done for AppUser.
     *
     * Don't use this DbContext for database migrations since it does not contain tables of the
     * used modules (as explained above). See RdmMigrationsDbContext for migrations.
     */
    [ConnectionStringName("Default")]
    public class RdmDbContext : AbpDbContext<RdmDbContext>
    {
        public DbSet<AppUser> Users { get; set; }

        /* Add DbSet properties for your Aggregate Roots / Entities here.
         * Also map them inside RdmDbContextModelCreatingExtensions.ConfigureRdm
         */
        public DbSet<CollectionMetadataEntity> CollectionMetadataEntities { get; set; }
        public DbSet<StoreEntity> Stores { get; set; }
        public DbSet<ItemEntity> Items { get; set; }
        public DbSet<SampleEntity> Samples { get; set; }
        public DbSet<ValidationRule> ValidationRules { get; set; }
        public DbSet<RuleValidValue> RuleValidValues { get; set; }
        public DbSet<UserDataset> UserDataset { get; set; }
        public DbSet<UserDatasetBackup> UserDatasetBackups { get; set; }
        public DbSet<DatasetEvent> DatasetEvents { get; set; }
        public DbSet<MetadataItem> MetadataItems { get; set; }
        public DbSet<ConsortiumCollection> ConsortiumDatasets { get; set; }

        public RdmDbContext(DbContextOptions<RdmDbContext> options)
            : base(options)
        {
        }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            /* Configure the shared tables (with included modules) here */

            builder.Entity<AppUser>(b =>
            {
                b.ToTable(AbpIdentityDbProperties.DbTablePrefix +
                          "Users"); //Sharing the same table "AbpUsers" with the IdentityUser

                b.ConfigureByConvention();
                b.ConfigureAbpUser();

                /* Configure mappings for your additional properties
                 * Also see the RdmEfCoreEntityExtensionMappings class
                 */
            });

            /* Configure your own tables/entities inside the ConfigureRdm method */

            builder.ConfigureRdm();
        }
        
        /*protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.EnableSensitiveDataLogging(true); // enable for more logs
            base.OnConfiguring(optionsBuilder);
        }*/
    }
}
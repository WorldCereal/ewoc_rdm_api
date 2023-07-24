using IIASA.WorldCereal.Rdm.Entity;
using Microsoft.EntityFrameworkCore;
using Volo.Abp;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace IIASA.WorldCereal.Rdm.EntityFrameworkCore
{
    public static class RdmDbContextModelCreatingExtensions
    {
        public static void ConfigureRdm(this ModelBuilder builder)
        {
            Check.NotNull(builder, nameof(builder));

            /* Configure your own tables/entities inside here */

            //builder.Entity<YourEntity>(b =>
            //{
            //    b.ToTable(RdmConsts.DbTablePrefix + "YourEntities", RdmConsts.DbSchema);
            //    b.ConfigureByConvention(); //auto configure for the base class props
            //    //...
            //});
            
            builder.Entity<CollectionMetadataEntity>(b =>
            {
                b.ToTable(RdmConsts.DbTablePrefix + "CollectionMetadata", RdmConsts.DbSchema);
                b.ConfigureByConvention(); //auto configure for the base class props
                //...
            });

            builder.Entity<StoreEntity>(b =>
            {
                b.ToTable(RdmConsts.DbTablePrefix + "Store", RdmConsts.DbSchema);
                b.ConfigureByConvention(); //auto configure for the base class props
                //...
            });

            builder.Entity<SampleEntity>(b =>
            {
                b.ToTable(RdmConsts.DbTablePrefix + "Sample", RdmConsts.DbSchema);
                b.ConfigureByConvention(); //auto configure for the base class props
                //...
            });

            builder.Entity<ValidationRule>(b =>
            {
                b.ToTable(RdmConsts.DbTablePrefix + "ValidationRules", RdmConsts.DbSchema);
                b.ConfigureByConvention(); //auto configure for the base class props
                //...
            });

            builder.Entity<RuleValidValue>(b =>
            {
                b.ToTable(RdmConsts.DbTablePrefix + "RuleValidValues", RdmConsts.DbSchema);
                b.ConfigureByConvention(); //auto configure for the base class props
                //...
            });

            builder.Entity<UserDataset>(b =>
            {
                b.ToTable(RdmConsts.DbTablePrefix + "UserDatasets", RdmConsts.DbSchema);
                b.ConfigureByConvention(); //auto configure for the base class props
                //...
            });
            
            builder.Entity<UserDatasetBackup>(b =>
            {
                b.ToTable(RdmConsts.DbTablePrefix + "UserDatasetBackups", RdmConsts.DbSchema);
                b.ConfigureByConvention(); //auto configure for the base class props
                //...
            });
            
            builder.Entity<DatasetEvent>(b =>
            {
                b.ToTable(RdmConsts.DbTablePrefix + "DatasetEvents", RdmConsts.DbSchema);
                b.ConfigureByConvention(); //auto configure for the base class props
                //...
            });

            builder.Entity<ItemEntity>(b =>
            {
                b.ToTable(RdmConsts.DbTablePrefix + "Items", RdmConsts.DbSchema);
                b.ConfigureByConvention(); //auto configure for the base class props
                b.HasIndex(nameof(ItemEntity.Lc), 
                    nameof(ItemEntity.Ct),
                    nameof(ItemEntity.Irr),
                    nameof(ItemEntity.UserConf),
                    nameof(ItemEntity.Area),
                    nameof(ItemEntity.Split),
                    nameof(ItemEntity.ValidityTime));
                //...
            });
            
            builder.Entity<MetadataItem>(b =>
            {
                b.ToTable(RdmConsts.DbTablePrefix + "MetadataItems", RdmConsts.DbSchema);
                b.ConfigureByConvention(); //auto configure for the base class props
                //...
            });
            
            builder.Entity<ConsortiumCollection>(b =>
            {
                b.ToTable(RdmConsts.DbTablePrefix + "ConsortiumColStatus", RdmConsts.DbSchema);
                b.ConfigureByConvention(); //auto configure for the base class props
                //...
            });
        }
    }
}
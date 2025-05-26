using IIASA.WorldCereal.Rdm.Entity;
using Microsoft.EntityFrameworkCore;
using Volo.Abp.AuditLogging.EntityFrameworkCore;
using Volo.Abp.BackgroundJobs.EntityFrameworkCore;
using Volo.Abp.Data;
using Volo.Abp.DependencyInjection;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore.Modeling;
using Volo.Abp.FeatureManagement.EntityFrameworkCore;
using Volo.Abp.Identity;
using Volo.Abp.Identity.EntityFrameworkCore;
using Volo.Abp.OpenIddict.EntityFrameworkCore;
using Volo.Abp.PermissionManagement.EntityFrameworkCore;
using Volo.Abp.SettingManagement.EntityFrameworkCore;
using Volo.Abp.TenantManagement;
using Volo.Abp.TenantManagement.EntityFrameworkCore;

namespace IIASA.WorldCereal.Rdm.EntityFrameworkCore;

[ReplaceDbContext(typeof(IIdentityDbContext))]
[ReplaceDbContext(typeof(ITenantManagementDbContext))]
[ConnectionStringName("Default")]
public class RdmDbContext :
    AbpDbContext<RdmDbContext>,
    IIdentityDbContext,
    ITenantManagementDbContext
{
    /* Add DbSet properties for your Aggregate Roots / Entities here. */

    #region Entities from the modules

    /* Notice: We only implemented IIdentityDbContext and ITenantManagementDbContext
     * and replaced them for this DbContext. This allows you to perform JOIN
     * queries for the entities of these modules over the repositories easily. You
     * typically don't need that for other modules. But, if you need, you can
     * implement the DbContext interface of the needed module and use ReplaceDbContext
     * attribute just like IIdentityDbContext and ITenantManagementDbContext.
     *
     * More info: Replacing a DbContext of a module ensures that the related module
     * uses this DbContext on runtime. Otherwise, it will use its own DbContext class.
     */

    //Identity
    public DbSet<IdentityUser> Users { get; set; }
    public DbSet<IdentityRole> Roles { get; set; }
    public DbSet<IdentityClaimType> ClaimTypes { get; set; }
    public DbSet<OrganizationUnit> OrganizationUnits { get; set; }
    public DbSet<IdentitySecurityLog> SecurityLogs { get; set; }
    public DbSet<IdentityLinkUser> LinkUsers { get; set; }
    public DbSet<IdentityUserDelegation> UserDelegations { get; set; }

    // Tenant Management
    public DbSet<Tenant> Tenants { get; set; }
    public DbSet<TenantConnectionString> TenantConnectionStrings { get; set; }

    #endregion
    
    public DbSet<CollectionMetadataEntity> CollectionMetadataEntities { get; set; }
    public DbSet<StoreEntity> Stores { get; set; }
    public DbSet<ItemEntity> Items { get; set; }
    public DbSet<ValidationRule> ValidationRules { get; set; }
    public DbSet<EoType> EoTypes { get; set; }
    public DbSet<RuleValidValue> RuleValidValues { get; set; }
    public DbSet<UserDataset> UserDataset { get; set; }
    public DbSet<UserDatasetBackup> UserDatasetBackups { get; set; }
    public DbSet<DatasetEvent> DatasetEvents { get; set; }
    public DbSet<MetadataItem> MetadataItems { get; set; }
    public DbSet<ConsortiumCollection> ConsortiumDatasets { get; set; }
    public DbSet<ExternalUserAccount> ExternalUserAccounts { get; set; }

    public RdmDbContext(DbContextOptions<RdmDbContext> options)
        : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        /* Include modules to your migration db context */

        builder.ConfigurePermissionManagement();
        builder.ConfigureSettingManagement();
        builder.ConfigureBackgroundJobs();
        builder.ConfigureAuditLogging();
        builder.ConfigureIdentity();
        builder.ConfigureOpenIddict();
        builder.ConfigureFeatureManagement();
        builder.ConfigureTenantManagement();

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

        builder.Entity<ValidationRule>(b =>
        {
            b.ToTable(RdmConsts.DbTablePrefix + "ValidationRules", RdmConsts.DbSchema);
            b.ConfigureByConvention(); //auto configure for the base class props
            //...
        });
        
        builder.Entity<EoType>(b =>
        {
            b.ToTable(RdmConsts.DbTablePrefix + "EoTypes", RdmConsts.DbSchema);
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
            b.HasIndex(
                nameof(ItemEntity.EwocCode),
                nameof(ItemEntity.IrrigationStatus),
                nameof(ItemEntity.QualityScore),
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
        
        builder.Entity<ExternalUserAccount>(b =>
        {
            b.ToTable(RdmConsts.DbTablePrefix + "ExternalUserAccounts", RdmConsts.DbSchema);
            b.ConfigureByConvention(); //auto configure for the base class props
            //...
        });
    }
}
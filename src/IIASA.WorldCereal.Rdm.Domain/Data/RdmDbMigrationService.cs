using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using IIASA.WorldCereal.Rdm.Entity;
using IIASA.WorldCereal.Rdm.Enums;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Newtonsoft.Json;
using Volo.Abp.Data;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.TenantManagement;
using Volo.Abp.Uow;

namespace IIASA.WorldCereal.Rdm.Data
{
    public class RdmDbMigrationService : ITransientDependency
    {
        public ILogger<RdmDbMigrationService> Logger { get; set; }

        private readonly IDataSeeder _dataSeeder;
        private readonly IEnumerable<IRdmDbSchemaMigrator> _dbSchemaMigrators;
        private readonly ITenantRepository _tenantRepository;
        private readonly IConfiguration _configuration;
        private readonly IRepository<StoreEntity> _storeRepository;
        private readonly IRepository<ValidationRule, int> _validationRulesRepository;
        private readonly IRepository<ConsortiumCollection, int> _consortiumCollections;
        private readonly ICurrentTenant _currentTenant;

        public RdmDbMigrationService(
            IDataSeeder dataSeeder,
            IEnumerable<IRdmDbSchemaMigrator> dbSchemaMigrators,
            ITenantRepository tenantRepository, IConfiguration configuration,
            IRepository<StoreEntity> storeRepository,
            IRepository<ValidationRule, int> validationRulesRepository,
            IRepository<ConsortiumCollection, int> consortiumCollections,
            ICurrentTenant currentTenant)
        {
            _dataSeeder = dataSeeder;
            _dbSchemaMigrators = dbSchemaMigrators;
            _tenantRepository = tenantRepository;
            _configuration = configuration;
            _storeRepository = storeRepository;
            _validationRulesRepository = validationRulesRepository;
            _consortiumCollections = consortiumCollections;
            _currentTenant = currentTenant;

            Logger = NullLogger<RdmDbMigrationService>.Instance;
        }

        public async Task MigrateAsync()
        {
            Logger.LogInformation("Starting the Migration Service of RDM (AWS)");
            Logger.LogInformation("---------------------------------------------------");
            Logger.LogInformation("Below is the RefDB connection string");
            Logger.LogInformation($"{GetStoreConnectionString(0)}");
            Logger.LogInformation("Below is the ComDB connection string");
            Logger.LogInformation($"{GetStoreConnectionString(1)}");
            Logger.LogInformation("---------------------------------------------------");
            
            var onlyRefDbUpdate = _configuration.GetValue<bool>("OnlyRefDbUpdate");
            if (onlyRefDbUpdate)
            {
                Logger.LogInformation("Updating only ref db list...");
                await SeedRefDatasets();
                return;
            }
            
            var initialMigrationAdded = AddInitialMigrationIfNotExist();

            if (initialMigrationAdded)
            {
                return;
            }

            Logger.LogInformation("Started database migrations...");

            await MigrateDatabaseSchemaAsync();
            await SeedDataAsync();
            await SeedValidationRules();
            await SeedStoreTypes();
            await SeedRefDatasets();
            Logger.LogInformation($"Successfully completed host database migrations.");

            var tenants = await _tenantRepository.GetListAsync(includeDetails: true);

            var migratedDatabaseSchemas = new HashSet<string>();
            foreach (var tenant in tenants)
            {
                using (_currentTenant.Change(tenant.Id))
                {
                    if (tenant.ConnectionStrings.Any())
                    {
                        var tenantConnectionStrings = tenant.ConnectionStrings
                            .Select(x => x.Value)
                            .ToList();

                        if (!migratedDatabaseSchemas.IsSupersetOf(tenantConnectionStrings))
                        {
                            await MigrateDatabaseSchemaAsync(tenant);

                            migratedDatabaseSchemas.AddIfNotContains(tenantConnectionStrings);
                        }
                    }

                    await SeedDataAsync(tenant);
                }

                Logger.LogInformation($"Successfully completed {tenant.Name} tenant database migrations.");
            }

            Logger.LogInformation("Successfully completed all database migrations");
            Logger.LogInformation("You can safely end this process...");
        }

        private async Task MigrateDatabaseSchemaAsync(Tenant tenant = null)
        {
            Logger.LogInformation(
                $"Migrating schema for {(tenant == null ? "host" : tenant.Name + " tenant")} database...");

            foreach (var migrator in _dbSchemaMigrators)
            {
                await migrator.MigrateAsync();
            }
        }

        private async Task SeedDataAsync(Tenant tenant = null)
        {
            Logger.LogInformation($"Executing {(tenant == null ? "host" : tenant.Name + " tenant")} database seed...");

            await _dataSeeder.SeedAsync(new DataSeedContext(tenant?.Id)
                .WithProperty(IdentityDataSeedContributor.AdminEmailPropertyName,
                    IdentityDataSeedContributor.AdminEmailDefaultValue)
                .WithProperty(IdentityDataSeedContributor.AdminPasswordPropertyName,
                    IdentityDataSeedContributor.AdminPasswordDefaultValue)
            );
        }

        private bool AddInitialMigrationIfNotExist()
        {
            try
            {
                if (!DbMigrationsProjectExists())
                {
                    return false;
                }
            }
            catch (Exception)
            {
                return false;
            }

            try
            {
                if (!MigrationsFolderExists())
                {
                    AddInitialMigration();
                    return true;
                }
                else
                {
                    return false;
                }
            }
            catch (Exception e)
            {
                Logger.LogWarning("Couldn't determinate if any migrations exist : " + e.Message);
                return false;
            }
        }

        private bool DbMigrationsProjectExists()
        {
            var dbMigrationsProjectFolder = GetDbMigrationsProjectFolderPath();

            return dbMigrationsProjectFolder != null;
        }

        private bool MigrationsFolderExists()
        {
            var dbMigrationsProjectFolder = GetDbMigrationsProjectFolderPath();

            return Directory.Exists(Path.Combine(dbMigrationsProjectFolder, "Migrations"));
        }

        private void AddInitialMigration()
        {
            Logger.LogInformation("Creating initial migration...");

            string argumentPrefix;
            string fileName;

            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX) || RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                argumentPrefix = "-c";
                fileName = "/bin/bash";
            }
            else
            {
                argumentPrefix = "/C";
                fileName = "cmd.exe";
            }

            var procStartInfo = new ProcessStartInfo(fileName,
                $"{argumentPrefix} \"abp create-migration-and-run-migrator \"{GetDbMigrationsProjectFolderPath()}\"\""
            );

            try
            {
                Process.Start(procStartInfo);
            }
            catch (Exception)
            {
                throw new Exception("Couldn't run ABP CLI...");
            }
        }

        private string GetDbMigrationsProjectFolderPath()
        {
            var slnDirectoryPath = GetSolutionDirectoryPath();

            if (slnDirectoryPath == null)
            {
                throw new Exception("Solution folder not found!");
            }

            var srcDirectoryPath = Path.Combine(slnDirectoryPath, "src");

            return Directory.GetDirectories(srcDirectoryPath)
                .FirstOrDefault(d => d.EndsWith(".DbMigrations"));
        }

        private string GetSolutionDirectoryPath()
        {
            var currentDirectory = new DirectoryInfo(Directory.GetCurrentDirectory());

            while (Directory.GetParent(currentDirectory.FullName) != null)
            {
                currentDirectory = Directory.GetParent(currentDirectory.FullName);

                if (Directory.GetFiles(currentDirectory.FullName).FirstOrDefault(f => f.EndsWith(".sln")) != null)
                {
                    return currentDirectory.FullName;
                }
            }

            return null;
        }

        [UnitOfWork]
        public virtual async Task SeedValidationRules()
        {
            var rules = _validationRulesRepository.ToList();

            var seedRules = ValidationRuleDataSeeder.Get();

            foreach (var seedRule in seedRules)
            {
                if (rules.Any(x => x.AttributeName == seedRule.AttributeName) == false)
                {
                    Logger.LogInformation(
                        $"Adding seed data- Rules - {seedRule.AttributeName} - validValuesCount- {seedRule.RuleValidValues.Count}");
                    await _validationRulesRepository.InsertAsync(seedRule);
                }
            }
        }

        private string GetStoreConnectionString(int type)
        {
            var connectionDef = new DbConfig();
            _configuration.GetSection("ConnectionStrings").Bind(connectionDef);
            var connectionData = type == 0
                ? connectionDef.RefDb.Split(";", StringSplitOptions.RemoveEmptyEntries)
                : connectionDef.ComDb.Split(";", StringSplitOptions.RemoveEmptyEntries);
            var storeConnectionStrings = connectionData.Where(x => !x.Contains("database"));
            return string.Join(";", storeConnectionStrings) + ";";
        }

        [UnitOfWork]
        public virtual async Task SeedStoreTypes()
        {
            if (_storeRepository.Any())
            {
                Logger.LogInformation("Default stores already added. Skip add stores");
                return;
            }

            Logger.LogInformation("Started adding AddStoreTypes...");
            var refDb = new StoreEntity
            {
                StoreType = StoreType.Reference, ConnectionString = GetStoreConnectionString(0), Name = "Consortium DB"
            };
            var communityDb = new StoreEntity
                { StoreType = StoreType.Community, ConnectionString = GetStoreConnectionString(1), Name = "User Db" };
            var storeEntities = new List<StoreEntity> { refDb, communityDb };
            await _storeRepository.InsertManyAsync(storeEntities);
            Logger.LogInformation("Completed adding AddStoreTypes...");
        }

        [UnitOfWork]
        public virtual async Task SeedRefDatasets()
        {
            Logger.LogInformation("Started SeedRefDatasets...");
            var baseUrl = _configuration.GetValue<string>("BlobBaseUrl");
            var urls = (await File.ReadAllLinesAsync(GetFile("RefDatasets.txt")))
                .Select(x => baseUrl + x.Trim())
                .ToArray();
            var listToInsert = new List<ConsortiumCollection>();
            foreach (var url in urls)
            {
                if (await _consortiumCollections.AnyAsync(x => x.DownloadZipUrl == url))
                {
                    continue;
                }

                Logger.LogInformation($"SeedRefDatasets: adding {url} to db");
                listToInsert.Add(new ConsortiumCollection { DownloadZipUrl = url, NeedsUpdate = true });
            }

            if (listToInsert.Any())
            {
                Logger.LogInformation("SeedRefDatasets: Saving to database...");
                await _consortiumCollections.InsertManyAsync(listToInsert);
                Logger.LogInformation("Completed SeedRefDatasets");
            }
        }

        public static string GetFile(string fileName)
        {
            var files = Directory.GetFiles(Directory.GetCurrentDirectory(), fileName,
                SearchOption.AllDirectories);
            var file = files.First();
            return file;
        }
    }

    public class DbConfig
    {
        public string Default { get; set; }
        public string RefDb { get; set; }
        public string ComDb { get; set; }
    }
}
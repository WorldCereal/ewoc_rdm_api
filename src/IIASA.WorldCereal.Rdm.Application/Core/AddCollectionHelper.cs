using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using IIASA.WorldCereal.Rdm.Entity;
using IIASA.WorldCereal.Rdm.EntityFrameworkCore;
using IIASA.WorldCereal.Rdm.Enums;
using IIASA.WorldCereal.Rdm.ExcelOps;
using IIASA.WorldCereal.Rdm.Mappings;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NetTopologySuite.Geometries;
using NetTopologySuite.IO;
using Npgsql;
using Volo.Abp.Data;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.TenantManagement;
using Volo.Abp.Uow;

namespace IIASA.WorldCereal.Rdm.Core
{
    public class AddCollectionHelper : IAddCollectionHelper
    {
        private readonly ITenantRepository _tenantRepository;
        private readonly ITenantManager _tenantManager;
        private readonly ILogger<AddCollectionHelper> _logger;
        private readonly IRepository<StoreEntity, Guid> _storeRepository;
        private readonly IRepository<UserDatasetBackup, Guid> _userDatasetBackups;
        private readonly IRepository<CollectionMetadataEntity, Guid> _masterCollectionStoreRepository;

        public AddCollectionHelper(ITenantRepository tenantRepository, ITenantManager tenantManager,
            ILogger<AddCollectionHelper> logger
            , IRepository<StoreEntity, Guid> storeRepository, IRepository<UserDatasetBackup, Guid> userDatasetBackups
            , IRepository<CollectionMetadataEntity, Guid> masterCollectionStoreRepository)
        {
            _tenantRepository = tenantRepository;
            _tenantManager = tenantManager;
            _logger = logger;
            _storeRepository = storeRepository;
            _userDatasetBackups = userDatasetBackups;
            _masterCollectionStoreRepository = masterCollectionStoreRepository;
        }

        public async Task<CollectionMetadataEntity> CreateMasterCollectionStoreInfo(
            CollectionMetadataEntity masterCollectionStoreInfo, IUnitOfWork currentUnitOfWork)
        {
            var collectionId = masterCollectionStoreInfo.CollectionId;
            await CheckName(collectionId);
            var store = await GetStoreEntity(masterCollectionStoreInfo.StoreType);

            var connectionString = await CreateDatabaseAsync(store.ConnectionString, collectionId);
            var tenant = await _tenantManager.CreateAsync(collectionId);
            tenant.SetDefaultConnectionString(connectionString);
            tenant.SetProperty("storeId", store.Id);
            tenant.SetProperty("storeName", store.Name);
            await _tenantRepository.InsertAsync(tenant);
            await currentUnitOfWork.SaveChangesAsync();

            await MigrateCollectionDb(connectionString);

            masterCollectionStoreInfo.TenantId = null;
            return await _masterCollectionStoreRepository.InsertAsync(masterCollectionStoreInfo);
        }

        public async Task<Geometry?> GetExtentFromDb(string collectionId, StoreType storeType)
        {
            var store = await GetStoreEntity(storeType);

            var dbName = GetDbName(collectionId);

            var connStr = store.ConnectionString + $"Database={dbName};";

            using (var connection = new NpgsqlConnection(connStr))
            {
                //TODO take user "postgres" from config
                var cmdText =
                    "SELECT st_astext(ST_Envelope(ST_Union(ST_Envelope(\"Geometry\")))) AS table_extent FROM \"Rdm_Items\" where \"Extract\" = 1;";

                var createDbCmd = new NpgsqlCommand(cmdText, connection);
                connection.Open();
                try
                {
                    var extentValue = await createDbCmd.ExecuteScalarAsync();
                    var reader = new WKTReader();
                    // Read the WKT string into a Geometry object
                    var geometry = reader.Read(extentValue?.ToString());
                    // Output the geometry
                    Console.WriteLine($"Geometry: {geometry}");
                    return geometry;
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                }

                await connection.CloseAsync();
            }

            return null;
        }

        public static string GetDbName(string collectionId)
        {
            return $"wc{collectionId}";
        }

        public async Task<CollectionMetadataEntity> GetMasterCollectionStoreInfo(string collectionId)
        {
            return await _masterCollectionStoreRepository.FirstOrDefaultAsync(x =>
                x.CollectionId == collectionId);
        }

        public async Task SaveMasterCollectionStoreInfoAsync(CollectionMetadataEntity data)
        {
            data.TenantId = null;
            await _masterCollectionStoreRepository.UpdateAsync(data);
        }

        public async Task<CollectionMetadataEntity> CreateMasterCollectionStoreInfo(
            CollectionMetadataEntity masterCollectionStoreInfo)
        {
            var collectionId = masterCollectionStoreInfo.CollectionId;
            await CheckName(collectionId);
            var store = await GetStoreEntity(masterCollectionStoreInfo.StoreType);

            var connectionString = await CreateDatabaseAsync(store.ConnectionString, collectionId);
            var tenant = await _tenantManager.CreateAsync(collectionId);
            tenant.SetDefaultConnectionString(connectionString);
            tenant.SetProperty("storeId", store.Id);
            tenant.SetProperty("storeName", store.Name);
            await _tenantRepository.InsertAsync(tenant);

            await MigrateCollectionDb(connectionString);

            store.Count++;
            await _storeRepository.UpdateAsync(store);
            return await _masterCollectionStoreRepository.InsertAsync(masterCollectionStoreInfo);
        }

        public async Task DeleteUserDataset(string collectionId)
        {
            if (await _masterCollectionStoreRepository.AnyAsync(x => x.CollectionId == collectionId))
            {
                var masterCollectionStoreInfo =
                    await _masterCollectionStoreRepository.GetAsync(x => x.CollectionId == collectionId);
                var store = await GetStoreEntity(masterCollectionStoreInfo.StoreType);
                try
                {
                    await DeleteDatabaseAsync(store.ConnectionString, collectionId);
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                    throw;
                }

                await _masterCollectionStoreRepository.DeleteAsync(x => x.CollectionId == collectionId);
            }

            var tenant = await _tenantRepository.FindByNameAsync(collectionId);
            if (tenant != default)
            {
                await _tenantRepository.DeleteAsync(tenant);
            }

            var backups = await (await _userDatasetBackups.GetQueryableAsync())
                .Where(x => x.CollectionId == collectionId).ToListAsync();
            foreach (var datasetBackup in backups)
            {
                CleanFiles(datasetBackup.ZipFilePath);
            }

            await _userDatasetBackups.DeleteManyAsync(backups);
        }

        private void CleanFiles(string directoryPathToClean)
        {
            try
            {
                Directory.Delete(directoryPathToClean, true);
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Error while deleting files");
            }
        }

        private static async Task MigrateCollectionDb(string connectionString)
        {
            DbContextOptionsBuilder<RdmDbContext> contextBuilder =
                new DbContextOptionsBuilder<RdmDbContext>();
            contextBuilder.UseNpgsql(connectionString, x => x.UseNetTopologySuite());
            var db = new RdmDbContext(contextBuilder.Options);
            await db.Database.MigrateAsync();
        }

        private async Task<StoreEntity> GetStoreEntity(StoreType storeType)
        {
            var store = await (await _storeRepository.GetQueryableAsync()).Where(x => x.StoreType == storeType)
                .OrderBy(x => x.Count).FirstOrDefaultAsync();
            if (store == null)
            {
                throw new EntityNotFoundException();
            }

            return store;
        }

        private async Task CheckName(string collectionId)
        {
            var tenant = await _tenantRepository.FindByNameAsync(collectionId);
            if (tenant != null)
            {
                throw new DuplicateNameException(
                    $"Collection with ID-{collectionId} already added.");
            }
        }

        private async Task DeleteDatabaseAsync(string connectionString, string colId)
        {
            var dbName = GetDbName(colId);
            string connStr = connectionString;

            var connection = new NpgsqlConnection(connStr);
            var cmdText = $@"
    UPDATE pg_database SET datallowconn = 'false' WHERE datname = '{dbName}';
    SELECT pg_terminate_backend(pid)
    FROM pg_stat_activity
    WHERE datname = '{dbName}';           
    ";
            var createDbCmd = new NpgsqlCommand(cmdText, connection);
            await connection.OpenAsync();
            await createDbCmd.ExecuteNonQueryAsync();
            await connection.CloseAsync();

            var dropCmd = $"drop database  {dbName};";
            var dropDbCmd = new NpgsqlCommand(dropCmd, connection);

            await connection.OpenAsync();
            await dropDbCmd.ExecuteNonQueryAsync();

            await connection.ReloadTypesAsync();
            await connection.CloseAsync();
            await connection.DisposeAsync();
        }

        private static async Task<string> CreateDatabaseAsync(string connectionString, string colId)
        {
            var dbName = GetDbName(colId);
            string connStr = connectionString;

            // create db
            using (var connection = new NpgsqlConnection(connStr))
            {
                //TODO take user "postgres" from config
                var cmdText = $@"
    CREATE DATABASE {dbName}
    WITH OWNER = postgres
    ENCODING = 'UTF8'
    CONNECTION LIMIT = -1;
    ";
                var createDbCmd = new NpgsqlCommand(cmdText, connection);
                connection.Open();
                try
                {
                    await createDbCmd.ExecuteNonQueryAsync();
                    connection.ReloadTypes();
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                }

                connection.Close();
            }

            connStr = connStr + $"Database={dbName};";
            // create extension
            var maxRetryCount = 3;
            for (int i = 0; i < maxRetryCount; i++)
            {
                try
                {
                    await CreatePostgisExtensionAsync(dbName, connStr);
                    break;
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                    if (i < maxRetryCount - 1)
                    {
                        Thread.Sleep(TimeSpan.FromSeconds(60));
                        continue;
                    }

                    throw;
                }
            }

            return connStr;
        }

        private static async Task CreatePostgisExtensionAsync(string dbName, string connStr)
        {
            using (var connection = new NpgsqlConnection(connStr))
            {
                var extensionPostgis = "CREATE EXTENSION if not exists POSTGIS;";
                var command = new NpgsqlCommand(extensionPostgis, connection);
                connection.Open();
                await command.ExecuteNonQueryAsync();
                connection.ReloadTypes();
                connection.Close();
            }
        }

        public static IList<MetadataItem> GetCollectionDefaultItems(CollectionMetadataEntity data)
        {
            return new List<MetadataItem>
            {
                new() { Name = ExcelMarkers.DatasetCollectionId, Value = data.CollectionId },
                new() { Name = ExcelMarkers.DatasetTitle, Value = data.Title },
                new()
                {
                    Name = ExcelMarkers.GeometryPointOrPolygonOrRaster,
                    Value = GetDsType(data)
                },
                new()
                {
                    Name = ExcelMarkers.NoOfObservations,
                    Value = data.FeatureCount.ToString()
                },
                new()
                {
                    Name = ExcelMarkers.ListLandCovers,
                    Value = string.Join(";",
                        data.EwocCodes.Select(x => x == 0 ? 0 : int.Parse(x.ToString().Substring(0, 2))).Distinct())
                },
                new()
                {
                    Name = ExcelMarkers.ListOfCropTypes,
                    Value = string.Join(";", data.EwocCodes)
                },
                new()
                {
                    Name = ExcelMarkers.ListOfIrrigationCodes,
                    Value = string.Join(";", data.IrrTypes)
                },
                new()
                {
                    Name = ExcelMarkers.GeometryBoundingBoxLl,
                    Value = SpatialMappingHelper.GetLowerLeftCoordinates(data.Extent)
                },
                new()
                {
                    Name = ExcelMarkers.GeometryBoundingBoxUr,
                    Value = SpatialMappingHelper.GetUpperRightCoordinates(data.Extent)
                },
                new()
                {
                    Name = ExcelMarkers.FirstDateObservation,
                    Value = data.FirstDateOfValidityTime.ToString("d/M/yyyy")
                },
                new()
                {
                    Name = ExcelMarkers.LastDateObservation,
                    Value = data.LastDateOfValidityTime.ToString("d/M/yyyy")
                },
            };
        }

        private static string GetDsType(CollectionMetadataEntity data)
        {
            if (data.Type == CollectionType.ClassifiedMap)
            {
                return "Raster";
            }

            if (data.Type == CollectionType.Point)
            {
                return "Point";
            }

            return "Polygon";
        }
    }
}
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using IIASA.WorldCereal.Rdm.Core;
using IIASA.WorldCereal.Rdm.Dtos;
using IIASA.WorldCereal.Rdm.Entity;
using IIASA.WorldCereal.Rdm.Enums;
using IIASA.WorldCereal.Rdm.Interfaces;
using IIASA.WorldCereal.Rdm.Jobs.Validation;
using IIASA.WorldCereal.Rdm.ServiceConfigs;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Volo.Abp.TenantManagement;

namespace IIASA.WorldCereal.Rdm.Services
{
    [RemoteService(Name = "OGC")]
    [ApiExplorerSettings(GroupName = "OGC")]
    public class UserDatasetService : RdmAppService, IUserDatasetService
    {
        private readonly IRepository<UserDataset, Guid> _userDatasetRepository;
        private readonly IBackgroundJobManager _backgroundJobManager;
        private readonly UserDatasetConfig _userDatasetConfig;
        private readonly ILogger<UserDatasetService> _logger;
        private readonly IAddCollectionHelper _addCollectionHelper;
        private readonly IRepository<MetadataItem, int> _metadataItemsRepository;
        private readonly ITenantRepository _tenantRepository;
        private readonly ICurrentTenant _currentTenant;
        private readonly IEwocUser _ewocUser;
        private readonly IRepository<CollectionMetadataEntity, Guid> _collectionRepository;

        public UserDatasetService(IRepository<UserDataset, Guid> userDatasetRepository,
            IBackgroundJobManager backgroundJobManager, ILogger<UserDatasetService> logger,
            IAddCollectionHelper addCollectionHelper,
            IRepository<MetadataItem, int> metadataItemsRepository,
            ITenantRepository tenantRepository,
            IRepository<CollectionMetadataEntity, Guid> collectionRepository,
            ICurrentTenant currentTenant, 
            IEwocUser ewocUser,
            UserDatasetConfig userDatasetConfig)
        {
            _userDatasetRepository = userDatasetRepository;
            _backgroundJobManager = backgroundJobManager;
            _userDatasetConfig = userDatasetConfig;
            _logger = logger;
            _addCollectionHelper = addCollectionHelper;
            _metadataItemsRepository = metadataItemsRepository;
            _tenantRepository = tenantRepository;
            _currentTenant = currentTenant;
            _ewocUser = ewocUser;
            _collectionRepository = collectionRepository;
        }

        [HttpGet("userdatasets")]
        public async Task<IEnumerable<UserDatasetViewModel>> GetUserDatasets(
            PagedResultRequestDto pagedResultRequestDto)
        {
            var entities = await _userDatasetRepository.Where(x => x.UserId == _ewocUser.UserId || _ewocUser.IsAdmin)
                .Skip(pagedResultRequestDto.SkipCount)
                .Take(pagedResultRequestDto.MaxResultCount)
                .AsNoTracking()
                .ToArrayAsync();
            return ObjectMapper.Map<UserDataset[], UserDatasetViewModel[]>(entities);
        }

        [HttpGet("userdatasets/{id:guid}")]
        public async Task<UserDatasetViewModel> GetUserDataset(Guid id)
        {
            var userDataset = await _userDatasetRepository.GetAsync(x =>
                x.Id == id && (x.UserId == _ewocUser.UserId || _ewocUser.IsAdmin));
            return ObjectMapper.Map<UserDataset, UserDatasetViewModel>(
                userDataset);
        }

        [HttpPost("userdatasets")]
        public async Task<IActionResult> AddOrUpdateUserDataset([FromQuery] CreateOrUpdateUserDataset createUserDataset,
            [Required] IFormFile uploadedFile)
        {
            if (createUserDataset.CollectionId.Contains(" ") || _collectionRepository.Any(x =>
                x.CollectionId == createUserDataset.CollectionId && x.AccessType == AccessType.Public))
            {
                return new BadRequestObjectResult(
                    "Invalid CollectionID. CollectionID must be unique across all collections and without spaces.");
            }

            if (_ewocUser.CanAccessUserData == false)
            {
                return new UnauthorizedObjectResult("Auth required.");
            }

            if (uploadedFile == null || uploadedFile.FileName.Contains(".zip") == false)
            {
                return new BadRequestObjectResult("Invalid file- zip shape file and upload");
            }

            var directoryToExtract = GetDirectoryPath(_userDatasetConfig.TempFolder);
            var zipFilePath = Path.Combine(GetDirectoryPath(_userDatasetConfig.DatasetBackupFolder),
                uploadedFile.FileName);

            await using (var fileStream = File.Create(zipFilePath))
            {
                await uploadedFile.CopyToAsync(fileStream);
            }

            ZipFile.ExtractToDirectory(zipFilePath, directoryToExtract, Encoding.UTF8, true);

            var shapeFiles = SearchHelper.GetFiles(directoryToExtract, "*.shp");
            var gpkgFiles = SearchHelper.GetFiles(directoryToExtract, "*.gpkg");

            if (shapeFiles.Length == 0 && gpkgFiles.Length == 0)
            {
                return new BadRequestObjectResult(".shp/.gpkg file not found in the zip file");
            }

            string filePath;
            if (shapeFiles.Length > 0)
            {
                filePath = shapeFiles[0];
                var files = SearchHelper.GetFiles(directoryToExtract, "*.prj");
                if (files.Length != 1)
                {
                    return new BadRequestObjectResult(".prj file not found in the zip file");
                }

                if (GeoJsonHelper.IsEpsg4326(await File.ReadAllTextAsync(files[0])) == false)
                {
                    return new BadRequestObjectResult(
                        $"shape files contains invalid project. Only EPSG 4326/WGS84 projection is supported. {GeoJsonHelper.EPsg4326EsriWkt}");
                }
            }
            else
            {
                filePath = gpkgFiles[0];
                var vectorGeoPackageFileReader = new VectorGeoPackageFileReader(filePath);
                if (vectorGeoPackageFileReader.GetSrsId() != GeoJsonHelper.GeometryWgs84Srid)
                {
                    vectorGeoPackageFileReader.Dispose();
                    return new BadRequestObjectResult(
                        "GPKG has invalid projection. Only EPSG 4326/WGS84 projection is supported.");
                }

                vectorGeoPackageFileReader.Dispose();
            }

            UserDataset entity;
            if (_userDatasetRepository.Any(x => x.CollectionId == createUserDataset.CollectionId))
            {
                entity = await _userDatasetRepository.GetAsync(x => x.CollectionId == createUserDataset.CollectionId);
                if (_ewocUser.IsAdmin == false && entity.UserId != _ewocUser.UserId)
                {
                    return new BadRequestObjectResult("User not owner of dataset");
                }

                if (entity.State == UserDatasetState.PublicDataset)
                {
                    return new BadRequestObjectResult(
                        $"User Dataset state: {entity.State:G}. Cannot upload new shape file.");
                }

                entity = UpdateEntity(createUserDataset, entity);
                entity = await _userDatasetRepository.UpdateAsync(entity);
            }
            else
            {
                var userDataset = new UserDataset
                {
                    CollectionId = createUserDataset.CollectionId,
                    UserId = _ewocUser.UserId
                };

                userDataset = UpdateEntity(createUserDataset, userDataset);
                entity = await _userDatasetRepository.InsertAsync(userDataset);
            }


            _logger.LogInformation($"File path-{filePath}, starting validation job");
            var validationArgs = new ValidationArgs
            {
                Path = filePath, UserDatasetId = entity.Id,
                DirectoryPathToClean = directoryToExtract,
                ZipFilePath = zipFilePath
            };
            await _backgroundJobManager.EnqueueAsync(validationArgs, delay: TimeSpan.FromMilliseconds(10));
            return new OkObjectResult(ObjectMapper.Map<UserDataset, UserDatasetViewModel>(entity));
        }

        private string GetDirectoryPath(string dirPath)
        {
            var directoryToExtract = Path.Combine(dirPath, Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directoryToExtract);
            return directoryToExtract;
        }

        [HttpPut("userdatasets/{id:guid}/public")]
        public async Task<IActionResult> MakeDatasetPublic(Guid id)
        {
            if (_ewocUser.IsAdmin == false)
            {
                return new UnauthorizedObjectResult("Auth required. Only Admin allowed for this operation");
            }

            // check if user has permissions
            var userDataset = await _userDatasetRepository.GetAsync(x => x.Id == id);
            var masterData =
                await _collectionRepository.GetAsync(x => x.CollectionId == userDataset.CollectionId);
            if (userDataset.State == UserDatasetState.PublicDataset ||
                userDataset.State != UserDatasetState.AvailableInModule)
            {
                return new BadRequestObjectResult($"Dataset is in  {userDataset.State} state.");
            }

            var tenant = await _tenantRepository.FindByNameAsync(userDataset.CollectionId);
            using (_currentTenant.Change(tenant.Id))
            {
                if (await _metadataItemsRepository.AnyAsync() == false)
                {
                    return new BadRequestObjectResult("No Metadata updated.");
                }

                var tenantData =
                    await _collectionRepository.GetAsync(x => x.CollectionId == userDataset.CollectionId);
                tenantData.AccessType = AccessType.Public;
                await _collectionRepository.UpdateAsync(tenantData);
            }

            // Check for metadata.
            if (userDataset.State != UserDatasetState.PublicDataset)
            {
                userDataset.State = UserDatasetState.PublicDataset;
                await _userDatasetRepository.UpdateAsync(userDataset);
            }

            masterData.AccessType = AccessType.Public;
            await _collectionRepository.UpdateAsync(masterData);

            return new OkResult();
        }

        [HttpDelete("userdatasets/{id:guid}")]
        public async Task<IActionResult> DeleteUserDataset(Guid id)
        {
            // check if user has delete permission and dataset is not public
            var userDataset = await _userDatasetRepository.GetAsync(x => x.Id == id
                                                                         && (x.UserId == _ewocUser.UserId
                                                                             || _ewocUser.IsAdmin));
            if (await _collectionRepository.AnyAsync(x => x.CollectionId == userDataset.CollectionId))
            {
                var masterData =
                    await _collectionRepository.GetAsync(x => x.CollectionId == userDataset.CollectionId);
                if (masterData.AccessType == AccessType.Public && _ewocUser.IsAdmin == false)
                {
                    return new BadRequestObjectResult(
                        $"{userDataset.CollectionId} is Public access collection. Cannot be deleted by user.");
                }
            }

            await _addCollectionHelper.DeleteUserDataset(userDataset.CollectionId);
            await _userDatasetRepository.DeleteAsync(x => x.Id == id);
            return new OkObjectResult("Deleted user Dataset");
        }

        private static UserDataset UpdateEntity(CreateOrUpdateUserDataset createUserDataset, UserDataset entity)
        {
            entity.Title = createUserDataset.Title;
            entity.TypeOfObservationMethod = createUserDataset.TypeOfObservationMethod;
            entity.CollectionId = createUserDataset.CollectionId;
            entity.ConfidenceLandCover = createUserDataset.ConfidenceLandCover;
            entity.ConfidenceCropType = createUserDataset.ConfidenceCropType;
            entity.ConfidenceIrrigationType = createUserDataset.ConfidenceIrrigationType;
            entity.State = UserDatasetState.UploadedValidationInProgressWait;
            entity.Errors = Array.Empty<string>();
            return entity;
        }
    }
}
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Flurl.Http;
using Flurl.Util;
using IIASA.WorldCereal.Rdm.Core;
using IIASA.WorldCereal.Rdm.Core.Ai;
using IIASA.WorldCereal.Rdm.Core.Vector;
using IIASA.WorldCereal.Rdm.Dtos;
using IIASA.WorldCereal.Rdm.Entity;
using IIASA.WorldCereal.Rdm.Enums;
using IIASA.WorldCereal.Rdm.ExcelOps;
using IIASA.WorldCereal.Rdm.Interfaces;
using IIASA.WorldCereal.Rdm.Jobs;
using IIASA.WorldCereal.Rdm.Jobs.ColumnExtraction;
using IIASA.WorldCereal.Rdm.Jobs.ColumnMapping;
using IIASA.WorldCereal.Rdm.Jobs.Harmonization;
using IIASA.WorldCereal.Rdm.Jobs.Sampling;
using IIASA.WorldCereal.Rdm.Jobs.Validation;
using IIASA.WorldCereal.Rdm.ServiceConfigs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Swashbuckle.AspNetCore.Annotations;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Volo.Abp.TenantManagement;
using Volo.Abp.Users;

namespace IIASA.WorldCereal.Rdm.Services
{
    [RemoteService(Name = "OGC")]
    [ApiExplorerSettings(GroupName = "OGC")]
    [Authorize]
    public class UserDatasetService : RdmAppService, IUserDatasetService
    {
        private readonly IRepository<UserDataset, Guid> _userDatasetRepository;
        private readonly IBackgroundJobManager _backgroundJobManager;
        private readonly UserDatasetConfig _userDatasetConfig;
        private readonly ILogger<UserDatasetService> _logger;
        private readonly IAddCollectionHelper _addCollectionHelper;
        private readonly IRepository<ExternalUserAccount, int> _externalUserAccountRepository;
        private readonly IRepository<MetadataItem, int> _metadataItemsRepository;
        private readonly ITenantRepository _tenantRepository;
        private readonly EmailHelper _emailHelper;
        private readonly ICurrentTenant _currentTenant;
        private readonly IAzureFileUploader _azureFileUploader;
        private readonly IRepository<UserDatasetBackup, Guid> _userDatasetBackups;
        private readonly IRepository<EoType, int> _eoTypeRepository;
        private readonly CurrentUser _currentUser;
        private readonly IRepository<CollectionMetadataEntity, Guid> _collectionRepository;
        private readonly IAccessProvider _accessProvider;
        private readonly MapPredictionService _mapPredictionService;

        public UserDatasetService(IRepository<UserDataset, Guid> userDatasetRepository,
            IBackgroundJobManager backgroundJobManager, ILogger<UserDatasetService> logger,
            IAddCollectionHelper addCollectionHelper,
            IRepository<ExternalUserAccount, int> externalUserAccountRepository,
            IRepository<MetadataItem, int> metadataItemsRepository,
            ITenantRepository tenantRepository, EmailHelper emailHelper,
            IRepository<CollectionMetadataEntity, Guid> collectionRepository,
            ICurrentTenant currentTenant, IAzureFileUploader azureFileUploader,
            IRepository<UserDatasetBackup, Guid> userDatasetBackups, IRepository<EoType, int> eoTypeRepository,
            CurrentUser currentUser, IAccessProvider accessProvider, MapPredictionService mapPredictionService,
            UserDatasetConfig userDatasetConfig)
        {
            _accessProvider = accessProvider;
            _mapPredictionService = mapPredictionService;
            _userDatasetRepository = userDatasetRepository;
            _backgroundJobManager = backgroundJobManager;
            _userDatasetConfig = userDatasetConfig;
            _logger = logger;
            _addCollectionHelper = addCollectionHelper;
            _externalUserAccountRepository = externalUserAccountRepository;
            _metadataItemsRepository = metadataItemsRepository;
            _tenantRepository = tenantRepository;
            _emailHelper = emailHelper;
            _currentTenant = currentTenant;
            _azureFileUploader = azureFileUploader;
            _userDatasetBackups = userDatasetBackups;
            _eoTypeRepository = eoTypeRepository;
            _currentUser = currentUser;
            _collectionRepository = collectionRepository;
        }

        [HttpGet("userdataset/reproject/4326")]
        public async Task<IActionResult> ReprojectToLatlng(string path)
        {
            try
            {
                var res = await _mapPredictionService.CheckAndReProjectTo4326(path);
                return new OkObjectResult(res);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                return new OkObjectResult(e.ToString());
            }
        }

        [HttpGet("predict/crop/map")]
        public async Task<IActionResult> GetCropMap(string[] crops)
        {
            try
            {
                var res = await _mapPredictionService.GetCropPredictions(crops);
                return new OkObjectResult(res);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                return new OkObjectResult(e.ToString());
            }
        }

        [HttpGet("predict/irrigation/map")]
        public async Task<IActionResult> GetIrrigationMap(string[] irrs)
        {
            try
            {
                var res = await _mapPredictionService.GetIrrigationPredictions(irrs);
                return new OkObjectResult(res);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                return new OkObjectResult(e.ToString());
            }
        }

        [HttpGet("userdatasets")]
        public async Task<IEnumerable<UserDatasetViewModel>> GetUserDatasets(
            PagedResultRequestDto pagedResultRequestDto)
        {
            var userId = _currentUser.GetId().ToString();
            var states = await _accessProvider.GetCommunityAllowedUserDatasetStates();
            var entities = await (await _userDatasetRepository.GetQueryableAsync())
                .Where(x => x.UserId == userId || states.Contains(x.State))
                .Skip(pagedResultRequestDto.SkipCount)
                .Take(pagedResultRequestDto.MaxResultCount)
                .AsNoTracking()
                .ToArrayAsync();
            return ObjectMapper.Map<UserDataset[], UserDatasetViewModel[]>(entities);
        }

        [HttpGet("userdatasets/{id:guid}/metadata")]
        public async Task<MetadataItemDto[]> GetUserDatasetMetadata(Guid id)
        {
            var userDataset = await _userDatasetRepository.GetAsync(x => x.Id == id);
            if (userDataset.State < UserDatasetState.AvailableInModule)
            {
                return [];
            }

            var tenant = await GetTenant(userDataset.CollectionId);

            using (CurrentTenant.Change(tenant.Id))
            {
                var items = await _metadataItemsRepository.GetListAsync();
                var itemNamesToCheck = new[]
                {
                    ExcelMarkers.ProviderContact,
                    ExcelMarkers.ProviderUserName,
                    ExcelMarkers.ObservationCoordinateSystem
                };

                if (items.Any(x => itemNamesToCheck.Contains(x.Name)) == false)
                {
                    var userExt = await _externalUserAccountRepository.GetAsync(x => x.UserId == userDataset.CreatorId);
                    var newItems = new[]
                    {
                        new MetadataItem { Name = ExcelMarkers.ProviderContact, Value = userExt.EmailId },
                        new MetadataItem { Name = ExcelMarkers.ProviderUserName, Value = userExt.UserName },
                        new MetadataItem { Name = ExcelMarkers.ObservationCoordinateSystem, Value = "EPSG:4326" },
                    };
                    await _metadataItemsRepository.InsertManyAsync(newItems);
                    items.AddRange(newItems);
                } /*else
                {
                    var emailItem = items.First(x => x.Name == ExcelMarkers.ProviderContact);
                    emailItem.Value = userExt.EmailId;
                    var userNameItem = items.First(x => x.Name == ExcelMarkers.ProviderUserName);
                    userNameItem.Value = userExt.UserName;
                    await _metadataItemsRepository.UpdateManyAsync(new[] { userNameItem, emailItem });
                }*/

                return ObjectMapper.Map<MetadataItem[], MetadataItemDto[]>(items
                    .OrderByDescending(x => x.LastModificationTime ?? x.CreationTime)
                    .ToArray());
            }
        }

        [HttpGet("userdatasets/{id:guid}/harmonization/pdf")]
        public async Task<string> GetHarmonizationPdf(Guid id)
        {
            var userDataset = await _userDatasetRepository.GetAsync(x => x.Id == id);

            var files = _azureFileUploader.GetUserDatasetBlobClientUrls(userDataset.Id.ToString("N"));

            return files.First(x => x.Contains("HarmonizationInfo.pdf"));
        }

        [HttpPost("userdatasets/{id:guid}/harmonization/pdf")]
        public async Task<IActionResult> UploadHarmonizationPdf(Guid id, [Required] IFormFile pdfFile)
        {
            var fileName = "HarmonizationInfo.pdf";
            var userDataset = await _userDatasetRepository.GetAsync(x => x.Id == id);

            if (pdfFile == null || pdfFile.FileName.Contains(".pdf") == false)
            {
                throw new BadHttpRequestException("only pdf document can be uploaded");
            }

            using (var fileStream = pdfFile.OpenReadStream())
            {
                var file = await _azureFileUploader.UploadUserDatasetFileAsync(userDataset.Id.ToString("N"), fileStream,
                    fileName);
            }

            return new OkObjectResult("File Successfully uploaded");
        }

        [HttpPost("userdatasets/{id:guid}/harmonization/excel")]
        public async Task<IActionResult> UploadHarmonizationExcel(Guid id, [Required] IFormFile newExcelFile)
        {
            var userDataset = await _userDatasetRepository.GetAsync(x => x.Id == id);

            if (newExcelFile == null || newExcelFile.FileName.Contains(".xlsx") == false)
            {
                throw new BadHttpRequestException("only .xlsx document can be uploaded");
            }

            var tenant = await GetTenant(userDataset.CollectionId);
            var excelItems = await GetExcelItems(tenant, userDataset);

            var files = _azureFileUploader.GetUserDatasetBlobClientUrls(userDataset.Id.ToString("N"));
            var excelMetadatasList = await GetExcelTableItems(files, userDataset);
            List<ExcelMetadata> excelMetadatas;
            using (Stream stream = newExcelFile.OpenReadStream())
            {
                excelMetadatas = ExcelOps.ExcelOps.ExtractHarmonizationCustomText(stream,userDataset.DatasetExtractedPath);
            }

            excelMetadatas.AddRange(excelItems);
            await GenerateDocs(excelMetadatas, excelMetadatasList, userDataset);

            return new OkObjectResult("File Successfully uploaded");
        }

        private const string ExcelFile = "HarmonizationInfo.xlsx";
        private const string PdfFile = "HarmonizationInfo.pdf";

        [HttpGet("userdatasets/{id:guid}/harmonization/excel")]
        public async Task<string[]> GenerateHarmonizationEditableExcelDoc(Guid id, [FromQuery] bool regenerate = false)
        {
            var userDataset = await _userDatasetRepository.GetAsync(x => x.Id == id);
            var tenant = await GetTenant(userDataset.CollectionId);
            var files = _azureFileUploader.GetUserDatasetBlobClientUrls(userDataset.Id.ToString("N"));
            /*if (!regenerate && files.Any(x => x.Contains(ExcelFile)))
            {
                return files.Where(x => x.Contains(ExcelFile) || x.Contains(PdfFile)).ToArray();
            }*/

            var excelItems = await GetExcelItems(tenant, userDataset);

            var excelMetadatasList = await GetExcelTableItems(files, userDataset);


            return await GenerateDocs(excelItems, excelMetadatasList, userDataset);
        }

        private async Task<string[]> GenerateDocs(List<ExcelMetadata> excelItems,
            List<ExcelMetadata[]> excelMetadatasList, UserDataset userDataset)
        {
            using (var memStream = new MemoryStream { Position = 0 })
            {
                ExcelOps.ExcelOps.WriteHarmonizationInfos(memStream, excelItems.ToArray(), excelMetadatasList);
                await _azureFileUploader.UploadUserDatasetFileAsync(userDataset.Id.ToString("N"), memStream, ExcelFile);
            }

            var imagePaths = SearchHelper.GetFiles(userDataset.DatasetExtractedPath, $"{ExcelOps.ExcelOps.ConfidenceCalcImage}.*", SearchOption.AllDirectories);
            var image = imagePaths.Length > 0 ? imagePaths.First() : string.Empty;
            var pdfFilePath = Path.Combine(userDataset.DatasetExtractedPath, PdfFile);
            using (var fileStream = new FileStream(pdfFilePath, FileMode.Create))
            {
                ExcelOps.ExcelOps.GenerateHarmonizationPdf(fileStream, excelItems.ToArray(), excelMetadatasList,image );
            }

            using (var stream = new FileStream(pdfFilePath, FileMode.Open))
            {
                await _azureFileUploader.UploadUserDatasetFileAsync(userDataset.Id.ToString("N"), stream, PdfFile);
            }

            var newFiles = _azureFileUploader.GetUserDatasetBlobClientUrls(userDataset.Id.ToString("N"));
            Directory.Delete(userDataset.DatasetExtractedPath, true);
            return newFiles.Where(x => x.Contains(ExcelFile) || x.Contains(PdfFile)).ToArray();
        }

        private async Task<List<ExcelMetadata[]>> GetExcelTableItems(string[] files, UserDataset userDataset)
        {
            var fileNames = new[]
                { ColMapConstants.EwocMappedFileNameWithExt, ColMapConstants.IrrMappedFileNameWithExt };
            var excelMetadatasList = new List<ExcelMetadata[]>();
            foreach (var file in files)
            {
                if (fileNames.Any(x => file.Contains(x)) == false)
                {
                    continue;
                }


                var fileNameToDownload = fileNames.First(x => file.Contains(x));
                var directoryToExtract = userDataset.DatasetExtractedPath;
                var filePath = Path.Combine(directoryToExtract, fileNameToDownload);
                Directory.CreateDirectory(directoryToExtract);
                await file.DownloadFileAsync(directoryToExtract);
                var codes = CodeMapHelper.GetMap(filePath);
                if (codes.Any())
                {
                    var name = fileNameToDownload == ColMapConstants.EwocMappedFileNameWithExt
                        ? ExcelMarkers.CropTypeMappings
                        : ExcelMarkers.IrrStatusMappings;

                    var ids = codes.Select(x => x.Code).Distinct().ToArray();
                    var eoLables = await _eoTypeRepository.GetListAsync(x => ids.Contains(x.Value));


                    var excelMetadatas = codes.Select(x =>
                            new ExcelMetadata
                            {
                                Name = name,
                                Value = $"{x.Label};{x.Code};{eoLables.Find(e => e.Value == x.Code)?.Name}"
                            })
                        .ToArray();

                    excelMetadatasList.Add(excelMetadatas);
                }
            }

            return excelMetadatasList;
        }

        private async Task<List<ExcelMetadata>> GetExcelItems(Tenant tenant, UserDataset userDataset)
        {
            var blobNames = _azureFileUploader.GetUserDatasetBlobNames(userDataset.Id.ToString("N"));
            var whiteListFiles = new[] { ".parquet", ".geoparquet", ".gpkg", ".shp" };
            var userFiles = blobNames.Where(x => whiteListFiles.Any(y => x.Contains(y))).Select(r => r.Split("/")[1])
                .Where(x => x.Contains(userDataset.CollectionId) == false);
            var userDataSetFileNames = string.Join("; ", userFiles);
            var requiredAttributes = new[]
            {
                ExcelMarkers.ProviderContact,
                ExcelMarkers.ProviderUserName, ExcelMarkers.DatasetTitle, ExcelMarkers.OriginalDatasetName
            };
            List<ExcelMetadata> excelItems;
            using (CurrentTenant.Change(tenant.Id))
            {
                var metadataItems =
                    await _metadataItemsRepository.GetListAsync(x => requiredAttributes.Contains(x.Name));

                excelItems = metadataItems.Select(x => new ExcelMetadata { Name = x.Name, Value = x.Value }).ToList();

                excelItems.Add(new ExcelMetadata
                    { Name = ExcelMarkers.HarmonizationColId, Value = userDataset.CollectionId });
                excelItems.Add(new ExcelMetadata { Name = ExcelMarkers.HarmonizationTitle, Value = userDataset.Title });
                excelItems.Add(new ExcelMetadata { Name = ExcelMarkers.FileName, Value = userDataSetFileNames });
                excelItems.Add(new ExcelMetadata
                    { Name = ExcelMarkers.DateUploaded, Value = userDataset.CreationTime.ToString("s")[..10] });
                excelItems.Add(new ExcelMetadata
                {
                    Name = ExcelMarkers.DatasetAttributes,
                    Value = string.Join(", ", userDataset.Columns.Select(x => x.Split(";")[0]))
                });
            }

            var isManualDate = IsManualDate(userDataset);
            if (isManualDate)
            {
                excelItems.Add(new ExcelMetadata { Name = ExcelMarkers.ManualValDate, Value = userDataset.ValDateCol });
            }
            else
            {
                excelItems.Add(new ExcelMetadata
                    { Name = ExcelMarkers.ValDateAttribute, Value = userDataset.ValDateCol });
            }

            excelItems.Add(new ExcelMetadata
                { Name = ExcelMarkers.CropTypeAttribute, Value = userDataset.EwocCodeCol });
            excelItems.Add(new ExcelMetadata { Name = ExcelMarkers.IrrStatusAttribute, Value = userDataset.IrrCol });
            return excelItems;
        }

        private static bool IsManualDate(UserDataset userDataset)
        {
            try
            {
                var date = DateTime.ParseExact(userDataset.ValDateCol, "yyyy-M-d", CultureInfo.InvariantCulture);
                return true;
            }
            catch (Exception e)
            {
                return false;
            }
        }

        [HttpGet("userdatasets/{id:guid}")]
        public async Task<UserDatasetViewModel> GetUserDataset(Guid id)
        {
            var userDataset = await _userDatasetRepository.GetAsync(x =>
                x.Id == id);
            return ObjectMapper.Map<UserDataset, UserDatasetViewModel>(userDataset);
        }

        [HttpPut("userdatasets/{id:guid}/title")]
        public async Task<UserDatasetViewModel> GetUserDataset(Guid id, string title)
        {
            var userDataset = await _userDatasetRepository.GetAsync(x =>
                x.Id == id);
            userDataset.Title = title;
            return ObjectMapper.Map<UserDataset, UserDatasetViewModel>(
                userDataset);
        }

        [HttpPost("userdatasets/{id}/mappedFile")]
        public async Task<IActionResult> UploadMappedFiles(Guid id, IFormFileCollection files)
        {
            var validFiles = new[] { ColMapConstants.EwocFileNameWithExt, ColMapConstants.IrrFileNameWithExt };
            if (files.Count > 2)
            {
                return new BadRequestObjectResult($"Only 2 files: {string.Join(",", validFiles)} can be uploaded");
            }

            var userDataset = await _userDatasetRepository.GetAsync(x =>
                x.Id == id);
            var directoryToExtract = userDataset.DatasetExtractedPath;

            var filePath = SearchHelper.GetFilePath(directoryToExtract);
            var harmoniseArgs = new HarmoniseArgs
            {
                Path = filePath, UserDatasetId = userDataset.Id,
                DatasetExtractedPath = directoryToExtract,
            };
            var index = 0;
            foreach (var file in files)
            {
                if (file.FileName.Contains(".csv") == false)
                {
                    return new BadRequestObjectResult(
                        $"Invalid file- {file.FileName} is not csv. Only {string.Join(",", validFiles)} file are allowed");
                }

                var newFileName = Path.Combine(directoryToExtract,
                    index == 0 ? ColMapConstants.EwocMappedFileNameWithExt : ColMapConstants.IrrMappedFileNameWithExt);
                File.Delete(newFileName); // delete existing file
                index++;

                await using (var fileStream = File.Create(newFileName))
                {
                    await file.CopyToAsync(fileStream);
                }
            }

            var ewocMapFile = Path.Combine(directoryToExtract, ColMapConstants.EwocFileNameWithExt);
            var ewocMappedFile = Path.Combine(directoryToExtract, ColMapConstants.EwocMappedFileNameWithExt);
            var ewocUnmappedLabels = CodeMapHelper.GetUnmappedCodes(ewocMapFile, ewocMappedFile);
            if (ewocUnmappedLabels.Length > 0)
            {
                return new BadRequestObjectResult(
                    $"Missing Ewoc Maps for {userDataset.EwocCodeCol} Column Labels -{string.Join(",", ewocUnmappedLabels)}");
            }
            else
            {
                // all good with ewoc code map file
                harmoniseArgs.EwocMappingFile = ewocMappedFile;
            }

            var irrMapFile = Path.Combine(directoryToExtract, ColMapConstants.IrrFileNameWithExt);
            var irrMappedFile = Path.Combine(directoryToExtract, ColMapConstants.IrrMappedFileNameWithExt);

            if (File.Exists(irrMapFile) && File.Exists(irrMappedFile))
            {
                var irrUnmappedLabels = CodeMapHelper.GetUnmappedCodes(irrMapFile, irrMappedFile);
                if (irrUnmappedLabels.Length > 0)
                {
                    return new BadRequestObjectResult(
                        $"Missing Irrigation Maps for {userDataset.IrrCol} Column Labels -{string.Join(",", irrUnmappedLabels)}");
                }

                harmoniseArgs.IrrMappingFile = irrMappedFile;
            }

            userDataset.State = UserDatasetState.UploadedHarmonisationInProgressWait;
            var entity = await _userDatasetRepository.UpdateAsync(userDataset);

            _logger.LogInformation($"File path-{filePath}, starting validation job");

            await _backgroundJobManager.EnqueueAsync(harmoniseArgs, delay: TimeSpan.FromSeconds(1));
            return new OkObjectResult(ObjectMapper.Map<UserDataset, UserDatasetViewModel>(entity));
        }

        [HttpPost("userdatasets/{id}/mappings")]
        public async Task<IActionResult> UploadEwocMappings(Guid id, [FromBody] AttributeMapInfo mapInfo)
        {
            var cropMaps = mapInfo.Maps.Where(x => x.AttributeName == ColMapConstants.EwocFileName).ToArray();
            var irrMaps = mapInfo.Maps.Where(x => x.AttributeName == ColMapConstants.IrrFileName).ToArray();

            if (cropMaps.Length != 0 == false && irrMaps.Length != 0 == false)
            {
                return new BadRequestObjectResult(
                    $"Missing Ewoc Maps for {ColMapConstants.EwocFileName} and {ColMapConstants.EwocFileName}");
            }

            var userDataset = await _userDatasetRepository.GetAsync(x =>
                x.Id == id);
            var directoryToExtract = userDataset.DatasetExtractedPath;

            var filePath = SearchHelper.GetFilePath(directoryToExtract);
            var harmoniseArgs = new HarmoniseArgs
            {
                Path = filePath, UserDatasetId = userDataset.Id,
                DatasetExtractedPath = directoryToExtract,
            };

            var ewocMapFile = Path.Combine(directoryToExtract, ColMapConstants.EwocFileNameWithExt);
            var ewocMappedFile = Path.Combine(directoryToExtract, ColMapConstants.EwocMappedFileNameWithExt);
            var irrMapFile = Path.Combine(directoryToExtract, ColMapConstants.IrrFileNameWithExt);
            var irrMappedFile = Path.Combine(directoryToExtract, ColMapConstants.IrrMappedFileNameWithExt);

            // save mapped files
            await CodeMapHelper.WriteMaps(ewocMappedFile, cropMaps);
            if (irrMaps.Any())
            {
                await CodeMapHelper.WriteMaps(irrMappedFile, irrMaps);
            }

            var ewocUnmappedLabels = CodeMapHelper.GetUnmappedCodes(ewocMapFile, ewocMappedFile);
            if (ewocUnmappedLabels.Length > 0)
            {
                return new BadRequestObjectResult(
                    $"Missing Ewoc Maps for {userDataset.EwocCodeCol} Column Labels -{string.Join(",", ewocUnmappedLabels)}");
            }

            // all good with ewoc code map file
            harmoniseArgs.EwocMappingFile = ewocMappedFile;

            if (File.Exists(irrMapFile) && File.Exists(irrMappedFile))
            {
                var irrUnmappedLabels = CodeMapHelper.GetUnmappedCodes(irrMapFile, irrMappedFile);
                if (irrUnmappedLabels.Length > 0)
                {
                    return new BadRequestObjectResult(
                        $"Missing Irrigation Maps for {userDataset.IrrCol} Column Labels -{string.Join(",", irrUnmappedLabels)}");
                }

                harmoniseArgs.IrrMappingFile = irrMappedFile;
            }

            await DateParseHelper.UpdateSelectedYear(userDataset.DatasetExtractedPath, mapInfo.SelectedYear);
            userDataset.State = UserDatasetState.UploadedHarmonisationInProgressWait;
            var entity = await _userDatasetRepository.UpdateAsync(userDataset);

            _logger.LogInformation($"File path-{filePath}, starting validation job");

            await _backgroundJobManager.EnqueueAsync(harmoniseArgs, delay: TimeSpan.FromSeconds(1));
            return new OkObjectResult(ObjectMapper.Map<UserDataset, UserDatasetViewModel>(entity));
        }

        [HttpGet("userdatasets/{userDatasetId}/attribute/{attributeName}")]
        public async Task<CodeMap[]> GetEwocCodeMappings(Guid userDatasetId, string attributeName)
        {
            var userDataset = await _userDatasetRepository.GetAsync(x =>
                x.Id == userDatasetId);
            var directoryToExtract = userDataset.DatasetExtractedPath;
            var fileName = attributeName == ColMapConstants.EwocFileName
                ? ColMapConstants.EwocFileNameWithExt
                : ColMapConstants.IrrFileNameWithExt;
            var filePath = Path.Combine(directoryToExtract, fileName);
            if (File.Exists(filePath))
            {
                var colMaps = CodeMapHelper.GetMap(filePath);
                return colMaps;
            }

            return [];
        }

        [HttpGet("userdatasets/{id}/valTimeYears")]
        public async Task<DateInfo?> GetValDateYears(Guid id)
        {
            var userDataset = await _userDatasetRepository.GetAsync(x =>
                x.Id == id);
            var directoryToExtract = userDataset.DatasetExtractedPath;

            return await DateParseHelper.GetDateInfo(directoryToExtract);
        }

        [HttpGet("userdatasets/{id}/colName/{colName}")]
        public async Task<IActionResult> GetMappingFile(Guid id, string colName)
        {
            var userDataset = await _userDatasetRepository.GetAsync(x =>
                x.Id == id);
            var directoryToExtract = userDataset.DatasetExtractedPath;
            var fileName = colName == ColMapConstants.EwocFileName
                ? ColMapConstants.EwocFileNameWithExt
                : ColMapConstants.IrrFileNameWithExt;
            var filePath = Path.Combine(directoryToExtract, fileName);
            if (File.Exists(filePath))
            {
                using (var file = File.OpenRead(filePath))
                {
                    return new FileContentResult(await file.GetAllBytesAsync(),
                        "application/octet-stream") { FileDownloadName = $"{fileName}" };
                }
            }

            return new NotFoundObjectResult($"{colName} file not found");
        }

        [HttpPost("userdatasets/{id}/columnsToMap")]
        public async Task<IActionResult> AddOrUpdateSimpleUserDataset(Guid id,
            [FromQuery] CreateOrUpdateUserDatasetColumnNames createUserDatasetColumnNames)
        {
            var userDataset = await _userDatasetRepository.GetAsync(x =>
                x.Id == id);
            var directoryToExtract = userDataset.DatasetExtractedPath;
            var filePath = SearchHelper.GetFilePath(directoryToExtract);
            var isDateTimeType = false;
            if (createUserDatasetColumnNames.IsManualValidityDate)
            {
                // check if date can be parsed, else throw exception. Manual date format always yyyy-M-d
                var formatString = "yyyy-M-d";
                var dateVal = DateTime.ParseExact(createUserDatasetColumnNames.ValTimeColumnName, formatString,
                    CultureInfo.InvariantCulture);
            }
            else
            {
                var reader = VectorFileReader.GetReader(filePath);

                var colIndex = reader.GetOrdinalExt(createUserDatasetColumnNames.ValTimeColumnName);
                if (colIndex == -1)
                {
                    throw new BadHttpRequestException(
                        $"Unable to find column: {createUserDatasetColumnNames.ValTimeColumnName}");
                }

                char[] separators = { 'T', ' ' };
                var type = reader.GetFieldType(colIndex);
                if (type.ToInvariantString().Contains("String"))
                {
                    reader.Read();
                    var sampleValue = reader.GetStringExt(colIndex);
                    var isValid = DateTime.TryParseExact(sampleValue.Split(separators)[0],
                        createUserDatasetColumnNames.DateFormat,
                        CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces,
                        out _); // check if date can be parsed
                    if (isValid == false)
                    {
                        return new BadRequestObjectResult(
                            $"Wrong Date Format Selected! Cannot parse string date: {sampleValue} with format: {createUserDatasetColumnNames.DateFormat}");
                    }
                }
                else
                {
                    // if Attribute is Datetime type
                    isDateTimeType = true;
                }

                reader.Dispose();
            }

            userDataset.EwocCodeCol = createUserDatasetColumnNames.EwocColumnName;
            userDataset.ValDateCol = createUserDatasetColumnNames.ValTimeColumnName;
            if (string.IsNullOrEmpty(createUserDatasetColumnNames.IrrColumnName))
            {
                var irrFile = Path.Combine(directoryToExtract, ColMapConstants.IrrFileNameWithExt);
                if (File.Exists(irrFile))
                {
                    File.Delete(irrFile);
                }

                userDataset.IrrCol = string.Empty;
            }
            else
            {
                userDataset.IrrCol = createUserDatasetColumnNames.IrrColumnName;
            }

            userDataset.State = UserDatasetState.MappingInProgressWait;
            var entity = await _userDatasetRepository.UpdateAsync(userDataset, true);

            _logger.LogInformation($"File path-{filePath}, starting UploadedColumnExtractionInProgressWait job");
            var columnMappingJobArgs = new ColumnMappingJobArgs
            {
                Path = filePath, UserDatasetId = userDataset.Id,
                DatasetExtractedPath = directoryToExtract,
                EwocColumnName = createUserDatasetColumnNames.EwocColumnName,
                IrrColumnName = createUserDatasetColumnNames.IrrColumnName ?? string.Empty,
                ValDateColumnName = createUserDatasetColumnNames.ValTimeColumnName,
                DateFormat = createUserDatasetColumnNames.DateFormat,
                IsManualValidityDate = createUserDatasetColumnNames.IsManualValidityDate,
                IsValAttributeDateTimeType = isDateTimeType
            };
            await _backgroundJobManager.EnqueueAsync(columnMappingJobArgs, delay: TimeSpan.FromSeconds(1));
            return new OkObjectResult(ObjectMapper.Map<UserDataset, UserDatasetViewModel>(entity));
        }


        [HttpPost("userdatasets/simple")]
        [SwaggerResponse(200, "Upload Userdataset", typeof(UserDatasetViewModel))]
        [RequestSizeLimit(101 * 1024 * 1024)] //100Mb
        public async Task<IActionResult> AddOrUpdateSimpleUserDataset(
            [FromQuery] CreateOrUpdateUserDataset createUserDataset,
            [Required] IFormFile uploadedFile)
        {
            if (createUserDataset.CollectionId.Contains(" ") || await _collectionRepository.AnyAsync(x =>
                    x.CollectionId == createUserDataset.CollectionId))
            {
                return new BadRequestObjectResult(
                    "Invalid CollectionID. CollectionID must be unique across all collections and without spaces.");
            }


            if (uploadedFile == null || (uploadedFile.FileName.Contains(".zip") == false &&
                                         uploadedFile.FileName.Contains(".gpkg") == false &&
                                         uploadedFile.FileName.Contains(".geoparquet") == false &&
                                         uploadedFile.FileName.Contains(".parquet") == false))
            {
                return new BadRequestObjectResult("Invalid file- zip shape file and upload or upload .geoparquet file");
            }

            var directoryToExtract = GetDirectoryPath(_userDatasetConfig.TempFolder);
            var zipOrGeoParqtFilePath = Path.Combine(directoryToExtract, uploadedFile.FileName);

            await using (var fileStream = File.Create(zipOrGeoParqtFilePath))
            {
                await uploadedFile.CopyToAsync(fileStream);
            }

            if (uploadedFile.FileName.Contains(".zip"))
            {
                ZipFile.ExtractToDirectory(zipOrGeoParqtFilePath, directoryToExtract, Encoding.UTF8, true);
                File.Delete(zipOrGeoParqtFilePath);
            }

            var shapeFiles = SearchHelper.GetFiles(directoryToExtract, "*.shp");
            var gpkgFiles = SearchHelper.GetFiles(directoryToExtract, "*.gpkg");
            var geoparquetFiles = SearchHelper.GetFiles(directoryToExtract, "*.geoparquet|*.parquet");

            if (shapeFiles.Length == 0 && gpkgFiles.Length == 0 && geoparquetFiles.Length == 0)
            {
                return new BadRequestObjectResult(".shp/.gpkg file not found in the zip file");
            }

            if (shapeFiles.Length > 1)
            {
                return new BadRequestObjectResult("Multiple shape files found. Rejected!");
            }

            if (gpkgFiles.Length > 0 && VectorFileReader.HasMultipleLayers(gpkgFiles[0]))
            {
                return new BadRequestObjectResult("Multiple layers found in geo-package. Rejected!");
            }

            var filePath = SearchHelper.GetFilePath(directoryToExtract);
            var newUserDatasetEntity = new UserDataset
            {
                CollectionId = createUserDataset.CollectionId,
                UserId = _currentUser.GetId().ToString(), DatasetExtractedPath = directoryToExtract
            };

            UserDataset entity;
            if (await _userDatasetRepository.AnyAsync(x => x.CollectionId == createUserDataset.CollectionId))
            {
                var dbEntity =
                    await _userDatasetRepository.FirstAsync(x => x.CollectionId == createUserDataset.CollectionId);
                dbEntity.DatasetExtractedPath = directoryToExtract;

                dbEntity = UpdateEntity(createUserDataset, dbEntity,
                    UserDatasetState.UploadedColumnExtractionInProgressWait);
                entity = await _userDatasetRepository.UpdateAsync(dbEntity, true);
            }
            else
            {
                newUserDatasetEntity = UpdateEntity(createUserDataset, newUserDatasetEntity,
                    UserDatasetState.UploadedColumnExtractionInProgressWait);
                entity = await _userDatasetRepository.InsertAsync(newUserDatasetEntity, true);
            }

            _logger.LogInformation($"File path-{filePath}, starting UploadedColumnExtractionInProgressWait job");
            var columnExtractionJobArgs = new ColumnExtractionJobArgs
            {
                Path = filePath, UserDatasetId = entity.Id,
                DatasetExtractedPath = directoryToExtract
            };
            await _backgroundJobManager.EnqueueAsync(columnExtractionJobArgs, delay: TimeSpan.FromSeconds(1));
            return new OkObjectResult(ObjectMapper.Map<UserDataset, UserDatasetViewModel>(entity));
        }

        [HttpPut("userdatasets/{id}/sample/start/job")]
        public async Task StartSamplingJob(Guid id)
        {
            var userDataset = await _userDatasetRepository.GetAsync(x => x.Id == id);
            await _backgroundJobManager.EnqueueAsync(
                new SamplingJobArgs
                    { UserDatasetId = userDataset.Id, DirectoryPathToClean = userDataset.DatasetExtractedPath },
                delay: TimeSpan.FromSeconds(1));
        }

        [HttpPost("userdatasets")]
        public async Task<IActionResult> AddOrUpdateUserDataset([FromQuery] CreateOrUpdateUserDataset createUserDataset,
            [Required] IFormFile uploadedFile)
        {
            if (createUserDataset.CollectionId.Contains(" ") || await _collectionRepository.AnyAsync(x =>
                    x.CollectionId == createUserDataset.CollectionId))
            {
                return new BadRequestObjectResult(
                    "Invalid CollectionID. CollectionID must be unique across all collections and without spaces.");
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
            var geoparquetFiles = SearchHelper.GetFiles(directoryToExtract, "*.geoparquet|*.parquet");

            if (shapeFiles.Length == 0 && gpkgFiles.Length == 0 && geoparquetFiles.Length == 0)
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
            else if (gpkgFiles.Length > 0)
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
            else if (geoparquetFiles.Length > 0)
            {
                filePath = geoparquetFiles[0];
                using (var fileReader = VectorFileReader.GetReader(filePath))
                {
                    if (fileReader.GetSrsId() != GeoJsonHelper.GeometryWgs84Srid)
                    {
                        return new BadRequestObjectResult(
                            "geoparquet has invalid projection. Only EPSG 4326/WGS84 projection is supported.");
                    }
                }
            }
            else
            {
                return new BadRequestObjectResult(
                    "File Type not Supported.");
            }

            UserDataset entity;
            if (await _userDatasetRepository.AnyAsync(x => x.CollectionId == createUserDataset.CollectionId))
            {
                entity = await _userDatasetRepository.GetAsync(x => x.CollectionId == createUserDataset.CollectionId);


                if (entity.State == UserDatasetState.PrivateSharedWithConsortium ||
                    entity.State == UserDatasetState.PublicDataset)
                {
                    return new BadRequestObjectResult(
                        $"User Dataset state: {entity.State:G}. Cannot upload new shape file.");
                }

                entity = UpdateEntity(createUserDataset, entity, UserDatasetState.UploadedValidationInProgressWait);
                entity = await _userDatasetRepository.UpdateAsync(entity);
            }
            else
            {
                var userDataset = new UserDataset
                {
                    CollectionId = createUserDataset.CollectionId,
                    UserId = _currentUser.GetId().ToString()
                };

                userDataset = UpdateEntity(createUserDataset, userDataset,
                    UserDatasetState.UploadedValidationInProgressWait);
                entity = await _userDatasetRepository.InsertAsync(userDataset);
            }


            _logger.LogInformation($"File path-{filePath}, starting validation job");
            var validationArgs = new ValidationArgs
            {
                VectorFilePath = filePath, UserDatasetId = entity.Id,
                DatasetExtractedPath = directoryToExtract
            };
            await _backgroundJobManager.EnqueueAsync(validationArgs, delay: TimeSpan.FromSeconds(1));
            return new OkObjectResult(ObjectMapper.Map<UserDataset, UserDatasetViewModel>(entity));
        }

        private string GetDirectoryPath(string dirPath)
        {
            var directoryToExtract = Path.Combine(dirPath, Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directoryToExtract);
            return directoryToExtract;
        }

        [HttpGet("userdatasets/{id:guid}/backupfiles")]
        public async Task<string[]> GetBackupFiles(Guid id)
        {
            var userDataset = await _userDatasetRepository.GetAsync(x => x.Id == id);
            var files = _azureFileUploader.GetUserDatasetBlobClientUrls(userDataset.Id.ToString("N"));
            return files;
        }

        [HttpPut("userdatasets/{id:guid}/backupfiles")]
        public async Task<string[]> BackupFiles(Guid id)
        {
            var userDataset = await _userDatasetRepository.GetAsync(x => x.Id == id);
            var filePaths = _azureFileUploader.GetUserDatasetBlobClientUrls(userDataset.Id.ToString("N"));
            if (filePaths.Any())
            {
                return filePaths;
            }

            var backUpData = await _userDatasetBackups.GetAsync(x => x.UserDatasetId == id);
            var files = Directory.GetFiles(backUpData.ZipFilePath);
            var list = new List<string>();
            foreach (var file in files)
            {
                await using (var streamFile = File.OpenRead(file))
                {
                    var path = await _azureFileUploader.UploadUserDatasetFileAsync(userDataset.Id.ToString("N"),
                        streamFile,
                        Path.GetFileName(file));
                    list.Add(path);
                }
            }

            try
            {
                Directory.Delete(backUpData.ZipFilePath, true);
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Error while deleting files");
            }

            return list.ToArray();
        }

        [HttpPut("userdatasets/{id:guid}/private-share")]
        [Authorize]
        public async Task<IActionResult> SharePrivateWithConsortium(Guid id)
        {
            var userDataset = await _userDatasetRepository.GetAsync(x => x.Id == id);
            var result = await SetUserDatasetState(id, UserDatasetState.PrivateSharedWithConsortium,
                AccessType.PrivateShared);
            var user = await _accessProvider.User();
            await _emailHelper.SendDatasetShared(userDataset.Id.ToString("D"), userDataset.Title, user.EmailId);
            return result;
        }

        [HttpPut("userdatasets/{id:guid}/public")]
        public async Task<IActionResult> MakeDatasetPublic(Guid id)
        {
            // check if user has permissions
            if (await _accessProvider.IsAdmin() || (await _accessProvider.User()).UserRole == UserRole.Moderator)
            {
                return await SetUserDatasetState(id, UserDatasetState.PublicDataset, AccessType.Public);
            }

            throw new UnauthorizedAccessException("Admin Auth required.");
        }

        [HttpPut("userdatasets/{id:guid}/verified")]
        public async Task<IActionResult> MakeDatasetVerified(Guid id)
        {
            // check if user has permissions
            if (await _accessProvider.IsAdmin() || (await _accessProvider.User()).UserRole == UserRole.Moderator)
            {
                var userDataset = await _userDatasetRepository.GetAsync(x => x.Id == id);
                var result = await SetUserDatasetState(id, UserDatasetState.PrivateSharedWithConsortiumVerified,
                    AccessType.PrivateSharedVerified);
                var user = await _accessProvider.User();
                await _emailHelper.SendDatasetSharedVerified(userDataset.Id.ToString("D"), userDataset.Title,
                    user.EmailId);
                return result;
            }

            throw new UnauthorizedAccessException("Admin/Moderator Auth required.");
        }

        private async Task<IActionResult> SetUserDatasetState(Guid id, UserDatasetState userDatasetState,
            AccessType datasetAccessType)
        {
            var userDataset = await _userDatasetRepository.GetAsync(x => x.Id == id);
            var masterData =
                await _collectionRepository.GetAsync(x => x.CollectionId == userDataset.CollectionId);
            if (userDataset.State == userDatasetState ||
                userDataset.State < UserDatasetState.AvailableInModule)
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
                tenantData.AccessType = datasetAccessType;
                await _collectionRepository.UpdateAsync(tenantData);
            }

            // Check for metadata.
            if (userDataset.State != userDatasetState)
            {
                userDataset.State = userDatasetState;
                await _userDatasetRepository.UpdateAsync(userDataset);
            }

            masterData.AccessType = datasetAccessType;
            await _collectionRepository.UpdateAsync(masterData);

            return new OkResult();
        }

        [HttpDelete("userdatasets/{id:guid}")]
        public async Task<IActionResult> DeleteUserDataset(Guid id)
        {
            // check if user has delete permission and dataset is not public
            var isAdmin = await _accessProvider.IsAdmin();
            var userId = _currentUser.GetId().ToString();
            var userDataset = await (await _userDatasetRepository.GetQueryableAsync())
                .Where(x => isAdmin || x.UserId == userId).FirstAsync(x => x.Id == id);
            if (!isAdmin && userDataset.State == UserDatasetState.PrivateSharedWithConsortium)
            {
                return new BadRequestObjectResult(
                    $"{userDataset.CollectionId} is Privately Shared with Consortium. Cannot be deleted by user.");
            }

            if (await _collectionRepository.AnyAsync(x => x.CollectionId == userDataset.CollectionId))
            {
                var masterData =
                    await _collectionRepository.GetAsync(x => x.CollectionId == userDataset.CollectionId);
                if (masterData.AccessType == AccessType.Public)
                {
                    return new BadRequestObjectResult(
                        $"{userDataset.CollectionId} is Public access collection. Cannot be deleted by user.");
                }
            }

            await _addCollectionHelper.DeleteUserDataset(userDataset.CollectionId);
            await _userDatasetRepository.DeleteAsync(x => x.Id == id);
            return new OkObjectResult("Deleted user Dataset");
        }

        private static UserDataset UpdateEntity(CreateOrUpdateUserDataset createUserDataset, UserDataset entity,
            UserDatasetState state)
        {
            entity.Title = createUserDataset.Title;
            entity.CollectionId = createUserDataset.CollectionId;
            entity.State = state;
            entity.Errors = Array.Empty<string>();
            return entity;
        }

        private async Task<Tenant> GetTenant(string collectionId)
        {
            var tenant = await _tenantRepository.FindByNameAsync(collectionId);
            if (tenant == null)
            {
                throw new EntityNotFoundException(
                    "Tenant/Collection Not Found. Please create using MasterCollectionStore API.");
            }

            return tenant;
        }
    }
}
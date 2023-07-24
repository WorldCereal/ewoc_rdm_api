using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using IIASA.WorldCereal.Rdm.Core;
using IIASA.WorldCereal.Rdm.Dtos;
using IIASA.WorldCereal.Rdm.Dtos.GeoJson;
using IIASA.WorldCereal.Rdm.Entity;
using IIASA.WorldCereal.Rdm.ServiceConfigs;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using Swashbuckle.AspNetCore.Annotations;
using Volo.Abp;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Volo.Abp.TenantManagement;

namespace IIASA.WorldCereal.Rdm.Services
{
    [RemoteService(Name = "OGC")]
    [ApiExplorerSettings(GroupName = "OGC")]
    public class ItemService : RdmAppService
    {
        private readonly ITenantRepository _tenantRepository;
        private readonly IItemsCodeStatsHelper _itemsCodeStatsHelper;
        private readonly IRepository<ItemEntity, long> _itemRepository;
        private readonly IGeoJsonHelper _geoJsonHelper;
        private readonly ICurrentTenant _currentTenant;
        private readonly IEwocUser _ewocUser;
        private readonly UserDatasetConfig _userDatasetConfig;

        public ItemService(ITenantRepository tenantRepository, IItemsCodeStatsHelper itemsCodeStatsHelper,
            IRepository<ItemEntity, long> itemRepository,
            IGeoJsonHelper geoJsonHelper, ICurrentTenant currentTenant,
            IEwocUser ewocUser,
            UserDatasetConfig userDatasetConfig)
        {
            _tenantRepository = tenantRepository;
            _itemsCodeStatsHelper = itemsCodeStatsHelper;
            _itemRepository = itemRepository;
            _geoJsonHelper = geoJsonHelper;
            _currentTenant = currentTenant;
            _ewocUser = ewocUser;
            _userDatasetConfig = userDatasetConfig;
        }

        /// <summary>
        /// Get single feature
        /// </summary>
        /// <remarks>Fetch the feature with id &#x60;featureId&#x60; in the feature collection with id &#x60;collectionId&#x60;.  Use content negotiation to request HTML or GeoJSON.</remarks>
        /// <param name="collectionId">local identifier of a collection</param>
        /// <param name="featureId">local identifier of a feature</param>
        /// <response code="200">fetch the feature with id &#x60;featureId&#x60; in the feature collection with id &#x60;collectionId&#x60;</response>
        /// <response code="404">The requested URI was not found.</response>
        /// <response code="500">A server error occurred.</response>
        [HttpGet("collections/{collectionId}/items/{featureId}")]
        [SwaggerOperation("GetFeature")]
        [SwaggerResponse(StatusCodes.Status200OK, type: typeof(FeatureGeoJson),
            description:
            "fetch the feature with id &#x60;featureId&#x60; in the feature collection with id &#x60;collectionId&#x60;")]
        [SwaggerResponse(StatusCodes.Status404NotFound, "No Items found.")]
        [SwaggerResponse(StatusCodes.Status500InternalServerError, "A server error occurred.")]
        public async Task<IActionResult> GetFeature(string collectionId, string featureId)
        {
            var tenant = await GetTenant(collectionId);
            using (CurrentTenant.Change(tenant.Id))
            {
                var item = await _itemRepository.GetAsync(x => x.SampleId == featureId);
                if (item == null)
                {
                    return new NotFoundResult();
                }

                var geoJsonString = _geoJsonHelper.GetFeature(item);
                dynamic jObject = JsonConvert.DeserializeObject(geoJsonString);
                jObject.Next = 1;
                return GeoJsonHttpHelper.GetContentResult(jObject);
            }
        }

        /// <summary>
        /// Get Features From collection
        /// </summary>
        /// <remarks>Fetch features of the feature collection with sampleID and &#x60;collectionId&#x60;.</remarks>
        /// <param name="collectionId">local identifier of a collection</param>
        /// <param name="itemsRequestFilter">Filter to select features based on landcover, crop type, irrigation codes and Validity time range and Bounding box</param>
        [HttpGet("collections/{collectionId}/items")]
        [SwaggerOperation("GetFeatures")]
        [SwaggerResponse(200, type: typeof(FeatureCollectionGeoJSON),
            description:
            "The response is a document consisting of features in the collection. The features included in the response are determined by the server based on the query parameters of the request. To support access to larger collections without overloading the client, the API supports paged access with links to the next page, if more features are selected that the page size.  The &#x60;bbox&#x60; and &#x60;datetime&#x60; parameter can be used to select only a subset of the features in the collection (the features that are in the bounding box or time interval). The &#x60;bbox&#x60; parameter matches all features in the collection that are not associated with a location, too. The &#x60;datetime&#x60; parameter matches all features in the collection that are not associated with a time stamp or interval, too.  The &#x60;limit&#x60; parameter may be used to control the subset of the selected features that should be returned in the response, the page size. Each page may include information about the number of selected and returned features (&#x60;numberMatched&#x60; and &#x60;numberReturned&#x60;) as well as links to support paging (link relation &#x60;next&#x60;).")]
        [SwaggerResponse(StatusCodes.Status400BadRequest, "A query parameter has an invalid value.")]
        [SwaggerResponse(StatusCodes.Status404NotFound, "No Items found.")]
        [SwaggerResponse(StatusCodes.Status500InternalServerError, "A server error occurred.")]
        public async Task<IActionResult> GetFeatures(string collectionId, ItemsRequestFilter itemsRequestFilter)
        {
            var tenant = await GetTenant(collectionId);
            using (CurrentTenant.Change(tenant.Id))
            {
                var query = SearchHelper.GetItemEntitiesQuery(itemsRequestFilter, _itemRepository);
                var items = await GetFilteredItems(itemsRequestFilter);
                if (items.Any() == false)
                {
                    return new NotFoundResult();
                }

                var jObject = await GetJObject(itemsRequestFilter, items, query);
                return GeoJsonHttpHelper.GetContentResult(jObject);
            }
        }

        [HttpGet("collection/{collectionId}/items/download")]
        public async Task<IActionResult> DownloadCollection(string collectionId,
            DownloadDatasetRequest pagedResultRequestDto)
        {
            if (_ewocUser.IsAdmin == false)
            {
                return new UnauthorizedObjectResult("Admin Auth required.");
            }

            var tenant = await _tenantRepository.FindByNameAsync(collectionId);
            var tempPath = Path.Combine(_userDatasetConfig.TempFolder, Guid.NewGuid().ToString("N"));
            using (_currentTenant.Change(tenant.Id))
            {
                var items = await _itemRepository.OrderBy(x => x.Id).Skip(pagedResultRequestDto.SkipCount)
                    .Take(pagedResultRequestDto.MaxResultCount).ToListAsync();
                GeoJsonHelper.WriteDataset(items, tempPath, collectionId);
            }

            await using (var stream = new MemoryStream())
            {
                SearchHelper.ZipFiles(tempPath, stream);
                stream.Position = 0;
                Directory.Delete(tempPath, true);
                return new FileContentResult(await stream.GetAllBytesAsync(),
                    "application/octet-stream") {FileDownloadName = $"{collectionId}.zip"};
            }
        }

        [HttpGet("collections/{collectionId}/items/codestats")]
        public async Task<CodeStats> GetCodeListStats(string collectionId)
        {
            var tenant = await GetTenant(collectionId);
            using (CurrentTenant.Change(tenant.Id))
            {
                return await _itemsCodeStatsHelper.GetCodeStats(collectionId, tenant.Id);
            }
        }

        private async Task<dynamic> GetJObject(ItemsRequestFilter itemsRequestFilter, List<ItemEntity> items,
            IQueryable<ItemEntity> query)
        {
            var geoJsonString = _geoJsonHelper.GetFeatureCollection(items);
            dynamic jObject = JsonConvert.DeserializeObject(geoJsonString);
            var filteredCount = await query.CountAsync();
            jObject.NumberMatched = filteredCount;
            jObject.NumberReturned = items.Count;
            jObject.UtcTimeStamp = DateTime.UtcNow.ToString("u");
            jObject.SkipCount = itemsRequestFilter.SkipCount;
            return jObject;
        }

        private async Task<List<ItemEntity>> GetFilteredItems(ItemsRequestFilter filter)
        {
            var query = SearchHelper.GetItemEntitiesQuery(filter, _itemRepository);

            return await _itemRepository.Where(x =>
                query.OrderBy(o => o.Id).Skip(filter.SkipCount).Take(filter.MaxResultCount)
                    .Select(i => i.Id)
                    .Contains(x.Id)).AsNoTracking().ToListAsync();
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
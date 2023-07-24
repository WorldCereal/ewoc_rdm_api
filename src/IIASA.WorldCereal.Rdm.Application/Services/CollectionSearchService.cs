using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using IIASA.WorldCereal.Rdm.Core;
using IIASA.WorldCereal.Rdm.Dtos;
using IIASA.WorldCereal.Rdm.Entity;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NetTopologySuite.Geometries;
using Swashbuckle.AspNetCore.Annotations;
using Volo.Abp;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.TenantManagement;

namespace IIASA.WorldCereal.Rdm.Services
{
    [RemoteService(Name = "OGC")]
    [ApiExplorerSettings(GroupName = "OGC")]
    public class CollectionSearchService : RdmAppService
    {
        private readonly IRepository<CollectionMetadataEntity, Guid> _masterCollectionStore;
        private readonly ILogger<CollectionSearchService> _logger;
        private readonly ITenantRepository _tenantRepository;

        public CollectionSearchService(IRepository<CollectionMetadataEntity,Guid> masterCollectionStore,
            ILogger<CollectionSearchService> logger,
            ITenantRepository tenantRepository)
        {
            _masterCollectionStore = masterCollectionStore;
            _logger = logger;
            _tenantRepository = tenantRepository;
        }


        /// <summary>
        /// Get collections
        /// </summary>
        /// <remarks>Gets list of Collections based on query parameters. Empty parameter are treated as * and all values are returned. BBox is mandatory</remarks>
        /// <param name="itemSearch">Filter to select features based on landcover, crop type, irrigation codes and validity time range and bounding box</param>
        [HttpGet]
        [SwaggerOperation("Search Collections")]
        [SwaggerResponse(statusCode: 200, type: typeof(CollectionMetadataDto),
            description:
            "The response is a document consisting of list collections. The response are determined by the server based on the query parameters of the request. To support access to larger collections without overloading the client, the API supports paged access with links to the next page, if more collections are selected that the page size.  The &#x60;bbox&#x60; and &#x60;datetime&#x60; parameter can be used to select only a subset of the collections.")]
        [SwaggerResponse(statusCode: StatusCodes.Status400BadRequest, "A query parameter has an invalid value.")]
        [SwaggerResponse(statusCode: StatusCodes.Status404NotFound, "No Items found.")]
        [SwaggerResponse(statusCode: StatusCodes.Status500InternalServerError, description: "A server error occurred.")]
        [Route("collections/search")]
        public async Task<IActionResult> Get([FromQuery] ItemSearch itemSearch)
        {
            _logger.LogInformation($"Getting collections for search query");
            if (itemSearch.Bbox == null || ValidationBBox(itemSearch.GetBoundingBoxPoints()) == false)
            {
                return new BadRequestResult();
            }

            var query = SearchHelper.GetCollectionQuery(itemSearch, _masterCollectionStore);
            var collections = await query.ToListAsync();

            var result = collections.Select(x => ObjectMapper.Map<CollectionMetadataEntity, CollectionMetadataDto>(x));

            return new OkObjectResult(result);
        }

        private bool ValidationBBox(IEnumerable<CoordinateDto> boundingBoxPoints)
        {
            var geometryFactory = new GeometryFactory(new PrecisionModel(), GeoJsonHelper.GeometryWgs84Srid);
            var boundingBox = new Polygon(new LinearRing(boundingBoxPoints
                .Select(x => new Coordinate(x.Latitude, x.Longitude)).ToArray()), geometryFactory);

            return boundingBox.IsValid;
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
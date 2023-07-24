using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using IIASA.WorldCereal.Rdm.Core;
using IIASA.WorldCereal.Rdm.Dtos;
using IIASA.WorldCereal.Rdm.Dtos.GeoJson;
using IIASA.WorldCereal.Rdm.Entity;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using Swashbuckle.AspNetCore.Annotations;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;

namespace IIASA.WorldCereal.Rdm.Services
{
    [RemoteService(Name = "OGC")]
    [ApiExplorerSettings(GroupName = "OGC")]
    public class PatchSampleService : RdmAppService
    {
        private readonly IRepository<SampleEntity, long> _sampleRepository;
        private readonly IEwocUser _ewocUser;
        private readonly IGeoJsonHelper _geoJsonHelper;

        public PatchSampleService(IRepository<SampleEntity,long> sampleRepository,IEwocUser ewocUser, IGeoJsonHelper geoJsonHelper)
        {
            _sampleRepository = sampleRepository;
            _ewocUser = ewocUser;
            _geoJsonHelper = geoJsonHelper;
        }

        [HttpGet("samples")]
        [SwaggerOperation("GetSamples")]
        [SwaggerResponse(statusCode: 200, type: typeof(FeatureCollectionGeoJSON), description: "list of samples for given BBox and filters")]
        [SwaggerResponse(statusCode: StatusCodes.Status400BadRequest, "A query parameter has an invalid value.")]
        [SwaggerResponse(statusCode: StatusCodes.Status404NotFound, "No Samples found.")]
        [SwaggerResponse(statusCode: StatusCodes.Status500InternalServerError, description: "A server error occurred.")]
        public async Task<IActionResult> Get(SampleSearchFilter sampleSearchFilter)
        {
            var query = SearchHelper.GetSampleQuery(sampleSearchFilter, _sampleRepository);
            var samples = await GetSamples(sampleSearchFilter, query);
            if (samples.Any() == false)
            {
                return new NotFoundResult();
            }

            var geoJsonString = _geoJsonHelper.GetFeatureCollection(samples);
            dynamic jObject = JsonConvert.DeserializeObject(geoJsonString);
            jObject.Total = await _sampleRepository.CountAsync();
            jObject.NumberMatched = query.Count();
            jObject.NumberReturned = samples.Count;
            jObject.Skipped = sampleSearchFilter.SkipCount;
            return GeoJsonHttpHelper.GetContentResult(jObject);
        }

        [HttpPost("samples/{version}")]
        [SwaggerOperation("AddSample")]
        [SwaggerResponse(statusCode: StatusCodes.Status202Accepted, "If valid samples are sent.")]
        [SwaggerResponse(statusCode: StatusCodes.Status400BadRequest, "Invalid input.")]
        [SwaggerResponse(statusCode: StatusCodes.Status500InternalServerError, description: "A server error occurred.")]
        public async Task<IActionResult> AddSample(double? version, FeatureCollectionGeoJSON featureCollection)
        {
            
            if (version.HasValue == false)
            {
                return new BadRequestObjectResult("error: version data");
            }
            if (_ewocUser.IsAdmin == false)
            {
                return new UnauthorizedObjectResult("Admin Auth required.");
            }

            var sampleEntities = _geoJsonHelper.GetSamples(featureCollection, version.Value);
            foreach (var sampleEntity in sampleEntities)
            {
                await _sampleRepository.InsertAsync(sampleEntity); //TODO use bulk insert
            }

            return new AcceptedResult();
        }

        private async Task<List<SampleEntity>> GetSamples(SampleSearchFilter sampleSearchFilter, IQueryable<SampleEntity> query)
        {
            return await _sampleRepository.Where(x => query.OrderBy(entity => entity.Id).Skip(sampleSearchFilter.SkipCount)
                .Take(sampleSearchFilter.MaxResultCount).Select(s => s.Id).Contains(x.Id)).AsNoTracking().ToListAsync();
        }
    }
}
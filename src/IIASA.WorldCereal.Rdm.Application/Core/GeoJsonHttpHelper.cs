using System.Net;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;

namespace IIASA.WorldCereal.Rdm.Core
{
    public static class GeoJsonHttpHelper
    {
        public static ContentResult GetContentResult(dynamic jObject)
        {
            var serializeObject = JsonConvert.SerializeObject(jObject);
            return new()
            {
                Content = serializeObject, ContentType = "application/json",
                StatusCode = (int) HttpStatusCode.OK
            };
        }
    }
}
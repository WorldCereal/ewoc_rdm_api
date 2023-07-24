using System.Net;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;

namespace IIASA.WorldCereal.Rdm.Services
{
    public static class GeoJsonHttpHelper
    {
        public static ContentResult GetContentResult(dynamic jObject)
        {
            return new()
            {
                Content = JsonConvert.SerializeObject(jObject), ContentType = "application/json",
                StatusCode = (int) HttpStatusCode.OK
            };
        }
    }
}
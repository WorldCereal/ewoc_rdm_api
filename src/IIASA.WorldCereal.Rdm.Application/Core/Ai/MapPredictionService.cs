using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Flurl.Http;
using IIASA.WorldCereal.Rdm.ServiceConfigs;

namespace IIASA.WorldCereal.Rdm.Core.Ai;

public class MapPredictionService
{
    private FlurlClient _client;

    public MapPredictionService(UserDatasetConfig userDatasetConfig)
    {
        _client = new FlurlClient { BaseUrl = userDatasetConfig.AiServiceBaseUrl };
    }

    public async Task<Dictionary<string, long>?> GetCropPredictions(string[] inputs)
    {
        var request = _client.Request("get_croptype_suggestions");
        foreach (var crop in inputs)
        {
            request.AppendQueryParam("crops_to_map", crop);
        }

        var response = await request.GetStringAsync();

        var dictionary = JsonSerializer.Deserialize<Dictionary<string, long>>(response);

        return dictionary;
    }

    public async Task<Dictionary<string, int>?> GetIrrigationPredictions(string[] inputs)
    {
        var request = _client.Request("get_irrigation_suggestions");
        foreach (var irrVal in inputs)
        {
            request.AppendQueryParam("irrigation_types_to_map", irrVal);
        }

        var response = await request.GetStringAsync();

        var dictionary = JsonSerializer.Deserialize<Dictionary<string, int>>(response);

        return dictionary;
    }

    public async Task<bool> DoH3Indexing(string dbName)
    {
        var request = _client.Request("assign_h3_index");
        request.AppendQueryParam("databaseName", dbName);

        var res = await request.GetAsync();

        return res.ResponseMessage.IsSuccessStatusCode;
    }

    public async Task<bool> DoSampleExtraction(string dbName)
    {
        var request = _client.Request("create_extract_flag");
        request.AppendQueryParam("databaseName", dbName);

        var res = await request.GetAsync();

        return res.ResponseMessage.IsSuccessStatusCode;
    }
    
    public async Task<string> CheckAndReProjectTo4326(string filePath)
    {
        try
        {
            var request = _client.Request("reproject_to_4326");
            request.AppendQueryParam("path_to_dataset", filePath);

            var res = await request.GetAsync();

            return await res.GetStringAsync();
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            return "error";
        }
    }
    
    public async Task<string> GetRegion(string wktPolygon)
    {
        try
        {
            var request = _client.Request("get_dataset_region_code");
            request.AppendQueryParam("geom_wkt", wktPolygon);

            var res = await request.GetAsync();

            var regionInfo = await res.GetJsonAsync<RegionInfo>();
            return regionInfo.Message;
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            return "GL";
        }
    }
}

public class RegionInfo
{
    public string Message { get; set; }
}
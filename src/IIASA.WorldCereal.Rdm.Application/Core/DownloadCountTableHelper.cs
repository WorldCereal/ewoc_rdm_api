using System;
using System.Threading.Tasks;
using Azure;
using Azure.Data.Tables;
using IIASA.WorldCereal.Rdm.Dtos;
using IIASA.WorldCereal.Rdm.ExcelOps;

namespace IIASA.WorldCereal.Rdm.Core;

public interface IDownloadCountTableHelper
{
    Task<DownloadCounts> GetDownloadCounts(string collectionId);
    Task<DownloadCounts> UpdateCount(string collectionId, string countName);
}

public class DownloadCountTableHelper : IDownloadCountTableHelper
{
    private readonly AzureConfig _azureConfig;
    private readonly string _partitionKey;

    public DownloadCountTableHelper(AzureConfig azureConfig)
    {
        _azureConfig = azureConfig;
        _partitionKey = "ewoc-rdm";
    }

    public async Task<DownloadCounts> GetDownloadCounts(string collectionId)
    {
        // New instance of the TableClient class
        var table = GetTable();
        var stats = await table.GetEntityIfExistsAsync<CollectionStatsTableEntity>(_partitionKey, collectionId);
        if (stats.HasValue)
        {
            var entity = stats.Value;
            return GetDownloadCountsResult(collectionId, entity);
        }

        var defaultEntity = GetDefault(collectionId);
        await table.AddEntityAsync(defaultEntity);
        return new DownloadCounts() { CollectionId = collectionId };
    }

    private static DownloadCounts GetDownloadCountsResult(string collectionId, CollectionStatsTableEntity entity)
    {
        return new DownloadCounts()
        {
            CollectionId = collectionId,
            Dataset = entity.Dataset,
            Sample = entity.Sample,
            Metadata = entity.Metadata,
        };
    }

    private TableClient GetTable()
    {
        TableServiceClient tableServiceClient = new TableServiceClient(_azureConfig.StorageConnectionStrings);
        TableClient tableClient = tableServiceClient.GetTableClient(
            tableName: "downloadCountsTable"
        );
        return tableClient;
    }

    public async Task<DownloadCounts> UpdateCount(string collectionId, string countName)
    {
        var table = GetTable();
        var stats = await table.GetEntityAsync<CollectionStatsTableEntity>(_partitionKey, collectionId);
        var entity = new CollectionStatsTableEntity();
        if (stats.HasValue)
        {
            entity = stats.Value;
        }

        if (countName == ExcelMarkers.DatasetDownloadCount)
        {
            entity.Dataset += 1;
        }

        if (countName == ExcelMarkers.DatasetSampleDownloadCount)
        {
            entity.Sample += 1;
        }

        if (countName == ExcelMarkers.DatasetMetadataDownloadCount)
        {
            entity.Metadata += 1;
        }

        try
        {
            if (stats.HasValue)
            {
                await table.UpdateEntityAsync(entity, stats.Value.ETag);
            }
            else
            {
                await table.AddEntityAsync(entity);
            }
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            throw;
        }

        return GetDownloadCountsResult(collectionId, entity);
    }

    private CollectionStatsTableEntity GetDefault(string collectionId)
    {
        return new CollectionStatsTableEntity()
        {
            PartitionKey = _partitionKey,
            RowKey = collectionId,
            Dataset = 0,
            Sample = 0,
            Metadata = 0
        };
    }
}

// C# record type for items in the table
public record CollectionStatsTableEntity : ITableEntity
{
    public string RowKey { get; set; } = default!;

    public string PartitionKey { get; set; } = default!;

    public int Dataset { get; set; }

    public int Sample { get; set; }

    public int Metadata { get; set; }

    public ETag ETag { get; set; } = default!;

    public DateTimeOffset? Timestamp { get; set; } = default!;
}
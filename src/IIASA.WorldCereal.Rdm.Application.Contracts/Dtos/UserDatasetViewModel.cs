using System;
using System.Text.Json.Serialization;
using IIASA.WorldCereal.Rdm.Enums;
using Volo.Abp.Application.Dtos;

namespace IIASA.WorldCereal.Rdm.Dtos
{
    public class UserDatasetViewModel : AuditedEntityDto<Guid>
    {
        public string Title { get; set; }
        public string CollectionId { get; set; }
        public string[] Columns { get; set; } = [];
        public string EwocCodeCol { get; set; } = string.Empty;
        public string ValDateCol { get; set; } = string.Empty;
        public string IrrCol { get; set; } = string.Empty;
        public UserDatasetState State { get; set; }
        public string[] Errors { get; set; } = [];
        public string UserId { get; set; } = string.Empty;
        public string DatasetExtractedPath { get; set; } = string.Empty;
    }

    public class DatasetEventViewModel : CreationAuditedEntityDto<int>
    {
        public EventType Type { get; set; }
        public string[] Comments { get; set; }
        public bool CanSubmit { get; set; }
    }

    public class DownloadDatasetRequest : PagedResultRequestDto
    {
        public DownloadDatasetRequest()
        {
            MaxResultCount = 1000;
        }
    }

    public class ColumnMap
    {
        [JsonPropertyName("valid_time")] public string ValidityTimeColumnName { get; set; }

        [JsonPropertyName("ewoc_code")] public string EwocCodeColumnName { get; set; }

        [JsonPropertyName("irrigation_status")]
        public string IrrColumnName { get; set; }
    }

    public class CodeMap
    {
        public string Label { get; set; }

        public long? Code { get; set; }
    }
    
    public class CodeMapWithAttribute
    {
        public string AttributeName { get; set; }
        
        public string Label { get; set; }

        public long? Code { get; set; }
    }

    public class AttributeMapInfo
    {
        public CodeMapWithAttribute[] Maps { get; set; }

        public int SelectedYear { get; set; }
    }

    public class AddUrlsForConsortiumUpload
    {
        public string[] Urls { get; set; }

        public bool NeedsUpdate { get; set; }
    }
}
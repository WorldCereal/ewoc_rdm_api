using System;
using Volo.Abp.BackgroundJobs;

namespace IIASA.WorldCereal.Rdm.Jobs.ColumnMapping;

[BackgroundJobName("User dataset Columns Mapping job")]
public class ColumnMappingJobArgs
{
    public Guid UserDatasetId { get; set; }
    public string Path { get; set; } = string.Empty;

    public string DatasetExtractedPath { get; set; } = string.Empty;
    
    public string EwocColumnName { get; set; } = string.Empty;
    
    public string ValDateColumnName { get; set; } = string.Empty;
    
    public string DateFormat { get; set; } = string.Empty;
    public string IrrColumnName { get; set; } = string.Empty; // Empty if no irrigation
    
    public bool IsManualValidityDate { get; set; }
    
    public bool IsValAttributeDateTimeType { get; set; }
}
using System;
using Volo.Abp.BackgroundJobs;

namespace IIASA.WorldCereal.Rdm.Jobs.Harmonization;

[BackgroundJobName("User dataset harmonise job")]
public class HarmoniseArgs
{
    public Guid UserDatasetId { get; set; }

    public string Path { get; set; } = string.Empty;

    public string DatasetExtractedPath { get; set; } = string.Empty;

    public string ColumnConfigFile { get; set; } = string.Empty;

    public string EwocMappingFile { get; set; } = string.Empty;

    public string IrrMappingFile { get; set; } = string.Empty;
}
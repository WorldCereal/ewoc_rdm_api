using System;
using Volo.Abp.BackgroundJobs;

namespace IIASA.WorldCereal.Rdm.Jobs.ColumnExtraction;

[BackgroundJobName("User dataset Columns Extraction job")]
public class ColumnExtractionJobArgs
{
    public Guid UserDatasetId { get; set; }

    public string Path { get; set; }

    public string DatasetExtractedPath { get; set; }
}

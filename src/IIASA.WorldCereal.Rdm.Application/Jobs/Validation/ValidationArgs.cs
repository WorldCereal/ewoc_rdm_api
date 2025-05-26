using System;
using Volo.Abp.BackgroundJobs;

namespace IIASA.WorldCereal.Rdm.Jobs.Validation
{
    [BackgroundJobName("User dataset validation job")]
    public class ValidationArgs
    {
        public Guid UserDatasetId { get; set; }

        public string VectorFilePath { get; set; }

        public string DatasetExtractedPath { get; set; }
    }
}

using System;
using Volo.Abp.BackgroundJobs;

namespace IIASA.WorldCereal.Rdm.Jobs.UploadUserDataset
{
    [BackgroundJobName("Provision User dataset job")]
    public class ProvisionUserDataSetJobArgs
    {
        public Guid UserDatasetId { get; set; }

        public string VectorFilePath { get; set; }

        public string DatasetExtractedPath { get; set; }
    }
}

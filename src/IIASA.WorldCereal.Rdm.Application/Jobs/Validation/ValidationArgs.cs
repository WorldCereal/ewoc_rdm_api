using System;
using Volo.Abp.BackgroundJobs;

namespace IIASA.WorldCereal.Rdm.Jobs.Validation
{
    [BackgroundJobName("User dataset validation job")]
    public class ValidationArgs
    {
        public Guid UserDatasetId { get; set; }

        public string Path { get; set; }

        public string DirectoryPathToClean { get; set; }

        public string ZipFilePath { get; set; }
    }
}

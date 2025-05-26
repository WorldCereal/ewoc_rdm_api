using System;
using Volo.Abp.BackgroundJobs;

namespace IIASA.WorldCereal.Rdm.Jobs.Sampling;

[BackgroundJobName("User dataset Sampling job")]
public class SamplingJobArgs
{
    public Guid UserDatasetId { get; set; }
    public string DirectoryPathToClean { get; set; }
}
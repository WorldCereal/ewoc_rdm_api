using Volo.Abp.Domain.Entities.Auditing;

namespace IIASA.WorldCereal.Rdm.Entity
{
    public class ConsortiumCollection : AuditedEntity<int>
    {
        public string DownloadZipUrl { get; set; }
        
        public bool Successful { get; set; }
        
        public string Errors{ get; set; }
        
        public bool NeedsUpdate{ get; set; }
        
        public bool OverwriteFeatures{ get; set; }
        
        public bool OverwriteOtherProps{ get; set; } // metadata and download link etc.
    }
}
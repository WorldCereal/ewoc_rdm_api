using System;
using IIASA.WorldCereal.Rdm.Enums;
using Volo.Abp.Domain.Entities.Auditing;

namespace IIASA.WorldCereal.Rdm.Entity;

public class ExternalUserAccount : FullAuditedEntity<int>
{
    public Guid UserId { get; set; }
    public string UserName { get; set; }
    public string EmailId { get; set; }
    public UserRole UserRole { get; set; }
}
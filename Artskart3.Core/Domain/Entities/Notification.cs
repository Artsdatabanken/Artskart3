using Artskart3.Core.Domain.Entities.Base;
using Artskart3.Core.Domain.Enums;

namespace Artskart3.Core.Domain.Entities;

public class Notification : BaseEntity<Guid>
{
    public AlertType AlertType { get; set; }
    public required string Heading { get; set; }
    public required string Description { get; set; }
    public DateTime? PublishFrom { get; set; }
    public DateTime? PublishUntil { get; set; }
    public DateTime? EventStart { get; set; }
    public DateTime? EventEnd { get; set; }
    public bool CanClose { get; set; }
}

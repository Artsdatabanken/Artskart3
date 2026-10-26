using Artskart3.Core.Domain.Entities.Base;
using Artskart3.Core.Domain.Enums;

namespace Artskart3.Core.Domain.Entities;

public class Notification : BaseEntity<Guid>
{
    public AlertType AlertType { get; set; }
    public required string Heading { get; set; }
    public required string Description { get; set; }
    public DateTime? StartDateTime { get; set; }
    public DateTime? EndDateTime { get; set; }
    public DateOnly? StartDisplayDate { get; set; }
    public DateOnly? EndDisplayDate { get; set; }
    public bool CanClose { get; set; }
}

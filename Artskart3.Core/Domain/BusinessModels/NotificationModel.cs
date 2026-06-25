using Artskart3.Core.Domain.Enums;

namespace Artskart3.Core.Domain.BusinessModels;

public class NotificationModel
{
    public AlertType Type { get; set; }
    public string Heading { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateTime? StartDateTime { get; set; }
    public DateTime? EndDateTime { get; set; }
    public DateOnly? StartDisplayDate { get; set; }
    public DateOnly? EndDisplayDate { get; set; }
    public bool CanClose { get; set; }
}

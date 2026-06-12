using Artskart3.Core.Domain.Enums;

namespace Artskart3.Core.Domain.BusinessModels;

public class WarningModel
{
    public AlertType Type { get; set; }
    public string Heading { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateOnly? Date { get; set; }
    public DateOnly? StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public DateTime? EventDateTime { get; set; }
    public DateTime? StartDateTime { get; set; }
    public DateTime? EndDateTime { get; set; }
    public DateOnly? StartDisplayDate { get; set; }
    public DateOnly? EndDisplayDate { get; set; }
    public bool CanClose { get; set; }
}

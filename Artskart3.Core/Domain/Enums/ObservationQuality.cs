using System.ComponentModel;

namespace Artskart3.Core.Domain.Enums
{
    public enum ObservationQuality
    {
        [Description("Quality of observation has not been checked")]
        Unchecked = 0,
        [Description("Observation that has been checked and deemed to be of good quality")]
        Good = 1,
        [Description("Observation that has been checked and deemed to be of bad quality")]
        QualityIssue = 2,
        [Description("Observation that is hidden by default unless filtered to be visible")]
        QualityIssueHidden = 3
    }
}

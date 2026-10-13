namespace Artskart3.Core.Application.Import.Models;

/// <summary>
/// En merknad om ett felt på én post: verdien kunne ikke tolkes, eller den ble tolket
/// på en måte som skal kunne spores tilbake. Port av Artskart 2s <c>Issue</c>.
///
/// Dette er ikke en avvisning. Posten importeres; merknaden følger med til
/// <c>ProcessRecordResult</c>-siden av importen (fase 9).
/// </summary>
public class ImportIssue
{
    public ImportIssue(string term, object? originalValue, ImportIssueRemark remark)
    {
        Term = term;
        Original = originalValue?.ToString() ?? string.Empty;
        Remark = remark;
    }

    /// <summary>Darwin Core-feltet merknaden gjelder.</summary>
    public string Term { get; }

    /// <summary>Verdien som sto i cachen, uendret.</summary>
    public string Original { get; }

    public ImportIssueRemark Remark { get; }
}

/// <summary>
/// Tallverdiene er Artskart 2s, og de er beholdt fordi de ender opp i importens
/// resultatrader. Nye verdier skal følge samme inndeling: feil under 100, info over
/// 100, advarsel over 200.
/// </summary>
public enum ImportIssueRemark
{
    NoValidInteger = 10,
    CoordinateUncertaintySetFromMgrsCoordinate = 110
}

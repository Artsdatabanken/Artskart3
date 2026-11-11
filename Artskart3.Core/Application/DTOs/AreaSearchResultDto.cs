namespace Artskart3.Core.Application.DTOs;

/// <summary>
/// Et fylke, en kommune eller et havområde fra områdesøket i filteret, gjeldende eller
/// historisk.
/// </summary>
public class AreaSearchResultDto
{
    public int Id { get; set; }
    public string Fid { get; set; } = null!;
    public string Name { get; set; } = null!;
    public int AreaTypeId { get; set; }
    public bool IsCurrent { get; set; }

    /// <summary>
    /// Fylket en kommune hørte til, via ParentFid. Skiller like kommunenavn fra hverandre.
    /// Null for andre områdetyper.
    /// </summary>
    public string? CountyName { get; set; }

    /// <summary>
    /// Året et historisk område sluttet å gjelde, vist som «før {år}». Null for gjeldende
    /// områder. Se <see cref="Services.HistoricalAreaRules"/>.
    /// </summary>
    public int? EndYear { get; set; }

    /// <summary>
    /// Null for historiske områder, der Area.ObservationCount ikke er oppdatert.
    /// </summary>
    public int? ObservationCount { get; set; }
}

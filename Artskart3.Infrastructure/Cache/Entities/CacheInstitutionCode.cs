namespace Artskart3.Infrastructure.Cache.Entities;

/// <summary>
/// Raden i <c>Artskart2Cache.InstitutionCodes</c> — harvests oppslagstabell for
/// institusjonskoder. Importen leser institusjonskoden fra blobben, ikke herfra;
/// tabellen er med fordi den er den eneste måten å se hvilke koder cachen inneholder
/// uten å pakke ut 60 millioner blober.
/// </summary>
public class CacheInstitutionCode
{
    public int Id { get; set; }

    public string? Code { get; set; }
}

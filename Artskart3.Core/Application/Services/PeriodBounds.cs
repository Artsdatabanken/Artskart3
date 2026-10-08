namespace Artskart3.Core.Application.Services;

/// <summary>
/// Datogrensene et årsintervall i filteret oversettes til.
///
/// Ligger for seg selv fordi FIRE steder trenger dem og må være enige: de tre
/// filterstiene i SearchRepository og minnelageret i PolygonLocationStore. Og
/// tellebufferen bøtter på år * 100 + måned, så grensene må dekke nøyaktig de
/// samme observasjonene som bøtteintervallet gjør — ellers svarer buffer og
/// telling ulikt for samme filter.
///
/// ØVRE GRENSE ER EKSKLUSIV MED VILJE
/// Den opprinnelige formen var <c>&lt;= 31.12 23:59:59</c>. DateTimeCollected er
/// <c>datetime2(7)</c>, så en observasjon klokka 23:59:59,5 nyttårsaften falt
/// utenfor et filter som eksplisitt inkluderte året — mens bufferen, som bare
/// kjenner år og måned, tok den med. «Før 1. januar året etter» fjerner både
/// avviket og den opprinnelige av-med-ett-feilen.
/// </summary>
public static class PeriodBounds
{
    public const int MinYear = 1;
    public const int MaxYear = 9999;

    /// <summary>Inklusiv nedre grense: 1. januar i året.</summary>
    public static DateTime InclusiveLower(int year) =>
        new(Math.Clamp(year, MinYear, MaxYear), 1, 1);

    /// <summary>
    /// Eksklusiv øvre grense: 1. januar året etter. For det siste året som finnes
    /// gir den <see cref="DateTime.MaxValue"/> — <c>new DateTime(10000, 1, 1)</c>
    /// kaster, og et filter med år 9999 skal ikke bli en 500.
    /// </summary>
    public static DateTime ExclusiveUpper(int year)
    {
        var klemt = Math.Clamp(year, MinYear, MaxYear);
        return klemt >= MaxYear ? DateTime.MaxValue : new DateTime(klemt + 1, 1, 1);
    }
}

namespace Artskart3.Core.Domain.Entities;

/// <summary>
/// Forhåndsberegnede områdeantall for ett enkelt filter.
///
/// Én rad per (dimensjon, bøtte, utdatacelle). «Bøtte» er verdien for
/// enkeltverdi-dimensjoner (kategori 12, atferd 3), en kodet verdi for
/// intervall-dimensjoner (periode = år * 100 + måned), og en syntetisk id for
/// flerverdi-dimensjoner (verneområde, prosjekt) — se
/// <see cref="AreaCountCacheBucketMember"/>.
///
/// Målt på produksjonslik data: 1 448 814 rader for alle 13 dimensjoner mot alle
/// 3 694 utdataceller.
///
/// EntityTypeId er int, ikke tinyint: områdetype-id-ene er ikke garantert små
/// (testdataene bruker 910001), og en tinyint ville stille avkortet dem.
/// </summary>
public class AreaCountCacheLevel1
{
    public byte DimensionId { get; set; }
    public int BucketId { get; set; }
    public int EntityTypeId { get; set; }
    public int EntityId { get; set; }
    public int ObservationCount { get; set; }
}

/// <summary>
/// Forhåndsberegnede områdeantall for to filtre samtidig.
///
/// <see cref="BucketA"/> hører til dimensjonen med lavest id i paret, <see cref="BucketB"/>
/// til den andre. <see cref="DimensionPairId"/> tildeles av CacheDimensionRegistry.
///
/// Målt: 36 320 085 rader for alle 78 par. Periode alene står for 19,5 millioner.
/// </summary>
public class AreaCountCacheLevel2
{
    public byte DimensionPairId { get; set; }
    public int BucketA { get; set; }
    public int BucketB { get; set; }
    public int EntityTypeId { get; set; }
    public int EntityId { get; set; }
    public int ObservationCount { get; set; }
}

/// <summary>
/// Medlemskap for flerverdi-dimensjonene: hvilke bøtter inneholder et gitt område
/// eller prosjekt.
///
/// En observasjon kan ligge i flere verneområder samtidig, så bøtta må være hele
/// mengden observasjonen tilhører — ikke enkeltområdet. Summerer man i stedet per
/// område, dobbelttelles de 1 059 410 observasjonene som ligger i mer enn ett.
///
/// Målt: verneområde har 4 176 distinkte kombinasjoner, prosjekt 10 112.
///
/// Klyngeindeksen er (DimensionId, MemberId, BucketId) fordi oppslaget alltid går
/// «gitt område X, hvilke bøtter» — aldri motsatt vei.
/// </summary>
public class AreaCountCacheBucketMember
{
    public byte DimensionId { get; set; }
    public int MemberId { get; set; }
    public int BucketId { get; set; }
}

/// <summary>
/// Tilstanden til bufferen. Én rad, <see cref="Id"/> = 1.
///
/// <see cref="SchemaVersion"/> beskriver formen på dimensjonsregisteret da bufferen
/// ble bygget. Legges en dimensjon til, endres pargruppe-id-ene, og en buffer bygget
/// før endringen ville svart på feil par. Oppslaget nekter å bruke en buffer med
/// annen SchemaVersion enn koden som leser den.
/// </summary>
public class AreaCountCacheState
{
    public byte Id { get; set; }
    public int SchemaVersion { get; set; }

    /// <summary>Building, Ready eller Failed. Kun Ready leses av oppslaget.</summary>
    public string Status { get; set; } = "Building";

    public DateTime? BuiltAt { get; set; }

    /// <summary>Antall rader i ObservationEntityIndex da bufferen ble bygget.</summary>
    public long? SourceRows { get; set; }

    public int? DurationSeconds { get; set; }
}

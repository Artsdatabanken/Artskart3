namespace Artskart3.Core.Application.Services.Interfaces;

/// <summary>
/// Oppstartslastet oppslag for områdehierarki (kommune→fylke) og Fid→EntityId-konvertering.
/// Data lastes én gang fra Area-tabellen og holdes i minne.
/// </summary>
public interface IAreaHierarchyService
{
    /// <summary>
    /// Returnerer fylkes-Fid for en gitt kommune-Fid, eller null hvis ukjent.
    /// </summary>
    string? GetCountyFid(string municipalityFid);

    /// <summary>
    /// Returnerer alle kommune-Fid-er som tilhører et gitt fylke.
    /// </summary>
    IReadOnlyList<string> GetMunicipalityFids(string countyFid);

    /// <summary>
    /// Konverterer en Fid-streng til EntityId (int) for bruk i ObservationEntityIndex.
    /// Håndterer historiske fylkes-Fid-er med understrek (f.eks. "15_2017" → 152017).
    /// </summary>
    int? FidToEntityId(string fid);

    /// <summary>
    /// Konverterer en verneområde-Fid til EntityId ved å fjerne "Naturbase VV"-prefiks.
    /// </summary>
    int? RestrictedAreaFidToEntityId(string fid);

    /// <summary>
    /// Batch-konvertering av Fid-er til EntityId-er. Ignorerer ugyldige Fid-er.
    /// </summary>
    int[] FidsToEntityIds(string[]? fids);

    /// <summary>
    /// Batch-konvertering av verneområde-Fid-er til EntityId-er. Ignorerer ugyldige Fid-er.
    /// </summary>
    int[] RestrictedAreaFidsToEntityIds(string[]? fids);

    /// <summary>
    /// Boksen som omslutter alle observasjonslokasjoner i området, eller null hvis
    /// området ikke har observasjoner med lokasjon.
    /// </summary>
    AreaBounds? GetAreaBounds(int entityTypeId, int entityId);

    /// <summary>
    /// Beholder bare de områdene som kan ha en observasjonslokasjon innenfor
    /// kartutsnittet. Et område hvis lokasjoner ikke når utsnittet kan ikke bidra
    /// til svaret, og er bare arbeid for databasen.
    ///
    /// Samme ID kan gjelde flere områdetyper — fylkes-ID-er slås opp mot både
    /// fylke og Svalbard — og da beholdes ID-en hvis minst én av typene når fram.
    ///
    /// Er utfallet tomt mens inndata ikke var det, kan kallet svare tomt uten å
    /// spørre databasen i det hele tatt.
    /// </summary>
    int[] PruneToEnvelope(int[] entityIds, AreaBounds envelope, params int[] entityTypeIds);

    /// <summary>
    /// De av ID-ene som faktisk finnes som områder av den gitte typen.
    ///
    /// HVORFOR DETTE TRENGS
    /// Fylkes-ID-er slås opp mot BÅDE fylke og Svalbard, fordi et fylkesvalg på
    /// Svalbard ellers ville falt ut. Men Svalbard har bare seks områder —
    /// 2101 til 2105 og 2201 — og ingen av dem deler ID med et fylke. For et
    /// vanlig fylke er Svalbard-grenen altså alltid tom.
    ///
    /// Tom er ikke gratis. Grenen gjør predikatet til en ELLER over to
    /// områdetyper, og da kan ikke
    /// IX_OEI_AreaListView (EntityTypeId, EntityId, DateTimeCollected DESC)
    /// lenger levere radene ferdig sortert — optimizeren må slå sammen to
    /// strømmer, og materialiserer i stedet. Målt på største fylke, 7 976 997
    /// indeksrader: 2 ms med én gren, 719 ms med to.
    ///
    /// VET VI IKKE, BEHOLDER VI ID-EN
    /// Oppslaget bygger på områdeboksene, som lastes ved oppstart. Før første
    /// last er det tomt, og da returneres inndata uendret. Å tolke «vet ikke»
    /// som «finnes ikke» ville gitt et stille tomt svar i stedet for et tregt
    /// — den feilen er mye verre.
    /// </summary>
    int[] FilterToExistingAreas(int[] entityIds, int entityTypeId);
}

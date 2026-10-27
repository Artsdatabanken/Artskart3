namespace Artskart3.Infrastructure.Import.Writing;

/// <summary>
/// De avledede tabellene — <c>ObservationTaxonHierarchy</c>,
/// <c>ObservationEntityIndex</c> og de tre kolonnene på <c>Observation</c> som utledes
/// av barnetabellene.
///
/// DETTE ER NYTT I FORHOLD TIL ARTSKART 2. Der finnes ingen av dem: filtrene leste
/// grafen direkte. I Artskart 3 er de forutsetningen for at filtrene svarer riktig, og
/// de er importens ansvar (plan 4.5, punkt 1–4 og 8–10).
///
/// FASITEN ER <c>Scripts/BackfillAll.sql</c>, som fyller de samme tabellene for de
/// ~61 M radene som alt ligger der. Reglene her er hentet derfra — men ikke
/// fremgangsmåten:
///
/// BACKFILLEN GÅR I FEM PASS, DENNE I ETT. Backfillen må reparere en tabell på 131 M
/// rader der et tidligere forsøk stoppet halvveis, så den setter inn rader først og
/// fyller kolonner etterpå, hver seksjon idempotent og med eget vannmerke. En
/// importrunde har ingen halvferdige rader å reparere: den sletter radene for sine egne
/// observasjoner og setter inn nye med alle kolonnene utfylt. Resultatet er det samme,
/// og det er én setning i stedet for fem pass over de samme radene.
///
/// SLETT OG SETT INN, IKKE OPPDATER. <c>ObservationEntityIndex</c> har en
/// columnstore-indeks, og en <c>UPDATE</c> der markerer den gamle raden i
/// delete-bitmappen og legger en ny versjon i delta store — den gamle plassen blir
/// liggende til neste rebuild (plan 3.5).
///
/// REKKEFØLGEN ER LASTBÆRENDE: <c>ObservationTaxonHierarchy</c> må være skrevet før
/// <c>ObservationEntityIndex</c>, fordi indeksens fire taksonrangkolonner leses derfra
/// (plan 4.5, «4 → 3»).
///
/// BACKFILLEN ER ELDRE ENN SKJEMAET PÅ ETT PUNKT: den skriver
/// <c>CollectionOrgId</c> og <c>ObservationDataset</c>, som ble omdøpt til
/// <c>DatasetOrgId</c> og <c>ObservationProject</c> 1. september 2026. Kolonnen
/// <c>MonthCollected</c> på indeksen kom også etterpå og står ikke i planens tabell.
/// Navnene her følger skjemaet slik det er nå.
/// </summary>
public static class DerivedTableSql
{
    /// <summary>
    /// De 26 rangkolonnene, i <c>TaxonRank.Id</c>-rekkefølge.
    ///
    /// MÅ STÅ FØR <see cref="Steps"/>: statiske initialiserere kjører i tekstrekkefølge,
    /// og står lista etter, er den null når setningene bygges.
    /// </summary>
    private static readonly string[] Ranks =
    [
        "KingdomTaxonId", "SubkingdomTaxonId", "PhylumTaxonId", "SubphylumTaxonId",
        "SuperclassTaxonId", "ClassTaxonId", "SubclassTaxonId", "InfraclassTaxonId",
        "CohortTaxonId", "SuperorderTaxonId", "OrderTaxonId", "SuborderTaxonId",
        "InfraorderTaxonId", "SuperfamilyTaxonId", "FamilyTaxonId", "SubfamilyTaxonId",
        "TribeTaxonId", "SubtribeTaxonId", "GenusTaxonId", "SubgenusTaxonId",
        "SectionTaxonId", "SpeciesTaxonId", "SubspeciesTaxonId", "VarietyTaxonId",
        "FormTaxonId", "NotSetTaxonId"
    ];

    /// <summary>
    /// Setningene i den rekkefølgen de skal kjøres, etter <see cref="PublishSql"/> og i
    /// samme transaksjon.
    /// </summary>
    public static IReadOnlyList<PublishStep> Steps { get; } =
    [
        new("Observation (utledede kolonner)", ObservationColumns),
        new("ObservationTaxonHierarchy", TaxonHierarchy),
        new("ObservationEntityIndex", EntityIndex)
    ];

    public static string FullScript => string.Join("\n\n", Steps.Select(x => x.Sql));

    /// <summary>
    /// De tre kolonnene på <c>Observation</c> som utledes av barnetabellene, og som
    /// derfor ikke kan settes før barna er skrevet (plan 4.5 punkt 10).
    ///
    /// <c>RegistrationStatusId</c>: 1 = funn, 2 = ikke funnet (<c>Absent</c>, tagg 5),
    /// 3 = ikke gjenfunnet (<c>NotRecovered</c>, tagg 6). Rekkefølgen er backfillens:
    /// tagg 6 slår tagg 5.
    ///
    /// <c>HasMediaFiles</c> og <c>BehaviorId</c> settes også av staging-raden —
    /// <see cref="ImportStagingSchema"/> har dem — men de er regnet ut fra den samme
    /// minnestrukturen. Her kommer de fra radene som faktisk ble skrevet, og det er den
    /// kilden de avledede tabellene må stemme med.
    /// </summary>
    private static string ObservationColumns => """
        UPDATE o
        SET o.HasMediaFiles = CASE WHEN EXISTS (
                SELECT 1 FROM dbo.MediaFile mf WHERE mf.Observation_Id = o.Id) THEN 1 ELSE 0 END,
            o.RegistrationStatusId = CASE
                WHEN EXISTS (SELECT 1 FROM dbo.ObservationTags t
                             WHERE t.ObservationId = o.Id AND t.TagId = 6) THEN 3
                WHEN EXISTS (SELECT 1 FROM dbo.ObservationTags t
                             WHERE t.ObservationId = o.Id AND t.TagId = 5) THEN 2
                ELSE 1 END,
            o.BehaviorId = (SELECT CAST(MIN(b.BehaviorId) AS TINYINT)
                            FROM dbo.ObservationBehaviors b WHERE b.ObservationId = o.Id)
        FROM dbo.Observation o
        INNER JOIN #stg_Observation s ON s.ProxyId = o.ProxyId;
        """;

    /// <summary>
    /// Én rad per observasjon, én kolonne per taksonrang, utledet av
    /// <c>Taxon.TaxonIdHiarchy</c> — en kommaseparert liste over taksonets forfedre.
    ///
    /// RANGENE 1–26 LIGGER I KOLONNEREKKEFØLGE: <c>TaxonRank.Id</c> 1 er kingdom og 26
    /// er notSet, og kolonnene står i samme orden. <c>MAX</c> fordi grupperingen er per
    /// observasjon og bare én forfader kan ha hver rang.
    ///
    /// SLETTES FØRST, fordi en reimport kan ha fått nytt takson — og da er hele
    /// slektslinjen en annen.
    /// </summary>
    private static string TaxonHierarchy =>
        $"""
         DELETE h
         FROM dbo.ObservationTaxonHierarchy h
         INNER JOIN dbo.Observation o ON o.Id = h.ObservationId
         INNER JOIN #stg_Observation s ON s.ProxyId = o.ProxyId;

         INSERT INTO dbo.ObservationTaxonHierarchy (ObservationId, {RankColumns})
         SELECT o.Id, {RankExpressions}
         FROM dbo.Observation o
         INNER JOIN #stg_Observation s ON s.ProxyId = o.ProxyId
         INNER JOIN dbo.Taxon t ON t.Id = o.TaxonId
         CROSS APPLY STRING_SPLIT(t.TaxonIdHiarchy, ',') AS deler
         INNER JOIN dbo.Taxon forfader ON forfader.Id = TRY_CAST(deler.value AS INT)
         WHERE o.IsDeleted = 0
         GROUP BY o.Id;
         """;

    /// <summary>
    /// Indeksen filtrene faktisk leser: én rad per observasjon og område, med alle
    /// filterkolonnene denormalisert inn.
    ///
    /// OMRÅDENE KOMMER FRA <c>Location</c> → <c>LocationAreas</c> → <c>Area</c>, og
    /// bare de gjeldende (<c>IsCurrent = 1</c>). <c>EntityTypeId</c> er områdetypen
    /// direkte; <c>EntityId</c> er <c>Area.Fid</c> som tall, og den konverteringen har
    /// to særtilfeller: verneområder (type 3) har prefikset «Naturbase VV», og
    /// historiske fylkes-Fid-er har understrek («15_2017»).
    ///
    /// INSTITUSJONSRADENE (<c>EntityTypeId = 101</c>) SETTES BEVISST IKKE INN. De
    /// finnes i eksisterende miljøer fra en gammel migrasjon, men er erstattet av
    /// kolonnen <c>InstitutionOrgId</c>, og backfillen sletter alle 61 052 216 rett
    /// etterpå. Ingen spørring leser dem lenger.
    ///
    /// EN OBSERVASJON UTEN LOKALITET FÅR INGEN RADER. Den er heller ikke søkbar på
    /// område, og det er riktig: den vet ikke hvor den er.
    /// </summary>
    private static string EntityIndex => """
        DELETE i
        FROM dbo.ObservationEntityIndex i
        INNER JOIN dbo.Observation o ON o.Id = i.ObservationId
        INNER JOIN #stg_Observation s ON s.ProxyId = o.ProxyId;

        INSERT INTO dbo.ObservationEntityIndex (
            ObservationId, EntityTypeId, EntityId,
            BasisOfRecordId, CategoryId, CoordinatePrecisionInMeters, DateTimeCollected,
            MonthCollected, HasMediaFiles, RegistrationStatusId, TaxonGroupId,
            SpeciesTaxonId, GenusTaxonId, FamilyTaxonId, OrderTaxonId,
            BehaviorId, DatasetOrgId, InstitutionOrgId, LocationId
        )
        SELECT DISTINCT
            o.Id,
            a.AreaTypeId,
            CASE WHEN a.AreaTypeId = 3 THEN CAST(REPLACE(a.Fid, 'Naturbase VV', '') AS INT)
                 ELSE CAST(REPLACE(a.Fid, '_', '') AS INT) END,
            o.BasisOfRecordId,
            o.CategoryId,
            o.CoordinatePrecisionInMeters,
            o.DateTimeCollected,
            CAST(MONTH(o.DateTimeCollected) AS TINYINT),
            o.HasMediaFiles,
            o.RegistrationStatusId,
            o.TaxonGroupId,
            h.SpeciesTaxonId,
            h.GenusTaxonId,
            h.FamilyTaxonId,
            h.OrderTaxonId,
            o.BehaviorId,
            o.DatasetOrgId,
            o.InstitutionOrgId,
            o.LocationId
        FROM dbo.Observation o
        INNER JOIN #stg_Observation s ON s.ProxyId = o.ProxyId
        INNER JOIN dbo.Location l ON l.Id = o.LocationId
        INNER JOIN dbo.LocationAreas la ON la.LocationId = l.Id
        INNER JOIN dbo.Area a ON a.Id = la.AreaId
        LEFT JOIN dbo.ObservationTaxonHierarchy h ON h.ObservationId = o.Id
        WHERE a.IsCurrent = 1
          AND o.IsDeleted = 0;
        """;

    private static string RankColumns => string.Join(", ", Ranks);

    private static string RankExpressions =>
        string.Join(
            ",\n       ",
            Ranks.Select((_, i) => $"MAX(CASE WHEN forfader.TaxonRankId = {i + 1} THEN forfader.Id END)"));
}

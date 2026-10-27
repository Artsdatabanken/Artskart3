namespace Artskart3.Infrastructure.Maintenance;

/// <summary>
/// Setningene som fyller <c>ObservationCount</c>-kolonnene. Port av Artskart 2s
/// <c>dbo.updateObservationCounts</c> og de åtte prosedyrene den kaller.
///
/// KOLONNENE LESES ALT AV ARTSKART 3. <c>LookupRepository</c> henter dem, men
/// ingenting skriver dem — så tallene grensesnittet viser i dag er arvet fra dumpen og
/// står stille (åpent spørsmål 7.2 punkt 7).
///
/// TOMME RADER FÅR <c>NULL</c>, IKKE 0. Hver prosedyre nullstiller hele kolonnen før
/// den fyller den, og en rad uten observasjoner får derfor ingen verdi. Forskjellen er
/// synlig: <c>NULL</c> betyr «ingen observasjoner», og 0 ville betydd det samme — men
/// det er <c>NULL</c> grensesnittet er skrevet for.
///
/// DETTE ER ETTERARBEID, IKKE DEL AV IMPORTEN. Hver setning skanner hele
/// <c>Observation</c>, så de hører i en egen jobb etter importen (plan 4.5 punkt 12).
/// </summary>
public static class ObservationCountSql
{
    /// <summary>
    /// Den laveste taksonrangen den kumulative løkken begynner på.
    ///
    /// ARTSKART 2 BRUKER 24, OG DET ER EN FEIL. Rangene går til 26 (<c>form</c> er 25,
    /// <c>notSet</c> er 26), så taksoner på rang 25 og 26 får aldri satt sine kumulative
    /// tall — og bidrar dermed heller ikke til forfedrenes. Målt 14. oktober 2026:
    /// 807 observasjoner ligger på et rang-25-takson og er usynlige i de kumulative
    /// tallene oppover. Ingen ligger på rang 26.
    ///
    /// Porten beholder 24, fordi en retting endrer tall som er publisert. Å sette den
    /// til 26 er en linje — men det er en beslutning om data, ikke om kode.
    /// </summary>
    public const int CumulativeStartRank = 24;

    /// <summary>
    /// Målene i den rekkefølgen Artskart 2 oppdaterer dem. Rekkefølgen betyr ingenting
    /// for resultatet — ingen av dem leser en annens kolonne — men den gjør loggen
    /// sammenlignbar med Artskart 2s.
    /// </summary>
    public static IReadOnlyList<CountTarget> Targets { get; } =
    [
        new("Area", Area),
        new("BasisOfRecord", BasisOfRecord),
        new("Category", Category),
        new("Organization", Organization),
        new("Behavior", Behavior),
        new("Tag", Tag),
        new("TaxonGroup", TaxonGroup),
        new("Taxon", Taxon)
    ];

    /// <summary>
    /// Områder: én observasjon teller i hvert område lokaliteten ligger i.
    ///
    /// JOINEN GÅR DIREKTE PÅ <c>LocationId</c>, slik Artskart 2 gjør det — ikke via
    /// <c>Location</c>. Resultatet er det samme, og dette er én join mindre over 61 M
    /// rader.
    ///
    /// TELLER OGSÅ IKKE-GJELDENDE OMRÅDER. <c>ObservationEntityIndex</c> filtrerer på
    /// <c>IsCurrent = 1</c>, men denne gjør ikke det — et historisk fylke får altså et
    /// tall her, men er ikke søkbart. Dagens oppførsel.
    /// </summary>
    private static string Area => """
        UPDATE dbo.Area SET ObservationCount = NULL;

        UPDATE a SET a.ObservationCount = t.Antall
        FROM dbo.Area a
        INNER JOIN (
            SELECT la.AreaId, COUNT_BIG(1) AS Antall
            FROM dbo.Observation o
            INNER JOIN dbo.LocationAreas la ON la.LocationId = o.LocationId
            GROUP BY la.AreaId
        ) t ON t.AreaId = a.Id;
        """;

    private static string BasisOfRecord => """
        UPDATE dbo.BasisOfRecord SET ObservationCount = NULL;

        UPDATE b SET b.ObservationCount = t.Antall
        FROM dbo.BasisOfRecord b
        INNER JOIN (
            SELECT BasisOfRecordId, COUNT_BIG(1) AS Antall
            FROM dbo.Observation GROUP BY BasisOfRecordId
        ) t ON t.BasisOfRecordId = b.Id;
        """;

    private static string Category => """
        UPDATE dbo.Category SET ObservationCount = NULL;

        UPDATE c SET c.ObservationCount = t.Antall
        FROM dbo.Category c
        INNER JOIN (
            SELECT CategoryId, COUNT_BIG(1) AS Antall
            FROM dbo.Observation WHERE CategoryId IS NOT NULL GROUP BY CategoryId
        ) t ON t.CategoryId = c.Id;
        """;

    /// <summary>
    /// Organisasjoner. HER MÅTTE PORTEN VELGE: Artskart 2 teller via
    /// <c>OrganizationRelation</c>, én rad per observasjon og organisasjon. Tabellen
    /// finnes ikke i Artskart 3 — den er erstattet av <c>Observation.InstitutionOrgId</c>,
    /// <c>Observation.DatasetOrgId</c> og <c>ObservationProject</c>.
    ///
    /// Summen er de tre kildene slått sammen, og <c>DISTINCT</c> sørger for at en
    /// organisasjon som er både utgiver og datasett for samme post teller én gang. Det
    /// er samme semantikk som relasjonstabellen hadde.
    /// </summary>
    private static string Organization => """
        UPDATE dbo.Organization SET ObservationCount = NULL;

        UPDATE org SET org.ObservationCount = t.Antall
        FROM dbo.Organization org
        INNER JOIN (
            SELECT OrgId, COUNT_BIG(1) AS Antall
            FROM (
                SELECT DISTINCT o.Id AS ObservationId, o.InstitutionOrgId AS OrgId
                FROM dbo.Observation o WHERE o.InstitutionOrgId IS NOT NULL
                UNION
                SELECT DISTINCT o.Id, o.DatasetOrgId
                FROM dbo.Observation o WHERE o.DatasetOrgId IS NOT NULL
                UNION
                SELECT DISTINCT p.ObservationId, p.ProjectOrgId
                FROM dbo.ObservationProject p
            ) kilder
            GROUP BY OrgId
        ) t ON t.OrgId = org.Id;
        """;

    private static string Behavior => """
        UPDATE dbo.Behavior SET ObservationCount = NULL;

        UPDATE b SET b.ObservationCount = t.Antall
        FROM dbo.Behavior b
        INNER JOIN (
            SELECT ob.BehaviorId, COUNT_BIG(1) AS Antall
            FROM dbo.ObservationBehaviors ob GROUP BY ob.BehaviorId
        ) t ON t.BehaviorId = b.Id;
        """;

    private static string Tag => """
        UPDATE dbo.Tag SET ObservationCount = NULL;

        UPDATE g SET g.ObservationCount = t.Antall
        FROM dbo.Tag g
        INNER JOIN (
            SELECT ot.TagId, COUNT_BIG(1) AS Antall
            FROM dbo.ObservationTags ot GROUP BY ot.TagId
        ) t ON t.TagId = g.Id;
        """;

    private static string TaxonGroup => """
        UPDATE dbo.TaxonGroup SET ObservationCount = NULL;

        UPDATE tg SET tg.ObservationCount = t.Antall
        FROM dbo.TaxonGroup tg
        INNER JOIN (
            SELECT TaxonGroupId, COUNT_BIG(1) AS Antall
            FROM dbo.Observation WHERE TaxonGroupId IS NOT NULL GROUP BY TaxonGroupId
        ) t ON t.TaxonGroupId = tg.Id;
        """;

    /// <summary>
    /// Taksoner: seks kolonner. De tre første er rene tellinger, de tre siste er
    /// kumulative og summeres nedenfra og opp gjennom <c>ParentTaxonId</c>.
    ///
    /// UKE OG DØGN REGNES FRA <c>DateTimeCollected</c>, ikke fra importtidspunktet. Et
    /// datasett med gamle funn gir derfor 0 uansett hvor nylig det ble importert — det
    /// er «samlet inn siste uke», ikke «ny i basen».
    ///
    /// TIDEN ER UTC HER, <c>GETDATE()</c> I ARTSKART 2. På et sju dagers vindu er
    /// forskjellen en time eller to i kanten, og UTC er det Artskart 3 bruker ellers.
    ///
    /// LØKKEN GÅR OPPOVER FRA <see cref="CumulativeStartRank"/>: et takson får sitt eget
    /// tall pluss summen av barnas kumulative tall, så barna må være ferdige først.
    /// </summary>
    private static string Taxon =>
        $"""
         UPDATE dbo.Taxon SET
             ObservationCount = NULL,
             WeeklyObservationCount = NULL,
             DailyObservationCount = NULL,
             CumulativeObservationCount = NULL,
             WeeklyCumulativeObservationCount = NULL,
             DailyCumulativeObservationCount = NULL;

         UPDATE t SET t.ObservationCount = k.Antall
         FROM dbo.Taxon t
         INNER JOIN (
             SELECT TaxonId, COUNT_BIG(1) AS Antall
             FROM dbo.Observation WHERE TaxonId IS NOT NULL GROUP BY TaxonId
         ) k ON k.TaxonId = t.Id;

         UPDATE t SET t.WeeklyObservationCount = k.Antall
         FROM dbo.Taxon t
         INNER JOIN (
             SELECT TaxonId, COUNT_BIG(1) AS Antall
             FROM dbo.Observation
             WHERE TaxonId IS NOT NULL
               AND DateTimeCollected >= DATEADD(DAY, -7, SYSUTCDATETIME())
             GROUP BY TaxonId
         ) k ON k.TaxonId = t.Id;

         UPDATE t SET t.DailyObservationCount = k.Antall
         FROM dbo.Taxon t
         INNER JOIN (
             SELECT TaxonId, COUNT_BIG(1) AS Antall
             FROM dbo.Observation
             WHERE TaxonId IS NOT NULL
               AND DateTimeCollected >= DATEADD(DAY, -1, SYSUTCDATETIME())
             GROUP BY TaxonId
         ) k ON k.TaxonId = t.Id;

         DECLARE @rang INT = {CumulativeStartRank};

         WHILE @rang >= 1
         BEGIN
             UPDATE t SET
                 t.CumulativeObservationCount = ISNULL(t.ObservationCount, 0)
                     + ISNULL((SELECT SUM(b.CumulativeObservationCount) FROM dbo.Taxon b
                               WHERE b.ParentTaxonId = t.Id AND b.CumulativeObservationCount IS NOT NULL), 0),
                 t.WeeklyCumulativeObservationCount = ISNULL(t.WeeklyObservationCount, 0)
                     + ISNULL((SELECT SUM(b.WeeklyCumulativeObservationCount) FROM dbo.Taxon b
                               WHERE b.ParentTaxonId = t.Id AND b.WeeklyCumulativeObservationCount IS NOT NULL), 0),
                 t.DailyCumulativeObservationCount = ISNULL(t.DailyObservationCount, 0)
                     + ISNULL((SELECT SUM(b.DailyCumulativeObservationCount) FROM dbo.Taxon b
                               WHERE b.ParentTaxonId = t.Id AND b.DailyCumulativeObservationCount IS NOT NULL), 0)
             FROM dbo.Taxon t
             WHERE t.TaxonRankId = @rang;

             SET @rang = @rang - 1;
         END;
         """;
}

/// <summary>
/// Én tabell som skal få oppdaterte tellinger.
/// </summary>
/// <param name="Table">Tabellnavnet, som også er navnet i loggen.</param>
/// <param name="Sql">Setningene som fyller den.</param>
public sealed record CountTarget(string Table, string Sql);

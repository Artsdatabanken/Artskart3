namespace Artskart3.Infrastructure.Import.Writing;

/// <summary>
/// Setningene som flytter en staging-runde inn i de ekte tabellene. Alle er
/// sett-baserte: én setning per tabell, ikke én per post.
///
/// KOLONNELISTENE GENERERES FRA <see cref="ImportStagingSchema"/>, mens formen på hver
/// setning står skrevet ut. Da kan ikke listene drive fra kolonnekartet, og
/// <c>MERGE</c>-ene er fortsatt mulige å lese.
///
/// REKKEFØLGEN ER LASTBÆRENDE: lokaliteter før observasjoner, fordi observasjonen
/// peker på lokaliteten; observasjoner før barna, fordi barna slås opp på
/// <c>Observation.Id</c>.
///
/// BARNERADENE SLÅS OPP PÅ <c>ProxyId</c>. Det er mulig fordi nøkkelen er unik — målt
/// over alle 61 052 216 observasjoner — og det sparer en <c>MERGE ... OUTPUT</c> med
/// etterfølgende kobling på nye Id-er.
///
/// TRE TABELLER BEHANDLES ANNERLEDES ENN PLANENS «DELETE + INSERT»:
///
/// <list type="bullet">
/// <item><c>Location</c> og <c>Observation</c> er <c>MERGE</c>, fordi radene har
/// identiteter andre rader peker på.</item>
/// <item><c>MediaFile</c> DIFFES, fordi raden bærer tilstand importen ikke eier:
/// <c>Image</c> (den nedlastede filen), <c>Downloaded</c>, <c>InDownloadqueue</c> og
/// <c>DownloadRetryCount</c>. Slettet og satt inn på nytt ville hver reimport kastet
/// alle nedlastede bilder og lagt dem i kø igjen. Artskart 2 differ dem på
/// <c>Origin</c>, og det gjør denne også.</item>
/// </list>
/// </summary>
public static class PublishSql
{
    /// <summary>Tidspunktet runden regnes som skrevet på.</summary>
    public const string NowParameter = "@now";

    /// <summary>SRID-en geometriene bygges med.</summary>
    public const string SridParameter = "@srid";

    /// <summary>
    /// Kolonner i kartet som ikke er kolonner i måltabellen — publiseringen gjør dem om
    /// til noe annet.
    /// </summary>
    private const string ProxyId = "ProxyId";

    private const string LinkedProxyId = "LinkedProxyId";

    private const string LookupId = "LookupId";

    private const string GeometryWkb = "GeometryWkb";

    /// <summary>
    /// Setningene i den rekkefølgen de skal kjøres, med et navn til logg og feilmelding.
    /// </summary>
    public static IReadOnlyList<PublishStep> Steps { get; } = Bygg();

    /// <summary>Hele publiseringen som én skriptstreng. Til logging og tester.</summary>
    public static string FullScript => string.Join("\n\n", Steps.Select(x => x.Sql));

    private static List<PublishStep> Bygg() =>
    [
        new("Kontroll", Kontroll),
        new("Location", Location),
        new("LocationAreas", LocationAreas),
        new("Observation", Observation),
        new("ObservationDetails", ObservationDetails),
        new("ObservationTags", Koblingstabell("ObservationTags", "TagId")),
        new("ObservationTaxons", Koblingstabell("ObservationTaxons", "TaxonId")),
        new("ObservationBehaviors", Koblingstabell("ObservationBehaviors", "BehaviorId")),
        new("ObservationProject", Koblingstabell("ObservationProject", "ProjectOrgId")),
        new("MediaFile", MediaFile),
        new("ObservationError", ObservationError),
        new("ObservationLink", ObservationLink),
        new("SensitiveObservationData", SensitiveObservationData)
    ];

    /// <summary>
    /// To ting som aldri skal kunne skje, og som ellers ville ført til stille tap:
    /// en observasjon uten identitet blir ikke koblet til noe, og to med samme identitet
    /// får <c>MERGE</c> til å feile med en melding ingen forstår (feil 8672).
    /// </summary>
    private static string Kontroll => """
        IF EXISTS (SELECT 1 FROM #stg_Observation WHERE ProxyId IS NULL OR ProxyId = '')
        BEGIN
            THROW 50001, 'Staging inneholder observasjoner uten ProxyId.', 1;
        END;

        IF EXISTS (SELECT ProxyId FROM #stg_Observation GROUP BY ProxyId HAVING COUNT(*) > 1)
        BEGIN
            THROW 50002, 'Staging inneholder samme ProxyId mer enn en gang.', 1;
        END;

        IF EXISTS (SELECT LookupId FROM #stg_Location GROUP BY LookupId HAVING COUNT(*) > 1)
        BEGIN
            THROW 50003, 'Staging inneholder samme LookupId mer enn en gang.', 1;
        END;
        """;

    /// <summary>
    /// Lokalitetene. <c>MERGE</c> på <c>LookupId</c>, som har <c>IX_LookupId</c>.
    ///
    /// GEOMETRIEN BYGGES HER, av WKB-en staging bærer, med SRID som parameter.
    ///
    /// MAKEVALID REPRODUSERER ARTSKART 2. Et omriss fra kilden kan være topologisk
    /// ugyldig — selvkryssende ringer, gjentatte punkter — og Artskart 2 reparerer det
    /// før lagring. Paritetsriggen målte at `MakeValid()` på Artskart 3s ureparerte
    /// geometri er **byte-identisk** med det Artskart 2 har lagret, i alle 39 tilfellene
    /// på gullsett medium.
    ///
    /// Den kalles ubetinget, og det er trygt: på de 1 918 geometriene som alt var
    /// gyldige er `MakeValid()` byte-identisk no-op — målt, ikke antatt. Et betinget
    /// kall ville krevd `STIsValid()` først, og det er to kall i stedet for ett.
    ///
    /// UTEN DEN BLIR KOLONNEN `GeometryTypeId` ULESBAR. Den er beregnet og kaller
    /// `STGeometryType()`, som kaster på en ugyldig instans — og feilen feller hele
    /// spørringen, ikke bare raden. Én ugyldig geometri gjør dermed `GeometryTypeId`
    /// uleselig for hele tabellen.
    ///
    /// <c>TimeStamp</c> er lokalitetens eget tidsstempel og settes hver gang raden
    /// røres, som i Artskart 2.
    /// </summary>
    private static string Location =>
        $"""
         MERGE dbo.Location WITH (HOLDLOCK) AS t
         USING (
             SELECT {Kolonner(ImportStagingSchema.Location, GeometryWkb)},
                    Geometry = CASE WHEN GeometryWkb IS NULL THEN NULL
                                    ELSE geometry::STGeomFromWKB(GeometryWkb, {SridParameter}).MakeValid() END
             FROM #stg_Location
         ) AS s ON t.LookupId = s.LookupId
         WHEN MATCHED THEN UPDATE SET
             {Tilordninger(ImportStagingSchema.Location, LookupId, GeometryWkb)},
             t.Geometry = s.Geometry,
             t.TimeStamp = {NowParameter},
             t.UpdatedAt = {NowParameter},
             t.IsDeleted = 0,
             t.DeletedAt = NULL
         WHEN NOT MATCHED THEN INSERT (
             {Kolonner(ImportStagingSchema.Location, GeometryWkb)}, Geometry, TimeStamp, CreatedAt, UpdatedAt, IsDeleted
         ) VALUES (
             {Prefikset(ImportStagingSchema.Location, "s", GeometryWkb)}, s.Geometry,
             {NowParameter}, {NowParameter}, {NowParameter}, 0
         );
         """;

    /// <summary>
    /// Områdene lokaliteten ligger i. Rene koblingsrader uten egen tilstand, så de
    /// settes opp på nytt for lokalitetene i runden.
    /// </summary>
    private static string LocationAreas => """
        DELETE la
        FROM dbo.LocationAreas la
        INNER JOIN dbo.Location l ON l.Id = la.LocationId
        INNER JOIN #stg_Location s ON s.LookupId = l.LookupId;

        INSERT INTO dbo.LocationAreas (LocationId, AreaId)
        SELECT DISTINCT l.Id, s.AreaId
        FROM #stg_LocationAreas s
        INNER JOIN dbo.Location l ON l.LookupId = s.LookupId;
        """;

    /// <summary>
    /// Observasjonene. <c>MERGE</c> på <c>ProxyId</c>.
    ///
    /// LOKALITETEN LØSES OPP HER: traff bufferet, bærer staging <c>LocationId</c>
    /// direkte; ellers slås den opp på <c>LookupId</c> i raden <c>Location</c>-steget
    /// nettopp skrev.
    ///
    /// EN POST SOM KOMMER TILBAKE BLIR LEVENDE IGJEN. <c>IsDeleted</c> og
    /// <c>DeletedAt</c> nullstilles på treff: står posten i cachen nå, er den ikke
    /// slettet.
    /// </summary>
    private static string Observation =>
        $"""
         MERGE dbo.Observation WITH (HOLDLOCK) AS t
         USING (
             SELECT {Prefikset(ImportStagingSchema.Observation, "stg", LookupId, "LocationId")},
                    LocationId = COALESCE(stg.LocationId, l.Id)
             FROM #stg_Observation stg
             LEFT JOIN dbo.Location l ON l.LookupId = stg.LookupId
         ) AS s ON t.ProxyId = s.ProxyId
         WHEN MATCHED THEN UPDATE SET
             {Tilordninger(ImportStagingSchema.Observation, ProxyId, LookupId)},
             t.UpdatedAt = {NowParameter},
             t.IsDeleted = 0,
             t.DeletedAt = NULL
         WHEN NOT MATCHED THEN INSERT (
             {Kolonner(ImportStagingSchema.Observation, LookupId)}, CreatedAt, UpdatedAt, IsDeleted
         ) VALUES (
             {Prefikset(ImportStagingSchema.Observation, "s", LookupId)},
             {NowParameter}, {NowParameter}, 0
         );
         """;

    /// <summary>
    /// Detaljradene. Samme Id som observasjonen — tabellene er delt, ikke relatert — så
    /// <c>MERGE</c> går på <c>Id</c>.
    /// </summary>
    private static string ObservationDetails =>
        $"""
         MERGE dbo.ObservationDetails WITH (HOLDLOCK) AS t
         USING (
             SELECT o.Id, {Prefikset(ImportStagingSchema.ObservationDetails, "stg", ProxyId)}
             FROM #stg_ObservationDetails stg
             INNER JOIN dbo.Observation o ON o.ProxyId = stg.ProxyId
         ) AS s ON t.Id = s.Id
         WHEN MATCHED THEN UPDATE SET
             {Tilordninger(ImportStagingSchema.ObservationDetails, ProxyId)},
             t.UpdatedAt = {NowParameter},
             t.IsDeleted = 0,
             t.DeletedAt = NULL
         WHEN NOT MATCHED THEN INSERT (
             Id, {Kolonner(ImportStagingSchema.ObservationDetails, ProxyId)}, CreatedAt, UpdatedAt, IsDeleted
         ) VALUES (
             s.Id, {Prefikset(ImportStagingSchema.ObservationDetails, "s", ProxyId)},
             {NowParameter}, {NowParameter}, 0
         );
         """;

    /// <summary>
    /// En ren koblingstabell: <c>(ObservationId, verdi)</c> uten egen tilstand. Da er
    /// sletting og ny innsetting billigere enn en diff, og utfallet er det samme.
    ///
    /// SLETTINGEN ER PÅ ALLE OBSERVASJONENE I RUNDEN, ikke bare på de som har rader i
    /// staging — en post som har mistet den siste taggen sin skal også miste raden.
    /// </summary>
    private static string Koblingstabell(string tabell, string verdikolonne) =>
        $"""
         DELETE c
         FROM dbo.{tabell} c
         INNER JOIN dbo.Observation o ON o.Id = c.ObservationId
         INNER JOIN #stg_Observation s ON s.ProxyId = o.ProxyId;

         INSERT INTO dbo.{tabell} (ObservationId, {verdikolonne})
         SELECT DISTINCT o.Id, s.{verdikolonne}
         FROM #stg_{tabell} s
         INNER JOIN dbo.Observation o ON o.ProxyId = s.ProxyId;
         """;

    /// <summary>
    /// Mediefilene, diffet på <c>Origin</c> i tre setninger.
    ///
    /// RADEN BÆRER TILSTAND IMPORTEN IKKE EIER: <c>Image</c> er den nedlastede filen, og
    /// <c>Downloaded</c>, <c>InDownloadqueue</c> og <c>DownloadRetryCount</c> er
    /// nedlastingskøens. Derfor oppdateres bare de tre feltene kilden faktisk eier, og
    /// bare rader som er borte fra kilden slettes. Artskart 2 gjør det samme i
    /// <c>Observation.UpdateFrom</c>.
    ///
    /// SAMMENLIGNINGEN AV <c>Origin</c> er uten hensyn til bokstavstørrelse og
    /// etterfølgende mellomrom, fordi SQL Servers standardkollasjon er det. Artskart 2
    /// kaller <c>ToLowerInvariant().Trim()</c> på begge sider for å få det samme.
    /// </summary>
    private static string MediaFile =>
        $"""
         UPDATE m SET
             m.License = s.License,
             m.RightsHolder = s.RightsHolder,
             m.Description = s.Description,
             m.MediaFileTypeId = s.MediaFileTypeId,
             m.UpdatedAt = {NowParameter},
             m.IsDeleted = 0,
             m.DeletedAt = NULL
         FROM dbo.MediaFile m
         INNER JOIN dbo.Observation o ON o.Id = m.Observation_Id
         INNER JOIN #stg_MediaFile s ON s.ProxyId = o.ProxyId AND s.Origin = m.Origin;

         DELETE m
         FROM dbo.MediaFile m
         INNER JOIN dbo.Observation o ON o.Id = m.Observation_Id
         INNER JOIN #stg_Observation b ON b.ProxyId = o.ProxyId
         WHERE NOT EXISTS (
             SELECT 1 FROM #stg_MediaFile s
             WHERE s.ProxyId = o.ProxyId AND s.Origin = m.Origin
         );

         INSERT INTO dbo.MediaFile (
             Observation_Id, Origin, MediaFileTypeId, License, RightsHolder, Description,
             Size, Downloaded, InDownloadqueue, DownloadRetryCount,
             CreatedAt, UpdatedAt, IsDeleted
         )
         SELECT o.Id, s.Origin, s.MediaFileTypeId, s.License, s.RightsHolder, s.Description,
                0, 0, 0, 0,
                {NowParameter}, {NowParameter}, 0
         FROM #stg_MediaFile s
         INNER JOIN dbo.Observation o ON o.ProxyId = s.ProxyId
         WHERE NOT EXISTS (
             SELECT 1 FROM dbo.MediaFile m
             WHERE m.Observation_Id = o.Id AND m.Origin = s.Origin
         );
         """;

    /// <summary>
    /// Feilradene. De beskriver denne kjøringen av denne posten, så de settes opp på
    /// nytt hver gang.
    /// </summary>
    private static string ObservationError =>
        $"""
         DELETE e
         FROM dbo.ObservationError e
         INNER JOIN dbo.Observation o ON o.Id = e.ObservationId
         INNER JOIN #stg_Observation s ON s.ProxyId = o.ProxyId;

         INSERT INTO dbo.ObservationError (
             ObservationId, ValidationCode, ErrorContext, AnnotationId,
             DateTimeModified, CreatedAt, UpdatedAt, IsDeleted
         )
         SELECT o.Id, s.ValidationCode, s.ErrorContext, s.AnnotationId,
                {NowParameter}, {NowParameter}, {NowParameter}, 0
         FROM #stg_ObservationError s
         INNER JOIN dbo.Observation o ON o.ProxyId = s.ProxyId;
         """;

    /// <summary>
    /// Koblinger til andre observasjoner.
    ///
    /// <c>LinkedObservationId</c> BLIR <c>NULL</c> NÅR DEN ANDRE POSTEN IKKE FINNES, og
    /// det er ikke en feil: den kan ligge i en senere bunt, eller i en node som ikke er
    /// importert. Referansen står da igjen i <c>Remarks</c>, som er den eneste veien
    /// tilbake til hva kilden mente.
    /// </summary>
    private static string ObservationLink =>
        $"""
         DELETE lnk
         FROM dbo.ObservationLink lnk
         INNER JOIN dbo.Observation o ON o.Id = lnk.ObservationId
         INNER JOIN #stg_Observation s ON s.ProxyId = o.ProxyId;

         INSERT INTO dbo.ObservationLink (
             ObservationId, LinkedObservationId, Remarks, CreatedAt, UpdatedAt, IsDeleted
         )
         SELECT o.Id, lo.Id, s.Remarks, {NowParameter}, {NowParameter}, 0
         FROM #stg_ObservationLink s
         INNER JOIN dbo.Observation o ON o.ProxyId = s.ProxyId
         LEFT JOIN dbo.Observation lo ON lo.ProxyId = s.LinkedProxyId;
         """;

    /// <summary>
    /// De umaskerte verdiene for sensitive observasjoner. Én rad per observasjon, og
    /// den utledes av posten hver gang — så sletting og ny innsetting er riktig.
    ///
    /// TABELLEN HAR INGEN REVISJONSKOLONNER, i motsetning til de andre.
    /// </summary>
    private static string SensitiveObservationData =>
        $"""
         DELETE sd
         FROM dbo.SensitiveObservationData sd
         INNER JOIN dbo.Observation o ON o.Id = sd.ObservationId
         INNER JOIN #stg_Observation s ON s.ProxyId = o.ProxyId;

         INSERT INTO dbo.SensitiveObservationData (
             ObservationId, {Kolonner(ImportStagingSchema.SensitiveObservationData, ProxyId, GeometryWkb)}, Geometry
         )
         SELECT o.Id, {Prefikset(ImportStagingSchema.SensitiveObservationData, "s", ProxyId, GeometryWkb)},
                CASE WHEN s.GeometryWkb IS NULL THEN NULL
                     ELSE geometry::STGeomFromWKB(s.GeometryWkb, {SridParameter}) END
         FROM #stg_SensitiveObservationData s
         INNER JOIN dbo.Observation o ON o.ProxyId = s.ProxyId;
         """;

    /// <summary>
    /// Lokalitets-Id-ene for runden, slik de kan føres inn i
    /// <c>LocationIdCache</c> og spare neste bunt for å skrive dem igjen.
    /// </summary>
    public const string SelectLocationIds = """
        SELECT l.LookupId, l.Id
        FROM dbo.Location l
        INNER JOIN #stg_Location s ON s.LookupId = l.LookupId;
        """;

    // --- Kolonnelister fra kartet ------------------------------------------

    private static string Kolonner(IStagingTable tabell, params string[] utenom) =>
        string.Join(", ", Navn(tabell, utenom).Select(x => $"[{x}]"));

    private static string Prefikset(IStagingTable tabell, string prefiks, params string[] utenom) =>
        string.Join(", ", Navn(tabell, utenom).Select(x => $"{prefiks}.[{x}]"));

    private static string Tilordninger(IStagingTable tabell, params string[] utenom) =>
        string.Join(",\n    ", Navn(tabell, utenom).Select(x => $"t.[{x}] = s.[{x}]"));

    private static IEnumerable<string> Navn(IStagingTable tabell, string[] utenom) =>
        tabell.Columns.Select(x => x.Name).Where(x => !utenom.Contains(x, StringComparer.Ordinal));
}

/// <summary>
/// Én setning i publiseringen, med et navn som havner i loggen og i feilmeldingen om
/// den feiler.
/// </summary>
public sealed record PublishStep(string Name, string Sql);

namespace Artskart3.Workers.Maintenance;

/// <summary>
/// Hangfire recurring job som kjører før harvest for å sikre at referansedata er oppdatert.
///
/// Ansvarsområder:
/// - Taksonomi-sync: Oppdaterer taksontabellene fra Artsdatabankens taksonomi-API
///   (erstatter TaxonomyWash som i dag kjører i artskart2)
/// - Områdeoppdateringer: Oppdaterer kommune-/fylkesgrenser og verneområder
/// - Vedlikeholdsoppgaver: Datarydding og andre planlagte vedlikeholdsjobber
/// - Columnstore-vedlikehold: REORGANIZE av IX_OEI_Columnstore etter ImportJob
/// - Klyngeindeksene: REBUILD når fragmenteringen passerer ~30 %
/// - Tellebufferne: bygg dem på nytt når oppslaget avviser dem (se nedenfor)
///
/// TODO: Implementeres i neste fase.
///
/// TELLEBUFFERNE MÅ BYGGES PÅ NYTT HERFRA — DE GJØR DET IKKE SELV
/// En buffer oppslaget avviser er ufarlig men permanent: hvert kall faller tilbake
/// til aggregering, svarer riktig, og er tregt helt til noen kjører
/// Scripts/BuildAreaCountCache.sql og deretter BuildLocationCountCache.sql for
/// hånd — rundt 16 minutter på prodlik datamengde. Ingenting rydder opp av seg
/// selv: BuildCountCaches er en migrasjon, og en anvendt migrasjon kjøres aldri
/// igjen.
///
/// To ting utløser en avvisning, og begge er normale hendelser:
///
///   SchemaVersion   dimensjonsregisteret er endret, så bøtte- og pargruppe-id-ene
///                   i bufferen betyr ikke lenger det koden tror
///   SourceRows      radantallet i ObservationEntityIndex har flyttet seg siden
///                   bufferen ble bygget — altså en import eller et manuelt skript
///
/// Jobben bør oppdage begge (tilstandsraden mot AreaCountCacheDimensions.SchemaVersion
/// og mot CountCacheSourceRows.Sql) og bygge på nytt. Da blir utfallet «tregt i
/// ~16 minutter», ikke «tregt til noen oppdager det».
///
/// TRE KRAV, OG DET FØRSTE AVGJØR OM DETTE ER MULIG I DET HELE TATT
///
///   1. RETTIGHETER. Skriptene TRUNCATE-r og kjører UPDATE STATISTICS, altså ALTER
///      på tabellene. Resten av løsningen er bygget for en least-privilege-pålogging
///      — CountCacheSourceRows velger sys.partitions nettopp fordi db_datareader
///      ikke har VIEW DATABASE STATE. Denne jobben trenger derfor en egen,
///      hevet tilkobling. Er den ikke på plass, feiler jobben på første kjøring i
///      hvert deployede miljø.
///   2. ÉN KJØRER. Skriptene tømmer og bygger; to samtidige kjøringer tømmer for
///      hverandre. Jobben må holde en lås (sp_getapplock eller tilsvarende).
///   3. IKKE PÅ LESESTIEN. Dette hører i en bakgrunnsjobb, ikke i
///      AreaCountCacheService: 32 samtidige søk ville startet 32 gjenoppbygginger,
///      og 16 minutter får ikke plass i en forespørsel.
///
/// MERK at en gjenoppbygging ikke er gratis mens den går: bufferen er borte OG
/// bygget konkurrerer om samme IO som spørringene. Skal det skje under trafikk,
/// trengs blue/green — se kommentaren i BuildAreaCountCache.sql.
///
/// Inntil jobben finnes: legges en dimensjon til, feiler
/// CountCacheScriptSchemaVersionTests, og den som gjør det vet da at bufferne må
/// bygges manuelt etter deploy.
///
/// Columnstore-vedlikehold (kjøres etter ImportJob, ikke før):
///
///     ALTER INDEX IX_OEI_Columnstore ON dbo.ObservationEntityIndex
///         REORGANIZE WITH (COMPRESS_ALL_ROW_GROUPS = ON);
///
/// REORGANIZE er online og inkrementell — den slår sammen små rowgroups og fjerner
/// slettede rader fysisk. COMPRESS_ALL_ROW_GROUPS tvinger også gjenværende
/// delta-rowgroups til å komprimeres, noe som er nødvendig når en daglig batch
/// var mindre enn 102 400 rader.
///
/// Bruk REORGANIZE, ikke REBUILD. En full rebuild av 192M rader tar 10-30 minutter
/// og hører ikke hjemme i en daglig jobb. Rebuild er kun nødvendig når andelen
/// slettede rader passerer ~20 %, som kan overvåkes med:
///
///     SELECT SUM(total_rows), SUM(deleted_rows)
///     FROM sys.dm_db_column_store_row_group_physical_stats
///     WHERE object_id = OBJECT_ID('dbo.ObservationEntityIndex');
///
/// KLYNGEINDEKSENE ER EN EGEN SAK — OG DE RÅTNER FORTERE
/// Avsnittet over gjelder columnstore. Rowstore-klyngeindeksene under de samme
/// tabellene trenger sitt eget vedlikehold, og de ble oversett lenge:
///
///     Indeks                        Fragmentering   Størrelse
///     PK_dbo.Observation                   99,2 %      16,0 GB
///     PK_ObservationEntityIndex            98,8 %      11,6 GB
///     Location SpatialIndex-Geometry       97,0 %       343 MB
///
/// Målt 29.09.2026 mot Artskart3IndexProdLikeTestMigrations. Etter REBUILD:
/// 0 % og henholdsvis 8,0 GB og 3,7 GB — databasen krympet 15,9 GB, og
/// tabellene hadde altså lest rundt tre ganger så mange sider som nødvendig.
/// Standard-nivået i ytelsessuiten gikk 538 → 504 s av dette alene, og en
/// Locations-«regresjon» på 23 % viste seg å være fragmentering og ikke kode.
///
/// HER ER REBUILD RIKTIG, I MOTSETNING TIL OVER
/// REORGANIZE er nesten virkningsløs på en b-tre-indeks over 90 %, og REBUILD
/// er rask nok: 418 s for 11,6 GB og 347 s for 16,0 GB med MAXDOP 4.
///
///     ALTER INDEX [PK_dbo.Observation] ON dbo.Observation REBUILD WITH (MAXDOP = 4);
///
/// HVA SOM FRAGMENTERER DEM
/// Store UPDATE-er som utvider rader, altså nøyaktig det backfill-migrasjonene
/// gjør. En persistert beregnet kolonne er verst: den skriver om hver eneste
/// rad. AddLocationGeometryTypeId etterlot Location 95,4 % fragmentert.
///
/// Derfor hører en REBUILD til etter hver backfill-migrasjon, ikke bare i den
/// daglige jobben — og AddLocationGeometryTypeId gjør nå nettopp det selv, som
/// siste setning i Up(). Den ENGANGS-fragmenteringen en migrasjon lager er
/// migrasjonens eget ansvar: den vet når den har skrevet om en tabell, og en
/// kommentar som peker hit ville pekt på en jobb som ikke finnes.
///
/// Det som gjenstår for denne jobben er den LØPENDE fragmenteringen. Når ImportJob
/// skriver daglig, bygger den seg opp igjen uten at noen migrasjon kjører, og da
/// må den overvåkes:
///
///     SELECT OBJECT_NAME(s.object_id), i.name,
///            s.avg_fragmentation_in_percent, s.page_count
///     FROM sys.dm_db_index_physical_stats(DB_ID(), NULL, NULL, NULL, 'LIMITED') s
///     JOIN sys.indexes i ON i.object_id = s.object_id AND i.index_id = s.index_id
///     WHERE s.page_count > 10000 AND s.avg_fragmentation_in_percent > 30;
///
/// 'LIMITED' er nok og leser bare øverste nivå — 'DETAILED' skanner hele
/// indeksen og er ikke noe man kjører mot 16 GB i en jobb.
/// </summary>
public class DatabaseMaintenanceJob
{
    private readonly ILogger<DatabaseMaintenanceJob> _logger;

    public DatabaseMaintenanceJob(ILogger<DatabaseMaintenanceJob> logger)
    {
        _logger = logger;
    }

    public Task ExecuteAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("DatabaseMaintenanceJob er ikke implementert ennå");
        return Task.CompletedTask;
    }
}

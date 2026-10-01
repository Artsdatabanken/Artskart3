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
///
/// TODO: Implementeres i neste fase.
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
/// Derfor bør en REBUILD høre til etter hver backfill-migrasjon, ikke bare i
/// den daglige jobben. Og siden ImportJob kommer til å skrive daglig, bør
/// fragmenteringen overvåkes:
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

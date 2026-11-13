using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Artskart3.Infrastructure.Migrations;

/// <summary>
/// Sidekomprimerer (DATA_COMPRESSION = PAGE) alle ukomprimerte rowstore-indekser.
///
/// Migrasjonene bygger på eksisterende databaser (InitialBaseline er tom) med
/// ukomprimerte indekser, og EF oppretter nye indekser uten komprimering. Bare
/// indeksene som en migrasjon har bygget med eksplisitt PAGE, er komprimert, så
/// miljøene sto med det meste ukomprimert. Målt i testmiljøet 05.11.2026 mot en
/// sidekomprimert lokal kopi med samme radantall: PK_dbo.Observation 49,9 mot 8,0 GB,
/// PK_ObservationEntityIndex 38,2 mot 3,9 GB, hele databasen 283 mot 114 GB. Søkene
/// ventet mest på sidelesing.
///
/// TRYGG Å KJØRE PÅ NYTT
/// Bare indekser som fortsatt står som NONE i sys.partitions bygges om, så der
/// komprimeringen alt er gjort for hånd, gjør den ingenting. En ombygging som er
/// pauset for hånd, gjenopptas i stedet for å startes på nytt. Feiler én indeks,
/// fortsetter den med resten og kaster til slutt, slik at migrasjonen ikke
/// registreres og neste deploy tar det som gjenstår.
///
/// ONLINE, MED LAV PRIORITET
/// ONLINE = ON tar likevel en kort Sch-M-lås ved byttet på slutten, og alt som kommer
/// etter, køer bak den. WAIT_AT_LOW_PRIORITY venter i lav prioritet, uten å stenge
/// for andre, og ABORT_AFTER_WAIT = SELF gjør at ombyggingen gir opp etter fem
/// minutter framfor å blokkere brukerspørringene. Utgaver uten online-ombygging
/// bygger offline; det gjelder bare utviklingsdatabaser.
///
/// IKKE RESUMABLE, PÅ GRUNN AV STATISTIKKEN
/// En vanlig ombygging gir indeksens statistikk full skanning. En RESUMABLE-ombygging
/// gjør det ikke: målt 11.11.2026 fikk IX_Observation_HasMediaFiles 0,6 % og
/// PK_HistoricalAreaLocation 3,2 % utvalg, mens den ikke-gjenopptakbare
/// IX_Location_GeometryTypeEastNorth fikk 100 %. Utvalgsstatistikk på
/// ObservationEntityIndex har før gjort LocationCount sju ganger tregere. Prisen er at
/// en avbrutt deploy starter den påbegynte indeksen på nytt, så deployjobbens
/// tidsgrense må romme den største indeksen. En for hånd pauset ombygging gjenopptas,
/// og får deretter statistikken oppdatert med full skanning.
///
/// MINSTE FØRST, slik at en feil i oppsettet viser seg på en liten indeks.
///
/// IKKE MED: MediaFile og ProcessSourceDataResult krymper nesten ikke (29,2 → 27,7 GB
/// og 1,8 → 1,7 GB). Klyngeindekser på tabeller med text, ntext eller image kan ikke
/// bygges online, og ville stoppet hver deploy. Heaps, columnstore og romlige indekser
/// er heller ikke med.
///
/// VARIGHET: rundt 216 GB å lese i testmiljøet, timer på Azure der loggraten er taket.
/// CommandTimeout er 6 timer.
///
/// TELLEBUFFERNE påvirkes ikke underveis: kopien som bygges, har en skjult index_id i
/// sys.partitions, så radantallet CountCacheSourceRows leser (index_id 0 og 1) står
/// stille, også mens en ombygging er pauset. Prøvd på SQL Server 2022.
/// </summary>
public partial class PageCompressIndexes : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            SET NOCOUNT ON;

            DECLARE @online bit = CASE WHEN CAST(SERVERPROPERTY('EngineEdition') AS int) IN (3, 5, 8) THEN 1 ELSE 0 END;

            DECLARE @maal TABLE (object_id int, index_id int, rader bigint, PRIMARY KEY (object_id, index_id));
            INSERT @maal (object_id, index_id, rader)
            SELECT i.object_id, i.index_id, s.rader
            FROM sys.indexes i
            CROSS APPLY (SELECT SUM(p.rows) AS rader
                         FROM sys.partitions p
                         WHERE p.object_id = i.object_id AND p.index_id = i.index_id) s
            WHERE OBJECTPROPERTY(i.object_id, 'IsUserTable') = 1
              AND OBJECTPROPERTY(i.object_id, 'IsMSShipped') = 0
              AND i.type IN (1, 2)
              -- En ombygging ville aktivert en deaktivert indeks igjen.
              AND i.is_disabled = 0
              AND i.is_hypothetical = 0
              AND i.object_id NOT IN (ISNULL(OBJECT_ID(N'dbo.MediaFile'), 0),
                                      ISNULL(OBJECT_ID(N'dbo.ProcessSourceDataResult'), 0),
                                      ISNULL(OBJECT_ID(N'dbo.__EFMigrationsHistory'), 0))
              AND NOT (i.type = 1 AND EXISTS (SELECT 1 FROM sys.columns c
                                              WHERE c.object_id = i.object_id
                                                AND c.system_type_id IN (34, 35, 99)))
              AND EXISTS (SELECT 1 FROM sys.partitions p
                          WHERE p.object_id = i.object_id AND p.index_id = i.index_id
                            AND p.data_compression = 0);

            DECLARE @schema sysname, @tabell sysname, @indeks sysname, @pauset bit,
                    @navn nvarchar(500), @sql nvarchar(max), @melding nvarchar(max), @forsteFeil nvarchar(2048);

            DECLARE indekser CURSOR LOCAL STATIC READ_ONLY FOR
            SELECT OBJECT_SCHEMA_NAME(m.object_id), OBJECT_NAME(m.object_id), i.name,
                   CASE WHEN EXISTS (SELECT 1 FROM sys.index_resumable_operations r
                                     WHERE r.object_id = m.object_id AND r.index_id = m.index_id
                                       AND r.state_desc = N'PAUSED')
                        THEN 1 ELSE 0 END
            FROM @maal m
            JOIN sys.indexes i ON i.object_id = m.object_id AND i.index_id = m.index_id
            ORDER BY m.rader;

            OPEN indekser;
            FETCH NEXT FROM indekser INTO @schema, @tabell, @indeks, @pauset;
            WHILE @@FETCH_STATUS = 0
            BEGIN
                SET @navn = QUOTENAME(@schema) + N'.' + QUOTENAME(@tabell);
                SET @sql = N'ALTER INDEX ' + QUOTENAME(@indeks) + N' ON ' + @navn
                    + CASE
                        -- En gjenopptatt ombygging gir utvalgsstatistikk, så den oppdateres etterpå.
                        WHEN @pauset = 1 THEN
                            N' RESUME; UPDATE STATISTICS ' + @navn + N' (' + QUOTENAME(@indeks) + N') WITH FULLSCAN;'
                        WHEN @online = 1 THEN
                            N' REBUILD WITH (ONLINE = ON (WAIT_AT_LOW_PRIORITY (MAX_DURATION = 5 MINUTES, ABORT_AFTER_WAIT = SELF))'
                            + N', MAXDOP = 4, DATA_COMPRESSION = PAGE);'
                        ELSE N' REBUILD WITH (MAXDOP = 4, DATA_COMPRESSION = PAGE);'
                      END;

                SET @melding = CONVERT(nvarchar(19), SYSDATETIME(), 120) + N'  ' + @sql;
                RAISERROR('%s', 0, 1, @melding) WITH NOWAIT;

                BEGIN TRY
                    EXEC sys.sp_executesql @sql;
                END TRY
                BEGIN CATCH
                    SET @melding = N'  FEILET: ' + ERROR_MESSAGE();
                    RAISERROR('%s', 0, 1, @melding) WITH NOWAIT;
                    IF @forsteFeil IS NULL SET @forsteFeil = LEFT(@indeks + N': ' + ERROR_MESSAGE(), 1000);
                END CATCH;

                FETCH NEXT FROM indekser INTO @schema, @tabell, @indeks, @pauset;
            END;
            CLOSE indekser;
            DEALLOCATE indekser;

            -- Sjekker resultatet, ikke bare feilene: en gjenopptatt ombygging kan ha vært
            -- startet uten komprimering.
            DECLARE @gjenstaar nvarchar(max) = (
                SELECT STRING_AGG(CONVERT(nvarchar(max), QUOTENAME(OBJECT_NAME(m.object_id)) + N'.' + QUOTENAME(i.name)), N', ')
                FROM @maal m
                JOIN sys.indexes i ON i.object_id = m.object_id AND i.index_id = m.index_id
                WHERE EXISTS (SELECT 1 FROM sys.partitions p
                              WHERE p.object_id = m.object_id AND p.index_id = m.index_id
                                AND p.data_compression = 0));

            IF @gjenstaar IS NOT NULL
            BEGIN
                DECLARE @feil nvarchar(2048) = LEFT(
                    N'PageCompressIndexes: fortsatt ukomprimert: ' + @gjenstaar
                    + N'. Første feil: ' + ISNULL(@forsteFeil, N'ingen'), 2048);
                THROW 50000, @feil, 1;
            END;
            """, suppressTransaction: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Bevisst tom. Komprimeringen endrer ikke skjema eller data, og å pakke ut
        // igjen ville bare gjort tabellene større.
    }
}

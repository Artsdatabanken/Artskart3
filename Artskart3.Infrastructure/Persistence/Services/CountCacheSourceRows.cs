using Artskart3.Core.Application.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Artskart3.Infrastructure.Persistence.Services;

/// <summary>
/// Radantallet i ObservationEntityIndex, som begge tellebufferne måler seg mot.
///
/// HVORFOR DET MÅLES I DET HELE TATT
/// En buffer som er eldre enn dataene blir ellers trodd: Status står på 'Ready',
/// SchemaVersion stemmer, og oppslaget svarer med tall fra forrige bygg. Formatet
/// er riktig, tallene er gamle, og ingenting feiler. Stemmer ikke radantallet med
/// det bufferen ble bygget fra, har indekstabellen endret seg siden, og bufferen
/// slår seg av selv framfor å svare.
///
/// HVORFOR KATALOGEN OG IKKE COUNT(*)
/// Tabellen har ~192 millioner rader. Et COUNT per kall ville kostet mer enn
/// oppslaget bufferen finnes for å gjøre raskt, og det ville ligget på den
/// hurtigste stien vi har. Katalogen svarer på én rad uten å lese tabellen.
///
/// HVORFOR sys.partitions OG IKKE sys.dm_db_partition_stats
/// De gir samme tall, men DMV-en krever <c>VIEW DATABASE STATE</c>, og det
/// gir ikke <c>db_datareader</c>. Under en least-privilege-pålogging — altså
/// det vanlige oppsettet i Azure SQL — ville hvert oppslag feilet, falt tilbake
/// til telling og logget en advarsel, mens konfigurasjonen sa at bufferne var
/// på. <c>sys.partitions</c> er en katalogvisning under reglene for
/// metadatasynlighet: har påloggingen SELECT på tabellen, kan den lese raden.
///
/// BEGGE SIDER MÅ MÅLE LIKT
/// Byggeskriptene skriver SourceRows med NØYAKTIG samme uttrykk (se
/// Scripts/BuildAreaCountCache.sql og BuildLocationCountCache.sql). Måler de to
/// sidene forskjellig, spriker tallene uten at dataene har endret seg, og bufferen
/// slår seg av uten grunn. Derfor står uttrykket her, ett sted, og skriptene
/// refererer til det.
///
/// <c>rows</c> er dokumentert som omtrentlig. Den vedlikeholdes av DML og er i
/// praksis eksakt, og skulle den drifte, er utfallet at bufferen ikke brukes —
/// tregere, aldri galt. Kolonnenavnet må stå i klammer: <c>ROWS</c> er et
/// reservert ord i T-SQL.
/// </summary>
public static class CountCacheSourceRows
{
    public const string Sql = """
        SELECT CAST(ISNULL(SUM(p.[rows]), 0) AS BIGINT) AS Value
        FROM sys.partitions p
        WHERE p.object_id = OBJECT_ID('dbo.ObservationEntityIndex')
          AND p.index_id IN (0, 1)
        """;

    public static Task<long> ReadAsync(IArtsKartDbContext context, CancellationToken cancellationToken) =>
        ((DbContext)context).Database.SqlQueryRaw<long>(Sql).SingleAsync(cancellationToken);
}

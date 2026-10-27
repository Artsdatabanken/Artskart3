using System.Data;
using Artskart3.Core.Application.Import.Writing;

namespace Artskart3.Infrastructure.Import.Writing;

/// <summary>
/// En kolonne i en staging-tabell.
/// </summary>
/// <param name="Name">Kolonnenavnet, som er det samme i staging og i måltabellen der begge har det.</param>
/// <param name="SqlType">
/// SQL-typen, ordrett slik den skal stå i <c>CREATE TABLE</c>. Den er en streng og ikke
/// en <c>SqlDbType</c> fordi det er denne teksten DDL-en trenger, og fordi lengden er
/// en del av typen.
/// </param>
public sealed record StagingColumn(string Name, string SqlType)
{
    /// <summary>Typenavnet uten lengde, slik det står i <c>sys.types</c>.</summary>
    public string BaseType
    {
        get
        {
            var parentes = SqlType.IndexOf('(', StringComparison.Ordinal);

            return parentes < 0 ? SqlType : SqlType[..parentes];
        }
    }
}

/// <summary>
/// En staging-tabell: hva den heter, hvilke kolonner den har, og hvordan radene hentes
/// ut av bunten.
///
/// SAMLET PÅ ETT STED FORDI DET ER HER PARITETEN BOR. Kolonnekartet er svaret på «hva
/// skriver importen, og hvor» — og det er lettere å lese som én tabell enn spredt over
/// tolv innsettingsmetoder.
/// </summary>
public interface IStagingTable
{
    /// <summary>Tabellen radene til slutt havner i.</summary>
    string TargetTable { get; }

    /// <summary>Den midlertidige tabellen radene bulk-kopieres til.</summary>
    string TempTable { get; }

    IReadOnlyList<StagingColumn> Columns { get; }

    /// <summary><c>CREATE TABLE</c> for staging-tabellen.</summary>
    string CreateTableSql { get; }

    /// <summary>Radene fra bunten, som en leser <c>SqlBulkCopy</c> kan ta.</summary>
    IDataReader CreateReader(ImportBatch batch);

    /// <summary>Antall rader bunten gir for denne tabellen. Til logging og tester.</summary>
    int CountRows(ImportBatch batch);
}

/// <summary>
/// Staging-tabellen for én radtype.
/// </summary>
/// <typeparam name="TRow">Radtypen kolonnene leser fra.</typeparam>
public sealed class StagingTable<TRow> : IStagingTable
{
    private readonly Func<ImportBatch, IEnumerable<TRow>> _rader;
    private readonly IReadOnlyList<Func<TRow, object?>> _lesere;

    public StagingTable(
        string targetTable,
        Func<ImportBatch, IEnumerable<TRow>> rows,
        params (string Name, string SqlType, Func<TRow, object?> Value)[] columns)
    {
        ArgumentException.ThrowIfNullOrEmpty(targetTable);
        ArgumentNullException.ThrowIfNull(rows);
        ArgumentNullException.ThrowIfNull(columns);

        if (columns.Length == 0) throw new ArgumentException("Tabellen må ha kolonner.", nameof(columns));

        TargetTable = targetTable;
        TempTable = "#stg_" + targetTable;

        _rader = rows;
        Columns = columns.Select(x => new StagingColumn(x.Name, x.SqlType)).ToArray();
        _lesere = columns.Select(x => x.Value).ToArray();
    }

    public string TargetTable { get; }

    public string TempTable { get; }

    public IReadOnlyList<StagingColumn> Columns { get; }

    public string CreateTableSql =>
        $"CREATE TABLE {TempTable} ("
        + string.Join(", ", Columns.Select(x => $"[{x.Name}] {x.SqlType} NULL"))
        + ");";

    public IDataReader CreateReader(ImportBatch batch) =>
        new ObjectDataReader<TRow>(_rader(batch), Columns.Select(x => x.Name).ToArray(), _lesere);

    public int CountRows(ImportBatch batch) => _rader(batch).Count();
}

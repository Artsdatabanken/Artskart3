using System.Data;

namespace Artskart3.Infrastructure.Import.Writing;

/// <summary>
/// Leser en sekvens av objekter som om den var et resultatsett, slik
/// <c>SqlBulkCopy</c> vil ha det.
///
/// HVORFOR IKKE EN DATATABLE: en bunt på 250 000 poster gir godt over en million rader
/// fordelt på tolv tabeller. En <c>DataTable</c> ville materialisert alle sammen én
/// gang til, med en boks rundt hver celle. Denne leseren strømmer rett fra
/// minnestrukturen.
///
/// BARE DET <c>SqlBulkCopy</c> BRUKER ER IMPLEMENTERT. Resten kaster, slik at en
/// fremtidig bruker som trenger mer oppdager det med en gang i stedet for å få
/// stilltiende gale verdier.
/// </summary>
/// <typeparam name="TRow">Radtypen.</typeparam>
public sealed class ObjectDataReader<TRow> : IDataReader
{
    private readonly IEnumerator<TRow> _rader;
    private readonly string[] _kolonnenavn;
    private readonly IReadOnlyList<Func<TRow, object?>> _lesere;
    private readonly Dictionary<string, int> _ordinaler;

    private TRow? _gjeldende;
    private bool _ferdig;

    public ObjectDataReader(
        IEnumerable<TRow> rows,
        string[] columnNames,
        IReadOnlyList<Func<TRow, object?>> readers)
    {
        ArgumentNullException.ThrowIfNull(rows);
        ArgumentNullException.ThrowIfNull(columnNames);
        ArgumentNullException.ThrowIfNull(readers);

        if (columnNames.Length != readers.Count)
        {
            throw new ArgumentException("Like mange kolonnenavn som lesere.", nameof(readers));
        }

        _rader = rows.GetEnumerator();
        _kolonnenavn = columnNames;
        _lesere = readers;
        _ordinaler = columnNames
            .Select((navn, i) => (navn, i))
            .ToDictionary(x => x.navn, x => x.i, StringComparer.OrdinalIgnoreCase);
    }

    public int FieldCount => _kolonnenavn.Length;

    public bool IsClosed => _ferdig;

    public int Depth => 0;

    public int RecordsAffected => -1;

    public object this[int i] => GetValue(i);

    public object this[string name] => GetValue(GetOrdinal(name));

    public bool Read()
    {
        if (_rader.MoveNext())
        {
            _gjeldende = _rader.Current;

            return true;
        }

        _gjeldende = default;
        _ferdig = true;

        return false;
    }

    public object GetValue(int i)
    {
        if (_gjeldende is null) throw new InvalidOperationException("Ingen gjeldende rad.");

        return _lesere[i](_gjeldende) ?? DBNull.Value;
    }

    public bool IsDBNull(int i) => GetValue(i) is DBNull;

    public string GetName(int i) => _kolonnenavn[i];

    public int GetOrdinal(string name) =>
        _ordinaler.TryGetValue(name, out var i)
            ? i
            : throw new IndexOutOfRangeException($"Kolonnen {name} finnes ikke.");

    public Type GetFieldType(int i)
    {
        var verdi = GetValue(i);

        return verdi is DBNull ? typeof(object) : verdi.GetType();
    }

    public string GetDataTypeName(int i) => GetFieldType(i).Name;

    public void Close() => Dispose();

    public void Dispose()
    {
        _rader.Dispose();
        _ferdig = true;
    }

    public bool NextResult() => false;

    public int GetValues(object[] values)
    {
        ArgumentNullException.ThrowIfNull(values);

        var antall = Math.Min(values.Length, FieldCount);

        for (var i = 0; i < antall; i++) values[i] = GetValue(i);

        return antall;
    }

    // --- Resten brukes ikke av SqlBulkCopy ---------------------------------

    public DataTable GetSchemaTable() => throw new NotSupportedException();

    public bool GetBoolean(int i) => (bool)GetValue(i)!;

    public byte GetByte(int i) => (byte)GetValue(i)!;

    public long GetBytes(int i, long fieldOffset, byte[]? buffer, int bufferoffset, int length) =>
        throw new NotSupportedException();

    public char GetChar(int i) => (char)GetValue(i)!;

    public long GetChars(int i, long fieldoffset, char[]? buffer, int bufferoffset, int length) =>
        throw new NotSupportedException();

    public IDataReader GetData(int i) => throw new NotSupportedException();

    public DateTime GetDateTime(int i) => (DateTime)GetValue(i)!;

    public decimal GetDecimal(int i) => (decimal)GetValue(i)!;

    public double GetDouble(int i) => (double)GetValue(i)!;

    public float GetFloat(int i) => (float)GetValue(i)!;

    public Guid GetGuid(int i) => (Guid)GetValue(i)!;

    public short GetInt16(int i) => (short)GetValue(i)!;

    public int GetInt32(int i) => (int)GetValue(i)!;

    public long GetInt64(int i) => (long)GetValue(i)!;

    public string GetString(int i) => (string)GetValue(i)!;
}

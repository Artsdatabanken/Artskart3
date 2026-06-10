using System.Globalization;
using System.Text;

namespace Artskart3.Workers.Export;

/// <summary>
/// Skriver CSV i henhold til RFC 4180, med semikolon som skilletegn (nordisk standard).
/// </summary>
public class CsvWriterService
{
    private const char Delimiter = ';';
    private const char Quote = '"';
    private static readonly string QuoteString = Quote.ToString();
    private static readonly string EscapedQuote = "\"\"";

    public async Task WriteHeaderAsync(StreamWriter writer, List<string> columnNames)
    {
        var line = string.Join(Delimiter, columnNames.Select(EscapeField));
        await writer.WriteLineAsync(line);
    }

    public async Task WriteRowAsync(StreamWriter writer, List<object?> values)
    {
        var line = string.Join(Delimiter, values.Select(FormatAndEscape));
        await writer.WriteLineAsync(line);
    }

    private static string FormatAndEscape(object? value)
    {
        var formatted = value switch
        {
            null => "",
            DateTime dt => dt.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
            bool b => b ? "1" : "0",
            double d => d.ToString(CultureInfo.InvariantCulture),
            float f => f.ToString(CultureInfo.InvariantCulture),
            _ => value.ToString() ?? ""
        };

        return EscapeField(formatted);
    }

    private static string EscapeField(string field)
    {
        if (field.Contains(Delimiter) || field.Contains(Quote) || field.Contains('\n') || field.Contains('\r'))
        {
            return Quote + field.Replace(QuoteString, EscapedQuote) + Quote;
        }

        return field;
    }
}

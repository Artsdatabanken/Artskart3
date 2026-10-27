using System.Text.Json;
using System.Text.Json.Serialization;
using Artskart3.Core.Application.Taxonomy;

namespace Artskart3.Workers.Taxonomy;

/// <summary>
/// Serialiseringskontrakten mot Artskart 2s dokumenter.
///
/// EGEN KLASSE FORDI DEN ER TESTBAR UTEN EN SERVER. Strømmeendepunktet i RavenDB kan
/// bare verifiseres mot en kjørende instans, men at dokumentformene leses riktig —
/// skrivefeilene i feltnavnene inkludert — er en ren funksjon av JSON og kan måles her.
/// </summary>
public static class TaxonomyJson
{
    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        NumberHandling = JsonNumberHandling.AllowReadingFromString,
        Converters = { new TaxonomicCategoryConverter() }
    };

    /// <summary>
    /// Rangen kan komme som streng eller tall.
    ///
    /// RavenDB 3.5 lagrer enumer som strenger med standardoppsettet, men
    /// <c>SaveEnumsAsIntegers</c> kan være slått på, og eldre dokumenter kan ha tall.
    /// Konverteren tar begge framfor å gamble på hvilken det er — og en ukjent verdi
    /// blir <see cref="TaxonomicCategory.NotSet"/>, slik Artskart 2s enum har en egen
    /// verdi for nettopp det.
    /// </summary>
    private sealed class TaxonomicCategoryConverter : JsonConverter<TaxonomicCategory>
    {
        public override TaxonomicCategory Read(
            ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
            reader.TokenType switch
            {
                JsonTokenType.Number => Enum.IsDefined(typeof(TaxonomicCategory), reader.GetInt32())
                    ? (TaxonomicCategory)reader.GetInt32()
                    : TaxonomicCategory.NotSet,
                JsonTokenType.String => Enum.TryParse<TaxonomicCategory>(
                    reader.GetString(), ignoreCase: true, out var rang)
                    ? rang
                    : TaxonomicCategory.NotSet,
                _ => TaxonomicCategory.NotSet
            };

        public override void Write(
            Utf8JsonWriter writer, TaxonomicCategory value, JsonSerializerOptions options) =>
            writer.WriteStringValue(value.ToString());
    }
}

using System.Text.Json;
using Artskart3.Infrastructure.Cache;
using FluentAssertions;

namespace Artskart3.Tests.Unit;

/// <summary>
/// JSON-en i testene er formet slik Artskart 2 skriver den: medlemsnavn som de står,
/// ISO 8601-datoer, og null- og standardverdier utelatt (<c>DefaultValueHandling.Ignore</c>,
/// <c>NullValueHandling.Ignore</c>).
/// </summary>
public class CacheRecordSerializerTests
{
    [Fact]
    public void Deserialize_leser_identitet_og_taksonomi()
    {
        const string json = """
            {
              "Id": "o_1234",
              "NodeDatabaseID": 50,
              "OccurrenceID": "urn:uuid:abc",
              "CatalogNumber": "O-1234567",
              "InstitutionCode": "UiO",
              "CollectionCode": "O",
              "ScientificName": "Vulpes vulpes",
              "ScientificNameAuthor": "Linnaeus, 1758",
              "NorskNavn": "rødrev",
              "TaxonRank": "species"
            }
            """;

        var record = CacheRecordSerializer.Deserialize(json);

        record.Id.Should().Be("o_1234");
        record.NodeDatabaseID.Should().Be(50);
        record.OccurrenceID.Should().Be("urn:uuid:abc");
        record.CatalogNumber.Should().Be("O-1234567");
        record.InstitutionCode.Should().Be("UiO");
        record.CollectionCode.Should().Be("O");
        record.ScientificName.Should().Be("Vulpes vulpes");
        record.ScientificNameAuthor.Should().Be("Linnaeus, 1758");
        record.NorskNavn.Should().Be("rødrev");
        record.TaxonRank.Should().Be("species");
    }

    [Fact]
    public void Deserialize_leser_koordinater_med_punktum_som_desimalskille()
    {
        const string json = """
            {"Latitude": 59.9139, "Longitude": 10.7522, "UTMnord": 6643000, "UTMost": 262000, "UTMsone": 33}
            """;

        var record = CacheRecordSerializer.Deserialize(json);

        record.Latitude.Should().Be(59.9139);
        record.Longitude.Should().Be(10.7522);
        record.UTMnord.Should().Be(6643000);
        record.UTMost.Should().Be(262000);
        record.UTMsone.Should().Be((short)33);
    }

    [Fact]
    public void Deserialize_tolker_tall_som_er_skrevet_som_streng()
    {
        const string json = """{"UTMsone": "33", "YearCollected": "1998"}""";

        var record = CacheRecordSerializer.Deserialize(json);

        record.UTMsone.Should().Be((short)33);
        record.YearCollected.Should().Be((short)1998);
    }

    [Fact]
    public void Deserialize_leser_datoer_i_iso_format()
    {
        const string json = """
            {"DateLastModified": "2019-04-02T00:00:00", "TrackInfoDateImported": "2024-11-05T13:45:12.123"}
            """;

        var record = CacheRecordSerializer.Deserialize(json);

        record.DateLastModified.Should().Be(new DateTime(2019, 4, 2, 0, 0, 0));
        record.TrackInfoDateImported.Should().Be(new DateTime(2024, 11, 5, 13, 45, 12, 123));
    }

    [Fact]
    public void Deserialize_lar_utelatte_felt_sta_som_standardverdi()
    {
        const string json = """{"Id": "o_1"}""";

        var record = CacheRecordSerializer.Deserialize(json);

        record.ScientificName.Should().BeNull();
        record.UnknownFields.Should().BeNull();
        record.Multimedias.Should().BeNull();
        record.IsDeleted.Should().BeFalse();
        record.YearCollected.Should().Be((short)0);
        record.Year.Should().BeNull();
    }

    [Fact]
    public void Deserialize_trimmer_feltene_Artskart2_trimmer()
    {
        const string json = """
            {"ScientificName": "  Vulpes vulpes  ", "Locality": "  Nordmarka ", "CatalogNumber": " O-1 "}
            """;

        var record = CacheRecordSerializer.Deserialize(json);

        record.ScientificName.Should().Be("Vulpes vulpes");
        record.Locality.Should().Be("Nordmarka");
        record.CatalogNumber.Should().Be("O-1");
    }

    /// <summary>
    /// Blanke strenger blir null, slik setterne i Artskart 2 gjør det. Det er den
    /// forskjellen alle <c>IsNullOrEmpty</c>-sjekkene i importen ser.
    /// </summary>
    [Fact]
    public void Deserialize_gjor_blanke_strenger_til_null()
    {
        const string json = """{"ScientificName": "   ", "Locality": "", "Notes": "\t"}""";

        var record = CacheRecordSerializer.Deserialize(json);

        record.ScientificName.Should().BeNull();
        record.Locality.Should().BeNull();
        record.Notes.Should().BeNull();
    }

    [Fact]
    public void Deserialize_rorer_ikke_felt_Artskart2_ikke_trimmer()
    {
        const string json = """{"LocationID": "  loc-1  ", "EventDate": " 2019-04-02 "}""";

        var record = CacheRecordSerializer.Deserialize(json);

        record.LocationID.Should().Be("  loc-1  ");
        record.EventDate.Should().Be(" 2019-04-02 ");
    }

    [Fact]
    public void Deserialize_leser_ukjente_felt_som_ordbok()
    {
        const string json = """
            {"UnknownFields": {"municipality": "Oslo", "waterBody": "Oslofjorden"}}
            """;

        var record = CacheRecordSerializer.Deserialize(json);

        record.UnknownFields.Should().NotBeNull();
        record.UnknownFields!["municipality"].Should().Be("Oslo");
        record.UnknownFields["waterBody"].Should().Be("Oslofjorden");
    }

    /// <summary>
    /// Harvest kan ha lagret subklassen <c>TrackableMultimedia</c>, som tar med
    /// <c>Id</c> og <c>DateLastModified</c>. Artskart 2 deserialiserer til basisklassen
    /// og ignorerer dem; vi skal gjøre det samme uten å feile.
    /// </summary>
    [Fact]
    public void Deserialize_leser_multimedia_og_ignorerer_ukjente_medlemmer()
    {
        const string json = """
            {
              "Multimedias": [
                {
                  "Type": "StillImage",
                  "Identifier": "https://example.org/bilde.jpg",
                  "Created": "2015-06-01T00:00:00",
                  "RightsHolder": "Artsdatabanken",
                  "Id": "media/1",
                  "DateLastModified": "2016-01-01T00:00:00"
                }
              ]
            }
            """;

        var record = CacheRecordSerializer.Deserialize(json);

        record.Multimedias.Should().HaveCount(1);
        record.Multimedias![0].Type.Should().Be("StillImage");
        record.Multimedias[0].Identifier.Should().Be("https://example.org/bilde.jpg");
        record.Multimedias[0].Created.Should().Be(new DateTime(2015, 6, 1));
        record.Multimedias[0].RightsHolder.Should().Be("Artsdatabanken");
    }

    [Fact]
    public void Deserialize_leser_maalinger_og_utvidede_maalinger()
    {
        const string json = """
            {
              "MeasurementOrFacts": [{"MeasurementType": "lengde", "MeasurementValue": "12", "MeasurementUnit": "cm"}],
              "ExtendedMeasurementOrFacts": [
                {"MeasurementType": "temperatur", "MeasurementValue": "4", "MeasurementUnit": "C", "MeasurementId": "m1"}
              ]
            }
            """;

        var record = CacheRecordSerializer.Deserialize(json);

        record.MeasurementOrFacts.Should().HaveCount(1);
        record.MeasurementOrFacts![0].MeasurementUnit.Should().Be("cm");
        record.ExtendedMeasurementOrFacts.Should().HaveCount(1);
        record.ExtendedMeasurementOrFacts![0].MeasurementType.Should().Be("temperatur");
        record.ExtendedMeasurementOrFacts[0].MeasurementId.Should().Be("m1");
    }

    [Fact]
    public void Deserialize_av_ugyldig_json_kaster_JsonException()
    {
        var act = () => CacheRecordSerializer.Deserialize("{ikke json");

        act.Should().Throw<JsonException>();
    }

    [Fact]
    public void Deserialize_av_tom_tekst_kaster_JsonException()
    {
        var act = () => CacheRecordSerializer.Deserialize(string.Empty);

        act.Should().Throw<JsonException>();
    }
}

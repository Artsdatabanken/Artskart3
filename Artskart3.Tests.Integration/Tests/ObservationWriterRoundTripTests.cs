using Artskart3.Core.Application.Import.Models;
using Artskart3.Core.Application.Import.Writing;
using Artskart3.Core.Domain.Enums;
using Artskart3.Infrastructure.Import.Writing;
using FluentAssertions;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging.Abstractions;
using NetTopologySuite.Geometries;
using Xunit.Abstractions;

namespace Artskart3.Tests.Integration.Tests;

/// <summary>
/// Skriver en liten bunt gjennom <see cref="ObservationWriter"/> og leser radene
/// tilbake. Dette er testen som faktisk beviser at publiseringen virker — de andre
/// sjekker bare at SQL-en og kolonnekartet henger sammen.
///
/// DEN SKRIVER, OG DERFOR HAR DEN SIN EGEN TILKOBLINGSVARIABEL:
///
///   $env:ARTSKART_WRITE_TEST_CONNECTION = "data source=localhost;initial catalog=Artskart3ImportWriteTest;Integrated Security=true;TrustServerCertificate=True"
///   dotnet test Artskart3.Tests.Integration --filter "FullyQualifiedName~ObservationWriterRoundTrip"
///
/// OG DEN NEKTER Å KJØRE MOT EN BASE UTEN MARKØRTABELLEN <c>__ImportWriteTest</c>.
/// Variabelen er med vilje en annen enn <c>ARTSKART_INDEX_CONNECTION</c>, og markøren er
/// det andre gjerdet.
///
/// TESTBASEN BYGGES IKKE AV MIGRASJONENE. <c>InitialBaseline</c> er bevisst tom —
/// skjemaet kommer fra en Artskart 2-dump, ikke fra migrasjonskjeden — så tabellene må
/// genereres fra en base som har dem. Testbasen har derfor ingen fremmednøkler, og
/// testen beviser at setningene virker, ikke at referanseintegriteten holder.
///
/// TRENGER IKKE DOCKER — men den trenger en base noen har opprettet for formålet.
/// </summary>
public class ObservationWriterRoundTripTests
{
    private const string ConnectionVariable = "ARTSKART_WRITE_TEST_CONNECTION";

    private readonly ITestOutputHelper _output;

    public ObservationWriterRoundTripTests(ITestOutputHelper output)
    {
        _output = output;
    }

    private static ObservationWriter Skriver(string connectionString) =>
        new(
            new ObservationWriterOptions { ConnectionString = connectionString },
            NullLogger<ObservationWriter>.Instance);

    [Fact]
    public async Task Skriver_en_bunt_og_leser_den_tilbake()
    {
        var connectionString = Environment.GetEnvironmentVariable(ConnectionVariable);

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            _output.WriteLine($"{ConnectionVariable} er ikke satt - testen gjør ingenting.");
            return;
        }

        await KrevTestbase(connectionString);
        await Rydd(connectionString);

        var bunt = new ImportBatch();
        var buffer = new LocationIdCache();

        bunt.Add(Post("test/samling/1", Lokalitet("test_600000_6600000_100")), buffer);
        bunt.Add(Post("test/samling/2", Lokalitet("test_600000_6600000_100")), buffer);

        var resultat = await Skriver(connectionString).PublishAsync(bunt, buffer, CancellationToken.None);

        _output.WriteLine($"{resultat.Observations} observasjoner, {resultat.Locations} lokaliteter, "
                          + $"{resultat.StagedRows} rader i staging, {resultat.Elapsed.TotalMilliseconds:F0} ms");

        resultat.Observations.Should().Be(2);
        resultat.Locations.Should().Be(1, "begge postene deler lokalitet");

        var skrevet = await LesAntall(connectionString);

        skrevet["Observation"].Should().Be(2);
        skrevet["ObservationDetails"].Should().Be(2);
        skrevet["Location"].Should().Be(1);
        skrevet["ObservationTags"].Should().Be(2, "én tagg per post");

        buffer.Count.Should().Be(1, "lokalitets-Id-en skal være ført inn i bufferet");
    }

    /// <summary>
    /// Samme bunt to ganger skal gi samme antall rader. Det er hele poenget med
    /// <c>MERGE</c> på <c>ProxyId</c>: en inkrementell kjøring som ser posten igjen
    /// skal oppdatere den, ikke lage en ny.
    /// </summary>
    [Fact]
    public async Task Samme_bunt_to_ganger_gir_samme_antall_rader()
    {
        var connectionString = Environment.GetEnvironmentVariable(ConnectionVariable);

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            _output.WriteLine($"{ConnectionVariable} er ikke satt - testen gjør ingenting.");
            return;
        }

        await KrevTestbase(connectionString);
        await Rydd(connectionString);

        var skriver = Skriver(connectionString);

        var første = new ImportBatch();
        første.Add(Post("test/samling/1", Lokalitet("test_600000_6600000_100")));
        await skriver.PublishAsync(første, cancellationToken: CancellationToken.None);

        var andre = new ImportBatch();
        andre.Add(Post("test/samling/1", Lokalitet("test_600000_6600000_100")));
        await skriver.PublishAsync(andre, cancellationToken: CancellationToken.None);

        var skrevet = await LesAntall(connectionString);

        skrevet["Observation"].Should().Be(1);
        skrevet["Location"].Should().Be(1);
        skrevet["ObservationTags"].Should().Be(1, "taggene settes opp på nytt, ikke i tillegg");
    }

    /// <summary>
    /// Kontrollsteget skal stoppe runden, og feilen skal si hvilket steg det var.
    /// Stegkjeden hindrer dubletter per node (<c>ImportCheckUniqueIdStep</c>), men
    /// kommer de hit likevel, ville <c>MERGE</c> feilet med feil 8672 og en melding
    /// ingen forstår.
    /// </summary>
    [Fact]
    public async Task Stopper_en_bunt_med_samme_identitet_to_ganger()
    {
        var connectionString = Environment.GetEnvironmentVariable(ConnectionVariable);

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            _output.WriteLine($"{ConnectionVariable} er ikke satt - testen gjør ingenting.");
            return;
        }

        await KrevTestbase(connectionString);
        await Rydd(connectionString);

        var bunt = new ImportBatch();
        bunt.Add(Post("test/samling/1", Lokalitet("test_600000_6600000_100")));
        bunt.Add(Post("test/samling/1", Lokalitet("test_600000_6600000_100")));

        var handling = async () =>
            await Skriver(connectionString).PublishAsync(bunt, cancellationToken: CancellationToken.None);

        await handling.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Kontroll*samme ProxyId*");

        var skrevet = await LesAntall(connectionString);

        skrevet["Observation"].Should().Be(0, "transaksjonen skal være rullet tilbake");
        skrevet["Location"].Should().Be(0);
    }

    private static ImportedLocation Lokalitet(string lookupId)
    {
        var lokalitet = new ImportedLocation
        {
            LookupId = lookupId,
            NodeId = 9999,
            East = 600000,
            North = 6600000,
            Latitude = 59.5,
            Longitude = 10.5,
            CoordinatePrecision = 100,
            Locality = "Testlokalitet",
            Geometry = new GeometryFactory().CreatePoint(new Coordinate(600000, 6600000))
        };

        return lokalitet;
    }

    private static ImportedObservation Post(string proxyId, ImportedLocation lokalitet)
    {
        var observasjon = new ImportedObservation
        {
            ProxyId = proxyId,
            NodeId = 9999,
            CatalogNumber = proxyId.Split('/').Last(),
            DateLastModified = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            DateTimeRecordImported = new DateTime(2026, 10, 14, 0, 0, 0, DateTimeKind.Utc),
            DateTimeRecordProcessed = new DateTime(2026, 10, 14, 0, 0, 0, DateTimeKind.Utc),
            BasisOfRecordId = 2,
            TaxonId = 1,
            MatchedScientificNameId = 1,
            TaxonGroupId = 1,
            Latitude = 59.5,
            Longitude = 10.5,
            East = 600000,
            North = 6600000,
            CoordinatePrecisionInMeters = 100,
            Location = lokalitet,
            RegistrationStatusId = 1
        };

        observasjon.AddTag(Tag.QualityIssue);

        return observasjon;
    }

    /// <summary>
    /// Tabellene testen skriver til, og tømmer før hver kjøring.
    /// </summary>
    private static readonly string[] Skrivetabeller =
    [
        "Location", "LocationAreas", "Observation", "ObservationDetails", "ObservationTags",
        "ObservationTaxons", "ObservationBehaviors", "ObservationProject", "MediaFile",
        "ObservationError", "ObservationLink", "SensitiveObservationData",
        "ObservationTaxonHierarchy", "ObservationEntityIndex"
    ];

    /// <summary>
    /// Gjerdet: basen må ha markørtabellen <c>__ImportWriteTest</c>.
    ///
    /// EN MARKØR, IKKE EN HEURISTIKK. «Ingen observasjoner» holder bare til testen har
    /// kjørt én gang, og «bare skrivetabellene» holder bare til basen får referansedata.
    /// En tabell noen har opprettet med vilje er det eneste gjerdet som ikke forvitrer —
    /// og ingen produksjonsbase kommer til å ha den.
    /// </summary>
    internal static async Task KrevTestbase(string connectionString)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(
            "SELECT COUNT(*) FROM sys.tables WHERE name = '__ImportWriteTest';", connection);

        var markør = (int)(await command.ExecuteScalarAsync())!;

        markør.Should().Be(1,
            "testen skriver, og skal bare kjøre mot en base som er merket som testbase");
    }

    /// <summary>
    /// Tømmer tabellene før hver test. Barna først — det finnes ingen
    /// fremmednøkler i testbasen, men rekkefølgen er den samme som i en ekte base.
    /// </summary>
    internal static async Task Rydd(string connectionString)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();

        foreach (var tabell in Skrivetabeller.Reverse())
        {
            await using var command = new SqlCommand($"DELETE FROM dbo.{tabell};", connection);

            await command.ExecuteNonQueryAsync();
        }
    }

    private static async Task<Dictionary<string, int>> LesAntall(string connectionString)
    {
        string[] tabeller =
            ["Observation", "ObservationDetails", "Location", "LocationAreas", "ObservationTags"];

        var resultat = new Dictionary<string, int>();

        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();

        foreach (var tabell in tabeller)
        {
            await using var command = new SqlCommand($"SELECT COUNT(*) FROM dbo.{tabell};", connection);

            resultat[tabell] = (int)(await command.ExecuteScalarAsync())!;
        }

        return resultat;
    }
}

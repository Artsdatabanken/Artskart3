using Artskart3.Core.Application.Import.Models;
using Artskart3.Core.Application.Import.Writing;
using Artskart3.Core.Domain.Enums;
using Artskart3.Infrastructure.Import.Writing;
using FluentAssertions;
using NetTopologySuite.Geometries;

namespace Artskart3.Tests.Unit;

public class ImportStagingSchemaTests
{
    private readonly ImportBatch _bunt = new();

    private static ImportedObservation Observasjon(string proxyId = "o/sopp/1")
    {
        return new ImportedObservation
        {
            ProxyId = proxyId,
            NodeId = 40,
            CatalogNumber = "1",
            BasisOfRecordId = 2,
            TaxonId = 100,
            MatchedScientificNameId = 200,
            TaxonGroupId = 3,
            Latitude = 59.9,
            Longitude = 10.7,
            East = 600000,
            North = 6600000,
            RegistrationStatusId = 1
        };
    }

    private static ImportedLocation Lokalitet(string lookupId = "40_600000_6600000_100") =>
        new() { LookupId = lookupId, NodeId = 40, East = 600000, North = 6600000 };

    [Fact]
    public void Dekker_tolv_tabeller()
    {
        ImportStagingSchema.All.Should().HaveCount(12);
    }

    /// <summary>
    /// Lokalitetene må publiseres før observasjonene, som må publiseres før barna sine.
    /// </summary>
    [Fact]
    public void Lokaliteter_kommer_forst_og_observasjonen_for_barna()
    {
        var rekkefølge = ImportStagingSchema.All.Select(x => x.TargetTable).ToList();

        rekkefølge.IndexOf("Location").Should().BeLessThan(rekkefølge.IndexOf("Observation"));
        rekkefølge.IndexOf("Observation").Should().BeLessThan(rekkefølge.IndexOf("ObservationDetails"));
        rekkefølge.IndexOf("Observation").Should().BeLessThan(rekkefølge.IndexOf("ObservationTags"));
    }

    [Fact]
    public void Hver_tabell_har_et_eget_staging_navn()
    {
        ImportStagingSchema.All.Select(x => x.TempTable).Should().OnlyHaveUniqueItems();
        ImportStagingSchema.All.Should().OnlyContain(x => x.TempTable == "#stg_" + x.TargetTable);
    }

    [Fact]
    public void Ingen_tabell_har_dupliserte_kolonnenavn()
    {
        foreach (var tabell in ImportStagingSchema.All)
        {
            tabell.Columns.Select(x => x.Name).Should()
                .OnlyHaveUniqueItems($"{tabell.TargetTable} skal ikke ha samme kolonne to ganger");
        }
    }

    /// <summary>
    /// Barneradene bærer forretningsnøkkelen, ikke <c>ObservationId</c> —
    /// identitetskolonnen finnes ikke før observasjonen er publisert.
    /// </summary>
    [Fact]
    public void Barnetabellene_barer_proxyid()
    {
        var barn = ImportStagingSchema.All
            .Where(x => x.TargetTable.StartsWith("Observation", StringComparison.Ordinal)
                        || x.TargetTable is "MediaFile" or "SensitiveObservationData")
            .ToList();

        barn.Should().OnlyContain(x => x.Columns.Any(y => y.Name == "ProxyId"));
    }

    [Fact]
    public void Lokalitetstabellene_barer_lookupid()
    {
        ImportStagingSchema.Location.Columns.Should().Contain(x => x.Name == "LookupId");
        ImportStagingSchema.LocationAreas.Columns.Should().Contain(x => x.Name == "LookupId");
    }

    /// <summary>
    /// De tre kolonnene databasen beregner selv. En innsetting i dem feiler.
    /// </summary>
    [Theory]
    [InlineData("Observation", "YearCollected")]
    [InlineData("Observation", "MonthCollected")]
    [InlineData("Location", "GeometryTypeId")]
    public void Tar_ikke_med_beregnede_kolonner(string tabell, string kolonne)
    {
        ImportStagingSchema.All.Single(x => x.TargetTable == tabell)
            .Columns.Should().NotContain(x => x.Name == kolonne);
    }

    [Fact]
    public void Lager_create_table_for_staging()
    {
        var sql = ImportStagingSchema.ObservationTags.CreateTableSql;

        sql.Should().Be("CREATE TABLE #stg_ObservationTags ([ProxyId] nvarchar(255) NULL, [TagId] int NULL);");
    }

    // --- Radene -----------------------------------------------------------

    [Fact]
    public void Observasjonstabellen_gir_en_rad_per_post()
    {
        _bunt.Add(Observasjon("a"));
        _bunt.Add(Observasjon("b"));

        ImportStagingSchema.Observation.CountRows(_bunt).Should().Be(2);
        ImportStagingSchema.ObservationDetails.CountRows(_bunt).Should().Be(2);
    }

    [Fact]
    public void Taggtabellen_gir_en_rad_per_tagg()
    {
        var observasjon = Observasjon();
        observasjon.AddTag(Tag.QualityIssue);
        observasjon.AddTag(Tag.HasImage);

        _bunt.Add(observasjon);

        ImportStagingSchema.ObservationTags.CountRows(_bunt).Should().Be(2);
    }

    [Fact]
    public void Lokalitetsomradene_gir_en_rad_per_omrade()
    {
        var lokalitet = Lokalitet();
        lokalitet.Areas.Add(new LocationArea(1, 1, "0301", "Oslo"));
        lokalitet.Areas.Add(new LocationArea(2, 2, "03", "Oslo"));

        var observasjon = Observasjon();
        observasjon.Location = lokalitet;

        _bunt.Add(observasjon);

        ImportStagingSchema.LocationAreas.CountRows(_bunt).Should().Be(2);
    }

    [Fact]
    public void Sensitivtabellen_gir_bare_rader_for_sensitive_poster()
    {
        _bunt.Add(Observasjon("a"));

        var sensitiv = Observasjon("b");
        sensitiv.SensitiveData = new ImportedSensitiveData { Latitude = 59.9, Longitude = 10.7 };
        _bunt.Add(sensitiv);

        ImportStagingSchema.SensitiveObservationData.CountRows(_bunt).Should().Be(1);
    }

    // --- Leseren ----------------------------------------------------------

    [Fact]
    public void Leseren_gir_kolonnene_i_rekkefolge()
    {
        _bunt.Add(Observasjon("a"));

        using var leser = ImportStagingSchema.Observation.CreateReader(_bunt);

        leser.FieldCount.Should().Be(ImportStagingSchema.Observation.Columns.Count);
        leser.GetName(0).Should().Be("ProxyId");
        leser.Read().Should().BeTrue();
        leser.GetValue(0).Should().Be("a");
        leser.Read().Should().BeFalse();
    }

    [Fact]
    public void Leseren_gir_dbnull_for_en_tom_verdi()
    {
        _bunt.Add(Observasjon("a"));

        using var leser = ImportStagingSchema.Observation.CreateReader(_bunt);
        leser.Read();

        leser.IsDBNull(leser.GetOrdinal("CategoryId")).Should().BeTrue();
        leser.GetValue(leser.GetOrdinal("CategoryId")).Should().Be(DBNull.Value);
    }

    [Fact]
    public void Leseren_finner_kolonnen_pa_navn()
    {
        _bunt.Add(Observasjon("a"));

        using var leser = ImportStagingSchema.Observation.CreateReader(_bunt);
        leser.Read();

        leser.GetValue(leser.GetOrdinal("NodeId")).Should().Be(40);
    }

    [Fact]
    public void Leseren_kaster_for_en_kolonne_som_ikke_finnes()
    {
        using var leser = ImportStagingSchema.Observation.CreateReader(_bunt);

        var handling = () => leser.GetOrdinal("FinnesIkke");

        handling.Should().Throw<IndexOutOfRangeException>();
    }

    /// <summary>
    /// Kolonnen er <c>NOT NULL</c>, og en tom liste skal bli en tom streng.
    /// </summary>
    [Fact]
    public void Tomme_referanser_blir_en_tom_streng()
    {
        _bunt.Add(Observasjon("a"));

        using var leser = ImportStagingSchema.ObservationDetails.CreateReader(_bunt);
        leser.Read();

        leser.GetValue(leser.GetOrdinal("AssociatedReferences")).Should().Be(string.Empty);
    }

    [Fact]
    public void Flere_referanser_skilles_med_pipe()
    {
        var observasjon = Observasjon();
        observasjon.AddAssociatedReference("Hoem 2016");
        observasjon.AddAssociatedReference("Sandmark 2019");

        _bunt.Add(observasjon);

        using var leser = ImportStagingSchema.ObservationDetails.CreateReader(_bunt);
        leser.Read();

        leser.GetValue(leser.GetOrdinal("AssociatedReferences")).Should().Be("Hoem 2016|Sandmark 2019");
    }

    /// <summary>
    /// Geometrien stages som WKB, så bulk-kopieringen ikke trenger
    /// <c>Microsoft.SqlServer.Types</c>. SRID-en settes i publiseringen.
    /// </summary>
    [Fact]
    public void Geometri_stages_som_wkb()
    {
        var lokalitet = Lokalitet();
        lokalitet.Geometry = new GeometryFactory().CreatePoint(new Coordinate(600000, 6600000));

        var observasjon = Observasjon();
        observasjon.Location = lokalitet;

        _bunt.Add(observasjon);

        using var leser = ImportStagingSchema.Location.CreateReader(_bunt);
        leser.Read();

        leser.GetValue(leser.GetOrdinal("GeometryWkb")).Should().BeOfType<byte[]>()
            .Which.Should().NotBeEmpty();
    }

    [Fact]
    public void Uten_geometri_stages_null()
    {
        var observasjon = Observasjon();
        observasjon.Location = Lokalitet();

        _bunt.Add(observasjon);

        using var leser = ImportStagingSchema.Location.CreateReader(_bunt);
        leser.Read();

        leser.IsDBNull(leser.GetOrdinal("GeometryWkb")).Should().BeTrue();
    }

    /// <summary>
    /// Kolonnen på observasjonen tar én atferd. Koblingstabellen får alle.
    /// </summary>
    [Fact]
    public void Atferdskolonnen_far_den_forste_og_koblingstabellen_alle()
    {
        var observasjon = Observasjon();
        observasjon.BehaviorIds.Add(2);
        observasjon.BehaviorIds.Add(4);

        _bunt.Add(observasjon);

        using var leser = ImportStagingSchema.Observation.CreateReader(_bunt);
        leser.Read();

        leser.GetValue(leser.GetOrdinal("BehaviorId")).Should().Be((byte)2);
        ImportStagingSchema.ObservationBehaviors.CountRows(_bunt).Should().Be(2);
    }

    [Fact]
    public void Atferdskolonnen_er_tom_uten_atferd()
    {
        _bunt.Add(Observasjon());

        using var leser = ImportStagingSchema.Observation.CreateReader(_bunt);
        leser.Read();

        leser.IsDBNull(leser.GetOrdinal("BehaviorId")).Should().BeTrue();
    }
}

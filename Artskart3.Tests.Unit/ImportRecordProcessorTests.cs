using Artskart3.Core.Application.Import;
using Artskart3.Core.Application.Import.Models;
using FluentAssertions;

namespace Artskart3.Tests.Unit;

public class ImportRecordProcessorTests
{
    private static readonly ImportRunSettings Settings = new()
    {
        NodeId = 43,
        ProcessEngineId = 7,
        ProcessedAt = new DateTime(2026, 10, 13, 9, 0, 0),
        ImportedAt = new DateTime(2026, 10, 13, 9, 0, 5)
    };

    private static CacheRecord Post(bool isDeleted = false) => new()
    {
        Id = "p_1",
        NodeDatabaseID = 43,
        IsDeleted = isDeleted,
        DateLastModified = new DateTime(2024, 5, 4),
        HashCode = 4711,
        CatalogNumber = "O-1",
        ScientificName = "Vulpes vulpes"
    };

    /// <summary>
    /// Et steg som noterer rekkefølgen det ble kjørt i, og svarer med det det blir
    /// fortalt.
    /// </summary>
    private sealed class FakeStep(
        string navn,
        ImportStepType type,
        List<string> kjørte,
        ImportRecordResult? resultat = null,
        Action<ImportedObservation, ImportContext>? virkning = null) : IImportStep
    {
        public ImportStepType StepType => type;

        public ImportRecordResult ValidateAndImport(
            CacheRecord record, ImportedObservation observation, ImportContext context)
        {
            lock (kjørte) kjørte.Add(navn);

            virkning?.Invoke(observation, context);

            return resultat ?? ImportRecordResult.Ok();
        }
    }

    private sealed class FakeIdentityStep(
        List<string> kjørte,
        string? proxyId = "uio/o/o-1") : IImportIdentityStep
    {
        public ImportStepType StepType => ImportStepType.Pre;

        public ImportRecordResult ValidateAndImport(
            CacheRecord record, ImportedObservation observation, ImportContext context)
        {
            kjørte.Add("identitet");
            observation.ProxyId = proxyId;

            return ImportRecordResult.Ok();
        }
    }

    [Fact]
    public void Kjorer_fasene_i_rekkefolge_pre_parallell_post()
    {
        var kjørte = new List<string>();
        var prosessor = new ImportRecordProcessor([
            new FakeStep("post1", ImportStepType.Post, kjørte),
            new FakeStep("parallell", ImportStepType.Parallel, kjørte),
            new FakeStep("pre1", ImportStepType.Pre, kjørte),
            new FakeStep("pre2", ImportStepType.Pre, kjørte)
        ]);

        var utfall = prosessor.Process(Post(), Settings);

        utfall.Status.Should().Be(ImportRecordStatus.Imported);
        kjørte.Should().Equal(["pre1", "pre2", "parallell", "post1"]);
    }

    [Fact]
    public void Setter_feltene_som_kommer_fra_kjoringen_og_posten()
    {
        var prosessor = new ImportRecordProcessor([]);

        var observasjon = prosessor.Process(Post(), Settings).Observation;

        observasjon.NodeId.Should().Be(43);
        observasjon.ProcessEngineId.Should().Be(7);
        observasjon.DateTimeRecordProcessed.Should().Be(Settings.ProcessedAt);
        observasjon.DateTimeRecordImported.Should().Be(Settings.ImportedAt);
        observasjon.DateLastModified.Should().Be(new DateTime(2024, 5, 4));
        observasjon.HashCode.Should().Be(4711, "hashen bæres gjennom fra cachen, ikke regnes om");
        observasjon.HasErrors.Should().BeFalse();
    }

    [Fact]
    public void Stopper_ved_forste_avvisning_i_pre_fasen()
    {
        var kjørte = new List<string>();
        var prosessor = new ImportRecordProcessor([
            new FakeStep("pre1", ImportStepType.Pre, kjørte),
            new FakeStep("pre2", ImportStepType.Pre, kjørte, ImportRecordResult.Rejected(
                RecordValidationCode.CatalogNumberIsMissing, RecordNotificationCode.Ok, "mangler katalognummer")),
            new FakeStep("pre3", ImportStepType.Pre, kjørte),
            new FakeStep("parallell", ImportStepType.Parallel, kjørte),
            new FakeStep("post1", ImportStepType.Post, kjørte)
        ]);

        var utfall = prosessor.Process(Post(), Settings);

        utfall.Status.Should().Be(ImportRecordStatus.Rejected);
        utfall.Errors.Should().HaveCount(1);
        utfall.Errors[0].ValidationCode.Should().Be(RecordValidationCode.CatalogNumberIsMissing);
        kjørte.Should().Equal(["pre1", "pre2"], "ingenting etter avvisningen skal kjøre");
    }

    [Fact]
    public void Stopper_ved_forste_avvisning_i_post_fasen()
    {
        var kjørte = new List<string>();
        var prosessor = new ImportRecordProcessor([
            new FakeStep("post1", ImportStepType.Post, kjørte, ImportRecordResult.Rejected(
                RecordValidationCode.DatetimeOutOfRange, RecordNotificationCode.DateTimeCollectedError, "dato")),
            new FakeStep("post2", ImportStepType.Post, kjørte)
        ]);

        var utfall = prosessor.Process(Post(), Settings);

        utfall.Status.Should().Be(ImportRecordStatus.Rejected);
        kjørte.Should().Equal(["post1"]);
    }

    /// <summary>
    /// Den parallelle fasen stopper ikke halvveis: alle steg deler observasjonen, og et
    /// steg som avbrytes etterlater en halvskrevet post.
    /// </summary>
    [Fact]
    public void Lar_alle_parallelle_steg_kjore_ferdig_selv_om_ett_avviser()
    {
        var kjørte = new List<string>();
        var prosessor = new ImportRecordProcessor([
            new FakeStep("p1", ImportStepType.Parallel, kjørte, ImportRecordResult.Rejected(
                RecordValidationCode.CoordinateMissing, RecordNotificationCode.MissingLongitudeLatitude, "ingen koordinat")),
            new FakeStep("p2", ImportStepType.Parallel, kjørte),
            new FakeStep("p3", ImportStepType.Parallel, kjørte, ImportRecordResult.Rejected(
                RecordValidationCode.CoordinateOutOfBounds, RecordNotificationCode.Ok, "utenfor")),
            new FakeStep("post", ImportStepType.Post, kjørte)
        ]);

        var utfall = prosessor.Process(Post(), Settings);

        utfall.Status.Should().Be(ImportRecordStatus.Rejected);
        utfall.Errors.Should().HaveCount(2);
        kjørte.Should().BeEquivalentTo(["p1", "p2", "p3"]);
        kjørte.Should().NotContain("post");
    }

    [Theory]
    [InlineData(RecordValidationCode.Ok)]
    [InlineData(RecordValidationCode.Warn)]
    [InlineData(RecordValidationCode.SensitiveSpecies)]
    public void Lar_posten_fortsette_for_de_tre_godtatte_kodene(RecordValidationCode kode)
    {
        var kjørte = new List<string>();
        var resultat = kode switch
        {
            RecordValidationCode.Warn => ImportRecordResult.Warn(RecordNotificationCode.DatasetAdded, "ny"),
            RecordValidationCode.SensitiveSpecies => ImportRecordResult.Sensitive(RecordNotificationCode.RecordShouldBeMasked),
            _ => ImportRecordResult.Ok()
        };

        var prosessor = new ImportRecordProcessor([
            new FakeStep("pre", ImportStepType.Pre, kjørte, resultat),
            new FakeStep("post", ImportStepType.Post, kjørte)
        ]);

        var utfall = prosessor.Process(Post(), Settings);

        utfall.Status.Should().Be(ImportRecordStatus.Imported);
        kjørte.Should().Equal(["pre", "post"]);
    }

    [Fact]
    public void Samler_advarsler_uten_a_avvise()
    {
        var kjørte = new List<string>();
        var prosessor = new ImportRecordProcessor([
            new FakeStep("pre", ImportStepType.Pre, kjørte,
                ImportRecordResult.Warn(RecordNotificationCode.CollectionCodeAdded, "samling opprettet")),
            new FakeStep("post", ImportStepType.Post, kjørte,
                ImportRecordResult.Warn(RecordNotificationCode.DatasetAdded, "datasett opprettet"))
        ]);

        var utfall = prosessor.Process(Post(), Settings);

        utfall.Status.Should().Be(ImportRecordStatus.Imported);
        utfall.Warnings.Should().HaveCount(2);
        utfall.Errors.Should().BeEmpty();
    }

    [Fact]
    public void Tar_med_advarsler_ogsa_naar_posten_avvises()
    {
        var kjørte = new List<string>();
        var prosessor = new ImportRecordProcessor([
            new FakeStep("pre1", ImportStepType.Pre, kjørte,
                ImportRecordResult.Warn(RecordNotificationCode.CollectionCodeAdded, "samling opprettet")),
            new FakeStep("pre2", ImportStepType.Pre, kjørte, ImportRecordResult.Rejected(
                RecordValidationCode.NoTaxonomicMatch, RecordNotificationCode.NotAbleToMatchTaxonomy, "ingen treff"))
        ]);

        var utfall = prosessor.Process(Post(), Settings);

        utfall.Status.Should().Be(ImportRecordStatus.Rejected);
        utfall.Warnings.Should().HaveCount(1);
        utfall.Errors.Should().HaveCount(1);
    }

    /// <summary>
    /// Den halvbygde observasjonen skal følge med en avvisning — Artskart 2 fyller
    /// <c>RejectedRecord</c> fra den.
    /// </summary>
    [Fact]
    public void Gir_den_halvbygde_observasjonen_med_en_avvisning()
    {
        var kjørte = new List<string>();
        var prosessor = new ImportRecordProcessor([
            new FakeStep("pre1", ImportStepType.Pre, kjørte, virkning: (o, _) =>
            {
                o.ProxyId = "uio/o/o-1";
                o.InstitutionCode = "UIO";
            }),
            new FakeStep("pre2", ImportStepType.Pre, kjørte, ImportRecordResult.Rejected(
                RecordValidationCode.CoordinateMissing, RecordNotificationCode.MissingLongitudeLatitude, "ingen koordinat"))
        ]);

        var utfall = prosessor.Process(Post(), Settings);

        utfall.Observation.ProxyId.Should().Be("uio/o/o-1");
        utfall.Observation.InstitutionCode.Should().Be("UIO");
    }

    [Fact]
    public void Slettet_post_kjorer_bare_identitetssteget()
    {
        var kjørte = new List<string>();
        var prosessor = new ImportRecordProcessor([
            new FakeIdentityStep(kjørte),
            new FakeStep("pre", ImportStepType.Pre, kjørte),
            new FakeStep("parallell", ImportStepType.Parallel, kjørte),
            new FakeStep("post", ImportStepType.Post, kjørte)
        ]);

        var utfall = prosessor.Process(Post(isDeleted: true), Settings);

        utfall.Status.Should().Be(ImportRecordStatus.Deleted);
        utfall.Observation.ProxyId.Should().Be("uio/o/o-1");
        kjørte.Should().Equal(["identitet"]);
    }

    [Fact]
    public void Slettet_post_uten_identitet_kan_ikke_slettes()
    {
        var kjørte = new List<string>();
        var prosessor = new ImportRecordProcessor([new FakeIdentityStep(kjørte, proxyId: null)]);

        var utfall = prosessor.Process(Post(isDeleted: true), Settings);

        utfall.Status.Should().Be(ImportRecordStatus.DeletedWithoutIdentity);
    }

    /// <summary>
    /// Nøklene sorteres, så JSON-en kan sammenlignes mellom to kjøringer.
    /// </summary>
    [Fact]
    public void Serialiserer_dynamiske_egenskaper_sortert_paa_nokkel()
    {
        var kjørte = new List<string>();
        var prosessor = new ImportRecordProcessor([
            new FakeStep("pre", ImportStepType.Pre, kjørte, virkning: (_, c) =>
            {
                c.AppendDynamicProperty("zebra", "1");
                c.SaveOriginalValue("Latitude", "59.9");
                c.AppendDynamicProperty("alfa", "2");
            })
        ]);

        var observasjon = prosessor.Process(Post(), Settings).Observation;

        observasjon.DynamicProperties.Should().Be(
            """{"_original_Latitude":"59.9","alfa":"2","zebra":"1"}""");
    }

    [Fact]
    public void Lar_dynamiske_egenskaper_vaere_null_naar_ingen_er_satt()
    {
        var prosessor = new ImportRecordProcessor([]);

        prosessor.Process(Post(), Settings).Observation.DynamicProperties.Should().BeNull();
    }

    [Fact]
    public void Avvist_post_far_ikke_dynamiske_egenskaper_serialisert()
    {
        var kjørte = new List<string>();
        var prosessor = new ImportRecordProcessor([
            new FakeStep("pre", ImportStepType.Pre, kjørte,
                ImportRecordResult.Rejected(RecordValidationCode.Error, RecordNotificationCode.Ok, "feil"),
                (_, c) => c.AppendDynamicProperty("alfa", "2"))
        ]);

        prosessor.Process(Post(), Settings).Observation.DynamicProperties.Should().BeNull();
    }

    [Fact]
    public void Sender_samme_kontekst_til_alle_steg()
    {
        var kjørte = new List<string>();
        ImportContext? fraPre = null;
        ImportContext? fraPost = null;

        var prosessor = new ImportRecordProcessor([
            new FakeStep("pre", ImportStepType.Pre, kjørte, virkning: (_, c) => fraPre = c),
            new FakeStep("post", ImportStepType.Post, kjørte, virkning: (_, c) => fraPost = c)
        ]);

        prosessor.Process(Post(), Settings);

        fraPost.Should().BeSameAs(fraPre);
    }

    [Fact]
    public void Sender_datasettets_sensitivitet_videre_i_konteksten()
    {
        var kjørte = new List<string>();
        var sensitiv = false;

        var prosessor = new ImportRecordProcessor([
            new FakeStep("pre", ImportStepType.Pre, kjørte, virkning: (_, c) => sensitiv = c.IsDatasetSensitive)
        ]);

        prosessor.Process(Post(), new ImportRunSettings
        {
            NodeId = 1011,
            ProcessEngineId = 7,
            ProcessedAt = Settings.ProcessedAt,
            ImportedAt = Settings.ImportedAt,
            IsDatasetSensitive = true
        });

        sensitiv.Should().BeTrue();
    }
}

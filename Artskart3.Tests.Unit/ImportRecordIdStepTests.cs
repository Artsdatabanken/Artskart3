using Artskart3.Core.Application.Import;
using Artskart3.Core.Application.Import.Models;
using Artskart3.Core.Application.Import.Steps;
using FluentAssertions;

namespace Artskart3.Tests.Unit;

public class ImportRecordIdStepTests
{
    /// <summary>
    /// Registeret slik det ser ut i testene: én utgiver «O» med aliaset «OSLO», ett
    /// datasett «SOPP» under den, og ett prosjekt med datasetID «urn:1».
    /// </summary>
    private sealed class FakeRegister : IOrganizationRegistry
    {
        public OrganizationRef Utgiver { get; } = new(10, "O", "Naturhistorisk museum", null);

        public OrganizationRef Datasett { get; } = new(20, "SOPP", "Soppherbariet", 10);

        public OrganizationRef Prosjekt { get; } = new(30, null, "Sopp i Oslo", 20);

        public int Oppslag { get; private set; }

        public OrganizationRef? PublisherByAlias(string alias)
        {
            Oppslag++;
            return string.Equals(alias, "OSLO", StringComparison.OrdinalIgnoreCase) ? Utgiver : null;
        }

        public OrganizationRef? PublisherByCode(string code)
        {
            Oppslag++;
            return string.Equals(code, "O", StringComparison.OrdinalIgnoreCase) ? Utgiver : null;
        }

        public OrganizationRef? DatasetByCode(int publisherOrgId, string code)
        {
            Oppslag++;
            return publisherOrgId == Utgiver.Id && string.Equals(code, "SOPP", StringComparison.OrdinalIgnoreCase)
                ? Datasett
                : null;
        }

        public OrganizationRef? ProjectBySourceId(int datasetOrgId, string sourceDatasetId)
        {
            Oppslag++;
            return datasetOrgId == Datasett.Id && sourceDatasetId == "urn:1" ? Prosjekt : null;
        }

        public OrganizationRef? ProjectByName(int datasetOrgId, string name)
        {
            Oppslag++;
            return datasetOrgId == Datasett.Id && name == "Sopp i Oslo" ? Prosjekt : null;
        }
    }

    private sealed class FakeOppretter : IOrganizationCreator
    {
        private int _nesteId = 1000;

        public List<string> Datasett { get; } = [];

        public List<(string? KildeId, string Navn)> Prosjekter { get; } = [];

        public OrganizationRef CreateDataset(OrganizationRef publisher, string code)
        {
            Datasett.Add(code);
            return new OrganizationRef(_nesteId++, code, $"{code} hos {publisher.Name}", publisher.Id);
        }

        public OrganizationRef CreateProject(OrganizationRef dataset, string? sourceDatasetId, string name)
        {
            Prosjekter.Add((sourceDatasetId, name));
            return new OrganizationRef(_nesteId++, null, name, dataset.Id);
        }
    }

    private readonly FakeRegister _register = new();
    private readonly FakeOppretter _oppretter = new();
    private readonly ImportContext _context = new();

    private ImportRecordIdStep Steg(bool kanOpprette = true) =>
        new(_register, kanOpprette ? _oppretter : null);

    private static CacheRecord Post(
        string? institusjon = "O",
        string? samling = "SOPP",
        string? katalognummer = "12345",
        string? occurrenceId = null,
        string? datasetId = null,
        string? datasetName = null) =>
        new()
        {
            InstitutionCode = institusjon,
            CollectionCode = samling,
            CatalogNumber = katalognummer,
            OccurrenceID = occurrenceId,
            DatasetId = datasetId,
            DatasetName = datasetName
        };

    private (ImportRecordResult Resultat, ImportedObservation Observasjon) Kjør(
        CacheRecord post,
        ImportRecordIdStep? steg = null)
    {
        var observasjon = new ImportedObservation();
        var resultat = (steg ?? Steg()).ValidateAndImport(post, observasjon, _context);

        return (resultat, observasjon);
    }

    [Fact]
    public void Er_et_pre_steg_og_identitetssteget()
    {
        var steg = Steg();

        steg.StepType.Should().Be(ImportStepType.Pre);
        steg.Should().BeAssignableTo<IImportIdentityStep>();
    }

    // --- Avvisninger -------------------------------------------------------

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Avviser_post_uten_katalognummer(string? katalognummer)
    {
        var (resultat, _) = Kjør(Post(katalognummer: katalognummer));

        resultat.ValidationCode.Should().Be(RecordValidationCode.CatalogNumberIsMissing);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Avviser_post_uten_institusjonskode(string? institusjon)
    {
        var (resultat, _) = Kjør(Post(institusjon: institusjon));

        resultat.ValidationCode.Should().Be(RecordValidationCode.InstitutionCodeMissing);
    }

    [Fact]
    public void Avviser_ukjent_institusjonskode()
    {
        var (resultat, _) = Kjør(Post(institusjon: "finnesikke"));

        resultat.ValidationCode.Should().Be(RecordValidationCode.InstitutionCodeNotKnown);
        resultat.ErrorContext.Should().Be("FINNESIKKE", "feilteksten er kildens versalerte kode");
    }

    /// <summary>
    /// En avvist post skal likevel ha identitet: har den stått i indexen før, er dette
    /// nøkkelen som finner raden som skal fjernes.
    /// </summary>
    [Fact]
    public void Setter_identitet_ogsaa_for_en_avvist_post()
    {
        var (_, observasjon) = Kjør(Post(institusjon: "finnesikke", samling: "x"));

        observasjon.ProxyId.Should().Be("finnesikke/x/12345");
        observasjon.InstitutionCode.Should().Be("FINNESIKKE", "kildens kode, siden ingen kanonisk finnes");
        observasjon.InstitutionOrgId.Should().BeNull();
    }

    // --- Utgiveroppslaget --------------------------------------------------

    [Fact]
    public void Slaar_opp_alias_for_kode()
    {
        var (resultat, observasjon) = Kjør(Post(institusjon: "oslo"));

        resultat.IsAccepted.Should().BeTrue();
        observasjon.InstitutionOrgId.Should().Be(_register.Utgiver.Id);
    }

    /// <summary>
    /// Treffer et alias, er det utgiverens egen kode som lagres — ikke den kilden
    /// sendte. Det er derfor et alias ikke bare er en bekvemmelighet: uten det hadde
    /// posten blitt avvist, med det havner den på riktig utgiver under riktig kode.
    /// </summary>
    [Fact]
    public void Lagrer_utgiverens_kanoniske_kode_ikke_kildens()
    {
        var (_, observasjon) = Kjør(Post(institusjon: "oslo"));

        observasjon.InstitutionCode.Should().Be("O");
        observasjon.ProxyId.Should().Be("O/SOPP/12345".ToLowerInvariant());
    }

    [Fact]
    public void Versalerer_kodene_for_oppslag()
    {
        var (resultat, observasjon) = Kjør(Post(institusjon: " o ", samling: " sopp "));

        resultat.IsAccepted.Should().BeTrue();
        observasjon.DatasetOrgId.Should().Be(_register.Datasett.Id);
    }

    /// <summary>
    /// Tom samlingskode blir institusjonskoden. Det er ikke en nødløsning: en
    /// institusjon uten samlinger har én samling som heter det samme som den selv.
    /// </summary>
    [Fact]
    public void Tom_samlingskode_blir_institusjonskoden()
    {
        var (_, observasjon) = Kjør(Post(samling: null));

        _oppretter.Datasett.Should().ContainSingle().Which.Should().Be("o",
            "samlingen opprettes med institusjonskoden, i små bokstaver");
        observasjon.CollectionCode.Should().Be("o");
    }

    // --- Lengdegrenser -----------------------------------------------------

    [Fact]
    public void Kutter_katalognummeret_til_200_tegn()
    {
        var (_, observasjon) = Kjør(Post(katalognummer: new string('a', 250)));

        observasjon.CatalogNumber.Should().HaveLength(200);
    }

    [Fact]
    public void Kutter_occurrenceid_til_255_tegn()
    {
        var (_, observasjon) = Kjør(Post(occurrenceId: "urn:" + new string('b', 300)));

        observasjon.OccurrenceId.Should().HaveLength(255);
    }

    [Fact]
    public void Kutter_institusjonskoden_til_100_tegn()
    {
        var (resultat, _) = Kjør(Post(institusjon: new string('c', 150)));

        resultat.ErrorContext.Should().HaveLength(100);
    }

    // --- Identiteten -------------------------------------------------------

    /// <summary>
    /// En <c>occurrenceID</c> som ikke er et rent løpenummer er identiteten. Da er den
    /// stabil på tvers av kilder, og det er hele poenget med feltet.
    /// </summary>
    [Fact]
    public void Bruker_occurrenceid_som_identitet_naar_den_er_en_ekte_id()
    {
        var (_, observasjon) = Kjør(Post(occurrenceId: "URN:UUID:ABC-123"));

        observasjon.ProxyId.Should().Be("urn:uuid:abc-123");
    }

    [Fact]
    public void Bruker_kodene_som_identitet_naar_occurrenceid_er_et_lopenummer()
    {
        var (_, observasjon) = Kjør(Post(occurrenceId: "9988776655"));

        observasjon.ProxyId.Should().Be("o/sopp/12345");
    }

    // --- Datasett (type 2) -------------------------------------------------

    [Fact]
    public void Kobler_posten_til_datasettet()
    {
        var (resultat, observasjon) = Kjør(Post());

        resultat.ValidationCode.Should().Be(RecordValidationCode.Ok);
        observasjon.DatasetOrgId.Should().Be(_register.Datasett.Id);
        observasjon.CollectionCode.Should().Be("SOPP");
        _oppretter.Datasett.Should().BeEmpty("datasettet finnes allerede");
    }

    [Fact]
    public void Oppretter_datasettet_naar_koden_er_ukjent()
    {
        var (resultat, observasjon) = Kjør(Post(samling: "LAV"));

        resultat.ValidationCode.Should().Be(RecordValidationCode.Ok);
        resultat.NotificationCode.Should().Be(RecordNotificationCode.CollectionCodeAdded);
        _oppretter.Datasett.Should().ContainSingle().Which.Should().Be("lav",
            "Artskart 2 oppretter samlingen i små bokstaver");
        observasjon.DatasetOrgId.Should().Be(1000);
    }

    /// <summary>
    /// En rad steget selv har opprettet står ikke i registerets øyeblikksbilde. Uten
    /// bufferet ville post nummer to med samme ukjente kode opprettet raden på nytt.
    /// </summary>
    [Fact]
    public void Oppretter_ikke_samme_datasett_to_ganger()
    {
        var steg = Steg();

        var første = Kjør(Post(samling: "LAV"), steg);
        var andre = Kjør(Post(samling: "LAV", katalognummer: "999"), steg);

        _oppretter.Datasett.Should().ContainSingle();
        andre.Observasjon.DatasetOrgId.Should().Be(første.Observasjon.DatasetOrgId);
        steg.CreatedCount.Should().Be(1);
    }

    [Fact]
    public void Avviser_ukjent_datasett_i_opprett_ikke_modus()
    {
        var steg = Steg(kanOpprette: false);

        var (resultat, _) = Kjør(Post(samling: "LAV"), steg);

        resultat.ValidationCode.Should().Be(RecordValidationCode.OrganizationNotCreated);
        resultat.ErrorContext.Should().Contain("LAV");
        steg.CanCreate.Should().BeFalse();
    }

    // --- Prosjekter (type 3) -----------------------------------------------

    [Fact]
    public void Kobler_posten_til_prosjektet_pa_datasetid()
    {
        var (resultat, observasjon) = Kjør(Post(datasetId: "urn:1", datasetName: "Sopp i Oslo"));

        resultat.IsAccepted.Should().BeTrue();
        observasjon.ProjectOrgIds.Should().Equal(_register.Prosjekt.Id);
        observasjon.DatasetId.Should().Be("urn:1");
        observasjon.DatasetName.Should().Be("Sopp i Oslo");
        _oppretter.Prosjekter.Should().BeEmpty();
    }

    [Fact]
    public void Kobler_posten_til_prosjektet_pa_navn_naar_datasetid_mangler()
    {
        var (_, observasjon) = Kjør(Post(datasetName: "Sopp i Oslo"));

        observasjon.ProjectOrgIds.Should().Equal(_register.Prosjekt.Id);
        observasjon.DatasetName.Should().Be("Sopp i Oslo");
        observasjon.DatasetId.Should().BeNull();
    }

    /// <summary>
    /// ID-en går før navnet fordi navnet ikke er entydig: 902 par prosjekter deler navn
    /// under samme datasett i dagens data.
    /// </summary>
    [Fact]
    public void Slaar_opp_datasetid_for_navn()
    {
        var (_, observasjon) = Kjør(Post(datasetId: "urn:1", datasetName: "Et helt annet navn"));

        observasjon.ProjectOrgIds.Should().Equal(_register.Prosjekt.Id);
        _oppretter.Prosjekter.Should().BeEmpty("ID-en traff, så navnet spiller ingen rolle");
    }

    [Fact]
    public void Faller_tilbake_pa_navnet_naar_datasetid_ikke_treffer()
    {
        var (_, observasjon) = Kjør(Post(datasetId: "urn:ukjent", datasetName: "Sopp i Oslo"));

        observasjon.ProjectOrgIds.Should().Equal(_register.Prosjekt.Id);
        _oppretter.Prosjekter.Should().BeEmpty();
    }

    [Fact]
    public void Oppretter_prosjektet_naar_verken_id_eller_navn_treffer()
    {
        var (resultat, observasjon) = Kjør(Post(datasetId: "urn:9", datasetName: "Nytt prosjekt"));

        resultat.NotificationCode.Should().Be(RecordNotificationCode.DatasetAdded);
        _oppretter.Prosjekter.Should().ContainSingle().Which.Should().Be(("urn:9", "Nytt prosjekt"));
        observasjon.ProjectOrgIds.Should().Equal(1000);
    }

    [Fact]
    public void Oppretter_prosjektet_uten_kildeid_naar_kilden_bare_gav_navn()
    {
        Kjør(Post(datasetName: "Bare et navn"));

        _oppretter.Prosjekter.Should().ContainSingle().Which.Should().Be((null, "Bare et navn"));
    }

    [Fact]
    public void Kobler_flere_prosjekter()
    {
        var (_, observasjon) = Kjør(Post(
            datasetId: "urn:1|urn:9",
            datasetName: "Sopp i Oslo|Nytt prosjekt"));

        observasjon.ProjectOrgIds.Should().Equal(_register.Prosjekt.Id, 1000);
        observasjon.DatasetId.Should().Be("urn:1|urn:9");
    }

    /// <summary>
    /// Stemmer ikke antallet, vet vi ikke hvilket navn som hører til hvilken ID — og da
    /// avvises posten i stedet for at importen gjetter.
    /// </summary>
    [Fact]
    public void Avviser_naar_antall_datasetid_og_navn_ikke_stemmer()
    {
        var (resultat, _) = Kjør(Post(datasetId: "urn:1|urn:9", datasetName: "Sopp i Oslo"));

        resultat.ValidationCode.Should().Be(RecordValidationCode.DatasetNameMissing);
        resultat.NotificationCode.Should().Be(RecordNotificationCode.CountOfDatasetIdsAndNamesDiffers);
        resultat.ErrorContext.Should().Contain("urn:1|urn:9").And.Contain("Sopp i Oslo");
    }

    [Fact]
    public void Kaster_bort_tomme_ledd_og_trimmer()
    {
        var (_, observasjon) = Kjør(Post(datasetId: " urn:1 ||", datasetName: " Sopp i Oslo |"));

        observasjon.DatasetId.Should().Be("urn:1");
        observasjon.DatasetName.Should().Be("Sopp i Oslo");
        observasjon.ProjectOrgIds.Should().Equal(_register.Prosjekt.Id);
    }

    /// <summary>
    /// Artskart 2 skrev <c>Datasets = [0]</c> for en post uten prosjekt. Her står lista
    /// tom: en rad i <c>ObservationProject</c> som peker på organisasjon 0 ville vært en
    /// ødelagt fremmednøkkel.
    /// </summary>
    [Fact]
    public void Gir_ingen_prosjekter_naar_kilden_ikke_oppgir_noe()
    {
        var (resultat, observasjon) = Kjør(Post());

        resultat.ValidationCode.Should().Be(RecordValidationCode.Ok);
        observasjon.ProjectOrgIds.Should().BeEmpty();
        observasjon.DatasetId.Should().BeNull();
        observasjon.DatasetName.Should().BeNull();
    }

    [Fact]
    public void Oppretter_ikke_samme_prosjekt_to_ganger()
    {
        var steg = Steg();

        Kjør(Post(datasetId: "urn:9", datasetName: "Nytt prosjekt"), steg);
        var andre = Kjør(Post(datasetId: "urn:9", datasetName: "Nytt prosjekt", katalognummer: "999"), steg);

        _oppretter.Prosjekter.Should().ContainSingle();
        andre.Observasjon.ProjectOrgIds.Should().Equal(1000);
    }

    [Fact]
    public void Avviser_ukjent_prosjekt_i_opprett_ikke_modus()
    {
        var (resultat, _) = Kjør(
            Post(datasetId: "urn:9", datasetName: "Nytt prosjekt"),
            Steg(kanOpprette: false));

        resultat.ValidationCode.Should().Be(RecordValidationCode.OrganizationNotCreated);
        resultat.ErrorContext.Should().Contain("urn:9");
    }
}

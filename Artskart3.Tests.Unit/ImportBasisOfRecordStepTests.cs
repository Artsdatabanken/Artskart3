using Artskart3.Core.Application.Import;
using Artskart3.Core.Application.Import.Models;
using Artskart3.Core.Application.Import.Steps;
using Artskart3.Core.Domain.Enums;
using FluentAssertions;

namespace Artskart3.Tests.Unit;

public class ImportBasisOfRecordStepTests
{
    /// <summary>
    /// Et utsnitt av de 14 radene, med de variantene som trengs i testene.
    /// </summary>
    private sealed class FakeRegister : IReferenceValueRegistry
    {
        private static readonly Dictionary<string, int> Navn = new(StringComparer.Ordinal)
        {
            ["unrecognised"] = -1,
            ["objekt"] = 1,
            ["observasjon"] = 2,
            ["humanobservation"] = 14
        };

        private static readonly Dictionary<string, int> Varianter = new(StringComparer.Ordinal)
        {
            ["preservedspecimen"] = 1,
            ["specimen"] = 1,
            [" dead"] = 1,
            ["observation"] = 2,
            ["human_observation"] = 14
        };

        public ReferenceValue? ByName(ReferenceValueKind kind, string name) =>
            Navn.TryGetValue(name, out var id) ? new ReferenceValue(id, name) : null;

        public ReferenceValue? ByVariant(ReferenceValueKind kind, string variant) =>
            Varianter.TryGetValue(variant, out var id)
                ? new ReferenceValue(id, Navn.First(x => x.Value == id).Key)
                : null;

        public ReferenceValue? Unrecognised(ReferenceValueKind kind) => new(-1, "unrecognised");
    }

    private sealed class FakeVariantskriver : IReferenceVariantWriter
    {
        public List<(ReferenceValueKind Type, string Verdi)> Skrevet { get; } = [];

        public void RecordUnrecognisedValue(ReferenceValueKind kind, string value) =>
            Skrevet.Add((kind, value));
    }

    private const int Ugjenkjent = -1;
    private const int Objekt = 1;
    private const int Observasjon = 2;

    private readonly FakeVariantskriver _skriver = new();
    private readonly ImportContext _context = new();

    private ImportBasisOfRecordStep Steg(bool kanSkrive = true) =>
        new(new FakeRegister(), kanSkrive ? _skriver : null);

    private (ImportRecordResult Resultat, ImportedObservation Observasjon, CacheRecord Post) Kjør(
        string? funntype,
        string? notater = null,
        ImportBasisOfRecordStep? steg = null)
    {
        var post = new CacheRecord { BasisOfRecord = funntype, Notes = notater };
        var observasjon = new ImportedObservation();

        var resultat = (steg ?? Steg()).ValidateAndImport(post, observasjon, _context);

        return (resultat, observasjon, post);
    }

    [Fact]
    public void Er_et_pre_steg()
    {
        Steg().StepType.Should().Be(ImportStepType.Pre);
    }

    // --- Treff ------------------------------------------------------------

    [Fact]
    public void Setter_funntypen_pa_eksakt_navn()
    {
        var (resultat, observasjon, _) = Kjør("humanobservation");

        resultat.ValidationCode.Should().Be(RecordValidationCode.Ok);
        observasjon.BasisOfRecordId.Should().Be(14);
        observasjon.HasErrors.Should().BeFalse();
    }

    [Fact]
    public void Setter_funntypen_pa_en_variant()
    {
        var (resultat, observasjon, _) = Kjør("PreservedSpecimen");

        resultat.ValidationCode.Should().Be(RecordValidationCode.Ok);
        observasjon.BasisOfRecordId.Should().Be(Objekt);
    }

    [Fact]
    public void Trimmer_og_senker_verdien_for_oppslag()
    {
        var (_, observasjon, _) = Kjør("  OBSERVATION  ");

        observasjon.BasisOfRecordId.Should().Be(Observasjon);
    }

    [Fact]
    public void Et_treff_skriver_ingen_varianter_og_ingen_originalverdi()
    {
        Kjør("observation");

        _skriver.Skrevet.Should().BeEmpty();
        _context.DynamicProperties.Should().BeEmpty();
    }

    // --- Ikke gjenkjent ---------------------------------------------------

    /// <summary>
    /// En ukjent verdi avviser ikke posten. Artskart 2 har avvisningene i koden, men
    /// utkommentert; det som faktisk skjer er et kvalitetsflagg.
    /// </summary>
    [Fact]
    public void Merker_posten_men_avviser_ikke_en_ukjent_verdi()
    {
        var (resultat, observasjon, post) = Kjør("tullball");

        resultat.ValidationCode.Should().Be(RecordValidationCode.Warn);
        resultat.IsAccepted.Should().BeTrue();
        observasjon.BasisOfRecordId.Should().Be(Ugjenkjent);
        observasjon.HasErrors.Should().BeTrue();
        observasjon.HasTag(Tag.QualityIssue).Should().BeTrue();
        observasjon.Errors.Should().ContainSingle()
            .Which.ValidationCode.Should().Be(RecordValidationCode.BasisOfRecordValueUnknown);
        post.Notes.Should().Contain("BasisOfRecord ikke gjenkjent tullball");
    }

    [Fact]
    public void Tar_vare_pa_kildens_verdi()
    {
        Kjør("tullball");

        _context.DynamicProperties.Should().ContainKey(ImportContext.OriginalValuePrefix + "BasisOfRecord")
            .WhoseValue.Should().Be("tullball");
    }

    [Fact]
    public void Forer_den_ukjente_verdien_opp_som_variant()
    {
        Kjør("Tullball");

        _skriver.Skrevet.Should().ContainSingle()
            .Which.Should().Be((ReferenceValueKind.BasisOfRecord, "tullball"));
    }

    /// <summary>
    /// Feilteksten er kildens verdi med bokstavstørrelsen intakt, ikke den
    /// normaliserte. Mellomrom er alt borte: <c>CacheRecord.BasisOfRecord</c> trimmer
    /// ved setting, slik <c>DarwinCoreProxy</c> gjør.
    /// </summary>
    [Fact]
    public void Feilteksten_er_kildens_egen_verdi()
    {
        var (_, observasjon, _) = Kjør("  TullBall  ");

        observasjon.Errors.Should().ContainSingle().Which.ErrorContext.Should().Be("TullBall");
    }

    // --- Verdien mangler --------------------------------------------------

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Merker_posten_naar_funntypen_mangler(string? funntype)
    {
        var (resultat, observasjon, post) = Kjør(funntype);

        resultat.ValidationCode.Should().Be(RecordValidationCode.Warn);
        observasjon.BasisOfRecordId.Should().Be(Ugjenkjent);
        observasjon.HasTag(Tag.QualityIssue).Should().BeTrue();
        post.Notes.Should().Contain("Mangler BasisOfRecord");
    }

    [Fact]
    public void Lagrer_ingen_originalverdi_naar_det_ikke_var_noen()
    {
        Kjør(null);

        _context.DynamicProperties.Should().BeEmpty();
    }

    /// <summary>
    /// Artskart 2 fører den tomme strengen opp som variant, og sporet står i dataene:
    /// rad -1 har variantlisten «unknown,». Her gjør vi det ikke.
    /// </summary>
    [Fact]
    public void Forer_ikke_tom_verdi_opp_som_variant()
    {
        Kjør("   ");

        _skriver.Skrevet.Should().BeEmpty();
    }

    // --- Den magiske verdien ----------------------------------------------

    /// <summary>
    /// Artskart 2 sammenligner med «basisofrecords/unrecognised», altså
    /// RavenDB-id-en med prefiks, som ingen kilde sender — så verdien «unrecognised»
    /// treffer i stedet det eksakte navneoppslaget og gir Ok uten kvalitetsflagg. Her
    /// er vakten på verdien, slik hensikten var.
    /// </summary>
    [Fact]
    public void Behandler_unrecognised_fra_kilden_som_ukjent()
    {
        var (resultat, observasjon, _) = Kjør("unrecognised");

        resultat.ValidationCode.Should().Be(RecordValidationCode.Warn);
        observasjon.BasisOfRecordId.Should().Be(Ugjenkjent);
        observasjon.HasTag(Tag.QualityIssue).Should().BeTrue();
    }

    // --- Varianter med skilletegn -----------------------------------------

    /// <summary>
    /// «specimen, dead» står som én variant i RavenDB, men kolonnen er kommaseparert,
    /// så den leses som «specimen» og « dead». Det utrimmede leddet kan ikke treffe —
    /// og skal ikke kunne: trimmet ville det begynt å gjenkjenne poster Artskart 2
    /// aldri har gjenkjent.
    /// </summary>
    [Fact]
    public void Et_oppdelt_variantledd_treffer_ikke()
    {
        var (resultat, observasjon, _) = Kjør("dead");

        resultat.ValidationCode.Should().Be(RecordValidationCode.Warn);
        observasjon.BasisOfRecordId.Should().Be(Ugjenkjent);
    }

    // --- Uten variantskriver ----------------------------------------------

    /// <summary>
    /// Variantlisten er bokføring. Slås skrivingen av, får posten akkurat samme utfall.
    /// </summary>
    [Fact]
    public void Gir_samme_utfall_uten_variantskriver()
    {
        var (resultat, observasjon, post) = Kjør("tullball", steg: Steg(kanSkrive: false));

        resultat.ValidationCode.Should().Be(RecordValidationCode.Warn);
        observasjon.BasisOfRecordId.Should().Be(Ugjenkjent);
        observasjon.HasTag(Tag.QualityIssue).Should().BeTrue();
        post.Notes.Should().Contain("ikke gjenkjent");
        _skriver.Skrevet.Should().BeEmpty();
    }

    // --- Notater ----------------------------------------------------------

    /// <summary>
    /// Meldingen føyes på <c>record.Notes</c>, som <c>ImportSimplePropertiesStep</c>
    /// kopierer til observasjonen senere.
    /// </summary>
    [Fact]
    public void Foyer_meldingen_pa_eksisterende_notater()
    {
        var (_, _, post) = Kjør("tullball", notater: "Fra kilden.");

        post.Notes.Should().StartWith("Fra kilden.").And.Contain("ikke gjenkjent");
    }
}

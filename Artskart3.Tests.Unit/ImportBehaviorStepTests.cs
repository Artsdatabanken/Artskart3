using Artskart3.Core.Application.Import;
using Artskart3.Core.Application.Import.Models;
using Artskart3.Core.Application.Import.Steps;
using Artskart3.Core.Domain.Enums;
using FluentAssertions;

namespace Artskart3.Tests.Unit;

public class ImportBehaviorStepTests
{
    /// <summary>
    /// Atferdstabellen slik den står i basen: 6 rader, ingen «unrecognised».
    /// </summary>
    private sealed class FakeRegister : IReferenceValueRegistry
    {
        private static readonly Dictionary<string, int> Navn = new(StringComparer.Ordinal)
        {
            ["moving"] = 1,
            ["feeding"] = 2,
            ["dead"] = 3,
            ["stationary"] = 4,
            ["reproductive"] = 6,
            ["possiblereproductive"] = 7
        };

        private static readonly Dictionary<string, int> Varianter = new(StringComparer.Ordinal)
        {
            ["trafikkdrept"] = 3,
            ["funnet dod"] = 3,
            ["hvilende"] = 4,
            ["soling"] = 4,
            ["nest"] = 6
        };

        public ReferenceValue? ByName(ReferenceValueKind kind, string name) =>
            Navn.TryGetValue(name, out var id) ? new ReferenceValue(id, name) : null;

        public ReferenceValue? ByVariant(ReferenceValueKind kind, string variant) =>
            Varianter.TryGetValue(variant, out var id)
                ? new ReferenceValue(id, Navn.First(x => x.Value == id).Key)
                : null;

        /// <summary>Tabellen har ingen slik rad, og trenger den ikke.</summary>
        public ReferenceValue? Unrecognised(ReferenceValueKind kind) => null;
    }

    private sealed class FakeVariantskriver : IReferenceVariantWriter
    {
        public List<(ReferenceValueKind Type, string Verdi)> Skrevet { get; } = [];

        public void RecordUnrecognisedValue(ReferenceValueKind kind, string value) =>
            Skrevet.Add((kind, value));
    }

    private const byte Moving = 1;
    private const byte Feeding = 2;
    private const byte Dead = 3;
    private const byte Stationary = 4;

    private readonly FakeVariantskriver _skriver = new();
    private readonly ImportContext _context = new();

    private ImportBehaviorStep Steg(bool kanSkrive = true) =>
        new(new FakeRegister(), kanSkrive ? _skriver : null);

    private (ImportRecordResult Resultat, ImportedObservation Observasjon, CacheRecord Post) Kjør(
        string? atferd,
        string? notater = null,
        ImportBehaviorStep? steg = null)
    {
        var post = new CacheRecord { Behavior = atferd, Notes = notater };
        var observasjon = new ImportedObservation();

        var resultat = (steg ?? Steg()).ValidateAndImport(post, observasjon, _context);

        return (resultat, observasjon, post);
    }

    [Fact]
    public void Er_et_pre_steg()
    {
        Steg().StepType.Should().Be(ImportStepType.Pre);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Gjor_ingenting_naar_feltet_er_tomt(string? atferd)
    {
        var (resultat, observasjon, _) = Kjør(atferd);

        resultat.ValidationCode.Should().Be(RecordValidationCode.Ok);
        observasjon.BehaviorIds.Should().BeEmpty();
        observasjon.HasErrors.Should().BeFalse();
    }

    // --- Treff ------------------------------------------------------------

    [Fact]
    public void Setter_atferden_pa_eksakt_navn()
    {
        var (resultat, observasjon, _) = Kjør("Moving");

        resultat.ValidationCode.Should().Be(RecordValidationCode.Ok);
        observasjon.BehaviorIds.Should().Equal(Moving);
    }

    [Fact]
    public void Setter_atferden_pa_en_variant()
    {
        var (_, observasjon, _) = Kjør("trafikkdrept");

        observasjon.BehaviorIds.Should().Equal(Dead);
    }

    [Theory]
    [InlineData("moving,feeding")]
    [InlineData("moving;feeding")]
    [InlineData("moving|feeding")]
    public void Tar_flere_atferder_skilt_med_komma_semikolon_eller_pipe(string atferd)
    {
        var (resultat, observasjon, _) = Kjør(atferd);

        resultat.ValidationCode.Should().Be(RecordValidationCode.Ok);
        observasjon.BehaviorIds.Should().Equal(Moving, Feeding);
    }

    [Fact]
    public void Gir_en_rad_selv_om_samme_atferd_star_to_ganger()
    {
        var (_, observasjon, _) = Kjør("moving,moving");

        observasjon.BehaviorIds.Should().Equal(Moving);
    }

    /// <summary>
    /// Både navnet og en variant kan peke på samme rad. Da skal den også bare bli én.
    /// </summary>
    [Fact]
    public void Gir_en_rad_naar_navn_og_variant_peker_samme_vei()
    {
        var (_, observasjon, _) = Kjør("dead,trafikkdrept");

        observasjon.BehaviorIds.Should().Equal(Dead);
    }

    /// <summary>
    /// Mellomrom fjernes før oppslag. Varianter som inneholder mellomrom kan derfor
    /// aldri treffe — «funnet dod» på <c>dead</c> er en død oppføring i dagens løsning
    /// også.
    /// </summary>
    [Fact]
    public void En_variant_med_mellomrom_kan_ikke_treffe()
    {
        var (resultat, observasjon, _) = Kjør("funnet dod");

        resultat.ValidationCode.Should().Be(RecordValidationCode.Warn);
        observasjon.BehaviorIds.Should().BeEmpty();
    }

    [Fact]
    public void Fjerner_mellomrom_ogsaa_mellom_leddene()
    {
        var (_, observasjon, _) = Kjør("moving, feeding");

        observasjon.BehaviorIds.Should().Equal(Moving, Feeding);
    }

    // --- Ikke gjenkjent ---------------------------------------------------

    [Fact]
    public void Merker_posten_men_avviser_ikke_en_ukjent_verdi()
    {
        var (resultat, observasjon, post) = Kjør("tullball");

        resultat.ValidationCode.Should().Be(RecordValidationCode.Warn);
        resultat.IsAccepted.Should().BeTrue();
        observasjon.BehaviorIds.Should().BeEmpty("Behavior har ingen unrecognised-rad");
        observasjon.HasErrors.Should().BeTrue();
        observasjon.HasTag(Tag.QualityIssue).Should().BeTrue();
        observasjon.Errors.Should().ContainSingle()
            .Which.ValidationCode.Should().Be(RecordValidationCode.BehaviorValueUnknown);
        post.Notes.Should().Contain("Behavior ikke gjenkjent 'tullball'");
    }

    /// <summary>
    /// De verdiene som lar seg gjenkjenne beholdes, selv om en annen i samme felt ikke
    /// gjør det.
    /// </summary>
    [Fact]
    public void Beholder_de_atferdene_som_treffer()
    {
        var (resultat, observasjon, _) = Kjør("moving,tullball,hvilende");

        resultat.ValidationCode.Should().Be(RecordValidationCode.Warn);
        observasjon.BehaviorIds.Should().Equal(Moving, Stationary);
    }

    [Fact]
    public void Gir_en_feil_per_ukjent_verdi()
    {
        var (_, observasjon, _) = Kjør("tullball,sprøyt");

        observasjon.Errors.Should().HaveCount(2);
    }

    [Fact]
    public void Tar_vare_pa_kildens_verdi()
    {
        Kjør("moving,tullball");

        _context.DynamicProperties.Should().ContainKey(ImportContext.OriginalValuePrefix + "Behavior")
            .WhoseValue.Should().Be("moving,tullball");
    }

    [Fact]
    public void Forer_den_ukjente_verdien_opp_som_variant()
    {
        Kjør("Tullball");

        _skriver.Skrevet.Should().ContainSingle()
            .Which.Should().Be((ReferenceValueKind.Behavior, "tullball"));
    }

    [Fact]
    public void Behandler_unrecognised_fra_kilden_som_ukjent()
    {
        var (resultat, observasjon, _) = Kjør("unrecognised");

        resultat.ValidationCode.Should().Be(RecordValidationCode.Warn);
        observasjon.BehaviorIds.Should().BeEmpty();
        _skriver.Skrevet.Should().BeEmpty("den magiske verdien er ikke verdt å bokføre");
    }

    [Fact]
    public void Gir_samme_utfall_uten_variantskriver()
    {
        var (resultat, observasjon, _) = Kjør("tullball", steg: Steg(kanSkrive: false));

        resultat.ValidationCode.Should().Be(RecordValidationCode.Warn);
        observasjon.HasTag(Tag.QualityIssue).Should().BeTrue();
        _skriver.Skrevet.Should().BeEmpty();
    }

    // --- mdir-tilfellet ---------------------------------------------------

    /// <summary>
    /// Noen kilder sender hele feltnavnet med verdien. Artskart 2 kaller det «mdir
    /// special case», og tar leddet etter prefikset.
    /// </summary>
    [Fact]
    public void Leser_verdien_etter_feltprefikset()
    {
        var (resultat, observasjon, post) = Kjør("noe,behavior:feeding");

        resultat.ValidationCode.Should().Be(RecordValidationCode.Ok);
        observasjon.BehaviorIds.Should().Equal(Feeding);
        post.Notes.Should().Contain("Behavior: 'noe,behavior:feeding'.");
    }

    /// <summary>
    /// Artskart 2 tar <c>[1]</c> av oppdelingen, så et felt som <em>begynner</em> med
    /// <c>behavior:</c> gir en <c>IndexOutOfRangeException</c> og feller hele
    /// nodeimporten. Her leses verdien.
    /// </summary>
    [Fact]
    public void Taaler_at_feltprefikset_star_forst()
    {
        var (resultat, observasjon, _) = Kjør("behavior:feeding");

        resultat.ValidationCode.Should().Be(RecordValidationCode.Ok);
        observasjon.BehaviorIds.Should().Equal(Feeding);
    }

    /// <summary>
    /// Artskart 2 leser <c>record.Notes.Contains(...)</c> uten null-sjekk, og
    /// <c>Notes</c> er null for de fleste poster. Samme krasj, samme node.
    /// </summary>
    [Fact]
    public void Taaler_at_notatfeltet_er_tomt_i_mdir_tilfellet()
    {
        var (resultat, _, post) = Kjør("noe,behavior:feeding", notater: null);

        resultat.IsAccepted.Should().BeTrue();
        post.Notes.Should().Contain("Behavior:");
    }

    [Fact]
    public void Gjentar_ikke_meldingen_naar_den_alt_star_i_notatene()
    {
        var (_, _, post) = Kjør("noe,behavior:feeding", notater: "Behavior: 'noe,behavior:feeding'.");

        post.Notes.Should().Be("Behavior: 'noe,behavior:feeding'.");
    }
}

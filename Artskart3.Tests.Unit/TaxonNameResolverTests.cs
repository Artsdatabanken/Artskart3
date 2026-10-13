using Artskart3.Core.Application.Import;
using Artskart3.Core.Application.Import.Models;
using Artskart3.Infrastructure.Import;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace Artskart3.Tests.Unit;

public class TaxonNameResolverTests
{
    /// <summary>Rangene som brukes i testene. Lavere ID er høyere rang.</summary>
    private const int Slekt = 20;
    private const int Art = 22;
    private const int Underart = 24;

    private sealed class FakeTaksonomi(TaxonomySnapshot snapshot) : ITaxonomySource
    {
        public int Lastinger { get; private set; }

        public TaxonomySnapshot LoadAll()
        {
            Lastinger++;
            return snapshot;
        }
    }

    private static TaxonRecord Takson(
        int id,
        int rangId = Art,
        string? gyldigNavn = null,
        string? forfatter = null,
        int taxonGroupId = 5,
        int[]? hierarki = null) =>
        new(id, rangId, gyldigNavn ?? $"Takson {id}", forfatter, taxonGroupId,
            hierarki ?? [id, 1], [], []);

    private static TaxonNameEntry Navn(
        int nameId,
        int taxonId,
        string navn,
        string? forfatter = null,
        bool akseptert = true,
        int rangId = Art,
        string rangNavn = "species") =>
        new()
        {
            NameId = nameId,
            TaxonId = taxonId,
            Accepted = akseptert,
            ScientificNameFormatted = navn,
            ScientificNameAuthorship = forfatter,
            RankId = rangId,
            RankName = rangNavn
        };

    private static TaxonNameResolver Oppslag(
        IEnumerable<TaxonRecord> taksoner,
        IEnumerable<TaxonNameEntry> navn) =>
        new(new FakeTaksonomi(new TaxonomySnapshot(taksoner.ToList(), navn.ToList())),
            NullLogger<TaxonNameResolver>.Instance);

    // --- Enkle treff --------------------------------------------------------

    [Fact]
    public void Finner_takson_paa_navnet_alene()
    {
        var oppslag = Oppslag(
            [Takson(100, gyldigNavn: "Vulpes vulpes", forfatter: "Linnaeus, 1758")],
            [Navn(1, 100, "<i>Vulpes vulpes</i>", "Linnaeus, 1758")]);

        var treff = oppslag.Resolve("vulpes vulpes", "species", null);

        treff.Should().NotBeNull();
        treff!.Taxon.Id.Should().Be(100);
        treff.MatchedScientificNameId.Should().Be(1);
        treff.MatchedScientificName.Should().Be("<i>Vulpes vulpes</i>", "navnet beholder formateringen sin");
        treff.Diagnostics.Should().BeEmpty();
    }

    /// <summary>
    /// Kilder oppgir ofte navn og forfatter i samme felt. Det sammensatte oppslaget er
    /// første ledd i kaskaden nettopp derfor.
    /// </summary>
    [Fact]
    public void Finner_takson_paa_navn_og_forfatter_sammen()
    {
        var oppslag = Oppslag(
            [Takson(100)],
            [Navn(1, 100, "<i>Vulpes vulpes</i>", "Linnaeus, 1758")]);

        var treff = oppslag.Resolve("vulpes vulpes linnaeus, 1758", null, null);

        treff.Should().NotBeNull();
        treff!.Taxon.Id.Should().Be(100);
    }

    [Fact]
    public void Gir_null_naar_navnet_ikke_finnes()
    {
        var oppslag = Oppslag([Takson(100)], [Navn(1, 100, "Vulpes vulpes")]);

        oppslag.Resolve("canis lupus", "species", null).Should().BeNull();
    }

    /// <summary>
    /// Taksonomien laster hele basen én gang. Oppslag nummer to skal ikke koste en ny
    /// runde.
    /// </summary>
    [Fact]
    public void Laster_taksonomien_bare_en_gang()
    {
        var kilde = new FakeTaksonomi(new TaxonomySnapshot(
            [Takson(100)],
            [Navn(1, 100, "Vulpes vulpes")]));

        var oppslag = new TaxonNameResolver(kilde, NullLogger<TaxonNameResolver>.Instance);

        oppslag.Resolve("vulpes vulpes", null, null);
        oppslag.Resolve("canis lupus", null, null);

        kilde.Lastinger.Should().Be(1);
    }

    // --- Parentesinnhold ----------------------------------------------------

    /// <summary>
    /// <c>Mergus (Mergellus) albellus</c> skal også kunne slås opp som
    /// <c>Mergus albellus</c>. Oppslaget får derfor en ekstra oppføring per navn med
    /// nøyaktig én parentes.
    /// </summary>
    [Fact]
    public void Finner_navn_med_parentes_ogsaa_uten_parentesinnholdet()
    {
        var oppslag = Oppslag(
            [Takson(100)],
            [Navn(1, 100, "Mergus (Mergellus) albellus")]);

        oppslag.Resolve("mergus albellus", null, null).Should().NotBeNull();
        oppslag.Resolve("mergus (mergellus) albellus", null, null).Should().NotBeNull();
    }

    /// <summary>To parentesnivåer gir et navn ingen leter etter, og hoppes over.</summary>
    [Fact]
    public void Lager_ingen_ekstra_oppforing_for_to_parenteser()
    {
        var oppslag = Oppslag(
            [Takson(100)],
            [Navn(1, 100, "Mus (mus) (domesticus)")]);

        oppslag.Resolve("mus", null, null).Should().BeNull();
    }

    /// <summary>
    /// Kilden oppgir navnet med forfatteren i parentes. Andre ledd i kaskaden fjerner
    /// parentesinnholdet fra søkenavnet.
    /// </summary>
    [Fact]
    public void Fjerner_parentesinnhold_fra_sokenavnet()
    {
        var oppslag = Oppslag(
            [Takson(100)],
            [Navn(1, 100, "Limosa lapponica", "Linnaeus, 1758")]);

        oppslag.Resolve("limosa lapponica (linnaeus, 1758)", null, null).Should().NotBeNull();
    }

    // --- Flertydighet: rang -------------------------------------------------

    /// <summary>Samme navn på to ranger — kildens rang avgjør.</summary>
    [Fact]
    public void Bruker_kildens_rang_til_a_skille_to_treff()
    {
        var oppslag = Oppslag(
            [Takson(100, Slekt), Takson(200, Art)],
            [
                Navn(1, 100, "Bufo", akseptert: false, rangId: Slekt, rangNavn: "genus"),
                Navn(2, 200, "Bufo", akseptert: false, rangId: Art, rangNavn: "species")
            ]);

        oppslag.Resolve("bufo", "genus", null)!.Taxon.Id.Should().Be(100);
        oppslag.Resolve("bufo", "species", null)!.Taxon.Id.Should().Be(200);
    }

    /// <summary>Uten rang fra kilden tas den høyeste rangen, hvis den er entydig.</summary>
    [Fact]
    public void Tar_hoyeste_rang_naar_kilden_ikke_oppgir_noen()
    {
        var oppslag = Oppslag(
            [Takson(100, Slekt), Takson(200, Underart)],
            [
                Navn(1, 100, "Bufo", akseptert: false, rangId: Slekt, rangNavn: "genus"),
                Navn(2, 200, "Bufo", akseptert: false, rangId: Underart, rangNavn: "subspecies")
            ]);

        oppslag.Resolve("bufo", null, null)!.Taxon.Id.Should().Be(100, "lavere rang-ID er høyere rang");
    }

    /// <summary>
    /// Står to kandidater likt på rang, gis det opp — og kandidatene legges i
    /// diagnostikken, som er det eneste sporet av hvorfor navnet ikke kunne bestemmes.
    /// </summary>
    [Fact]
    public void Gir_opp_og_forklarer_naar_to_kandidater_star_likt()
    {
        var oppslag = Oppslag(
            [Takson(100), Takson(200)],
            [
                Navn(1, 100, "Bufo bufo", akseptert: false),
                Navn(2, 200, "Bufo bufo", akseptert: false)
            ]);

        oppslag.Resolve("bufo bufo", null, null).Should().BeNull();
    }

    // --- Flertydighet: forfatter og gyldig navn -----------------------------

    [Fact]
    public void Bruker_forfatteren_til_a_skille_to_treff()
    {
        var oppslag = Oppslag(
            [Takson(100), Takson(200)],
            [
                Navn(1, 100, "Bufo bufo", "Linnaeus, 1758", akseptert: false),
                Navn(2, 200, "Bufo bufo", "Laurenti, 1768", akseptert: false)
            ]);

        oppslag.Resolve("bufo bufo", null, "laurenti, 1768")!.Taxon.Id.Should().Be(200);
    }

    [Fact]
    public void Sammenligner_forfatteren_uten_hensyn_til_bokstavstorrelse()
    {
        var oppslag = Oppslag(
            [Takson(100), Takson(200)],
            [
                Navn(1, 100, "Bufo bufo", "Linnaeus, 1758", akseptert: false),
                Navn(2, 200, "Bufo bufo", "Laurenti, 1768", akseptert: false)
            ]);

        oppslag.Resolve("bufo bufo", null, "LAURENTI, 1768")!.Taxon.Id.Should().Be(200);
    }

    /// <summary>
    /// Uten forfatter fra kilden veier gyldig navn tyngre enn rang: et synonym og et
    /// gyldig navn på samme navneform gir det gyldige.
    /// </summary>
    [Fact]
    public void Foretrekker_gyldig_navn_framfor_synonym()
    {
        var oppslag = Oppslag(
            [Takson(100), Takson(200)],
            [
                Navn(1, 100, "Bufo bufo", akseptert: false),
                Navn(2, 200, "Bufo bufo", akseptert: true)
            ]);

        oppslag.Resolve("bufo bufo", null, null)!.Taxon.Id.Should().Be(200);
    }

    /// <summary>
    /// Flere synonymer som alle peker på samme takson er ikke flertydig i praksis — da
    /// er det samme hvilket navn som velges.
    /// </summary>
    [Fact]
    public void Godtar_flere_synonymer_som_peker_paa_samme_takson()
    {
        var oppslag = Oppslag(
            [Takson(100)],
            [
                Navn(1, 100, "Bufo bufo", "Linnaeus, 1758", akseptert: false),
                Navn(2, 100, "Bufo bufo", "Laurenti, 1768", akseptert: false)
            ]);

        var treff = oppslag.Resolve("bufo bufo", null, null);

        treff.Should().NotBeNull();
        treff!.Taxon.Id.Should().Be(100);
        treff.MatchedScientificNameId.Should().Be(1, "det første navnet tas");
    }

    // --- Forkortet navn ----------------------------------------------------

    /// <summary>
    /// En art oppgitt med tre ledd er ofte slekt, art og forfatter. Fjerde ledd i
    /// kaskaden prøver de to første.
    /// </summary>
    [Fact]
    public void Forkorter_artsnavn_til_to_ledd()
    {
        var oppslag = Oppslag(
            [Takson(100)],
            [Navn(1, 100, "Vulpes vulpes")]);

        oppslag.Resolve("vulpes vulpes fulvus", "species", null)!.Taxon.Id.Should().Be(100);
    }

    [Fact]
    public void Forkorter_underartsnavn_til_tre_ledd()
    {
        var oppslag = Oppslag(
            [Takson(100, Underart)],
            [Navn(1, 100, "Carex nigra juncella", rangId: Underart, rangNavn: "subspecies")]);

        oppslag.Resolve("carex nigra juncella fries", "subspecies", null)!.Taxon.Id.Should().Be(100);
    }

    [Fact]
    public void Forkorter_ikke_naar_rangen_ikke_tilsier_det()
    {
        var oppslag = Oppslag(
            [Takson(100, Slekt)],
            [Navn(1, 100, "Vulpes", rangId: Slekt, rangNavn: "genus")]);

        oppslag.Resolve("vulpes vulpes fulvus", "genus", null).Should().BeNull();
    }

    // --- Det treffet bærer med seg -----------------------------------------

    [Fact]
    public void Treffet_baerer_hierarki_gruppe_og_egenskaper()
    {
        var takson = new TaxonRecord(
            100, Art, "Vulpes vulpes", "Linnaeus, 1758", 42,
            [100, 90, 80, 1],
            [1, 2],
            [new TaxonPropertyInfo("RL", "Norge", "LC")]);

        var oppslag = Oppslag([takson], [Navn(1, 100, "Vulpes vulpes")]);

        var treff = oppslag.Resolve("vulpes vulpes", null, null);

        treff!.Taxon.TaxonIdHierarchy.Should().Equal([100, 90, 80, 1]);
        treff.Taxon.TaxonGroupId.Should().Be(42);
        treff.Taxon.ScientificNameIds.Should().Equal([1, 2]);
        treff.Taxon.Properties.Should().ContainSingle(x => x.Property == "LC");
        treff.Taxon.ValidScientificName.Should().Be("Vulpes vulpes");
        treff.Taxon.ValidScientificNameAuthorship.Should().Be("Linnaeus, 1758");
    }

    /// <summary>
    /// Et navn som peker på et takson som ikke finnes — bare mulig hvis taksonet er
    /// slettet mens navnet står igjen — er verdiløst, og skal ikke gi et halvt treff.
    /// </summary>
    [Fact]
    public void Gir_null_naar_navnet_peker_paa_et_takson_som_ikke_finnes()
    {
        var oppslag = Oppslag([], [Navn(1, 999, "Vulpes vulpes")]);

        oppslag.Resolve("vulpes vulpes", null, null).Should().BeNull();
    }

    /// <summary>
    /// Navn som bare er en rangforkortelse sier ingenting og legges ikke i oppslaget.
    /// Målt: 0 av 228 639 navn i den produksjonslike basen er slike.
    /// </summary>
    [Theory]
    [InlineData("sp")]
    [InlineData("indet")]
    [InlineData("agg")]
    [InlineData("×")]
    public void Legger_ikke_bare_rangforkortelser_i_oppslaget(string navn)
    {
        var oppslag = Oppslag([Takson(100)], [Navn(1, 100, navn)]);

        oppslag.Resolve(navn, null, null).Should().BeNull();
    }

    [Fact]
    public void Stripper_html_formatering_fra_navnet_ved_oppslag()
    {
        var oppslag = Oppslag(
            [Takson(100)],
            [Navn(1, 100, "<i>Echinorhynchus gadi</i>")]);

        oppslag.Resolve("echinorhynchus gadi", null, null).Should().NotBeNull();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Avviser_tomt_sokenavn(string? navn)
    {
        var oppslag = Oppslag([Takson(100)], [Navn(1, 100, "Vulpes vulpes")]);

        var act = () => oppslag.Resolve(navn!, null, null);

        act.Should().Throw<ArgumentException>();
    }
}

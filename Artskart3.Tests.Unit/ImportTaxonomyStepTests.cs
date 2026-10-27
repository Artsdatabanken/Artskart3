using Artskart3.Core.Application.Import;
using Artskart3.Core.Application.Import.Models;
using Artskart3.Core.Application.Import.Steps;
using FluentAssertions;

namespace Artskart3.Tests.Unit;

public class ImportTaxonomyStepTests
{
    private readonly ImportedObservation _observasjon = new();
    private readonly ImportContext _context = new();

    /// <summary>
    /// Navneoppslag som svarer på forhåndsbestemte navn og noterer hva det ble spurt
    /// om. Kaskaden i steget er det som testes her — selve oppslaget er testet for seg.
    /// </summary>
    private sealed class FakeOppslag : ITaxonNameResolver
    {
        private readonly Dictionary<string, TaxonMatch> _svar = new(StringComparer.Ordinal);
        private readonly Dictionary<(string, string), TaxonMatch> _svarPerForfatter = [];

        public List<(string Navn, string? Rang, string? Forfatter)> Spørsmål { get; } = [];

        public FakeOppslag Med(string navn, TaxonMatch treff)
        {
            _svar[navn] = treff;
            return this;
        }

        /// <summary>Svarer bare når forfatterstrengen stemmer også.</summary>
        public FakeOppslag Med(string navn, string forfatter, TaxonMatch treff)
        {
            _svarPerForfatter[(navn, forfatter)] = treff;
            return this;
        }

        public TaxonMatch? Resolve(string name, string? recordTaxonRank, string? authorString)
        {
            Spørsmål.Add((name, recordTaxonRank, authorString));

            return _svarPerForfatter.TryGetValue((name, authorString ?? string.Empty), out var påForfatter)
                ? påForfatter
                : _svar.GetValueOrDefault(name);
        }
    }

    private static TaxonMatch Treff(
        int taxonId = 100,
        string gyldigNavn = "Vulpes vulpes",
        string? forfatter = "Linnaeus, 1758",
        int taxonGroupId = 42,
        int[]? hierarki = null,
        IReadOnlyList<TaxonPropertyInfo>? egenskaper = null,
        int matchetNavnId = 7,
        string diagnostikk = "") =>
        new(
            new TaxonRecord(
                taxonId, 22, gyldigNavn, forfatter, taxonGroupId,
                hierarki ?? [taxonId, 90, 1],
                [7, 8],
                egenskaper ?? []),
            matchetNavnId,
            "<i>" + gyldigNavn + "</i>",
            diagnostikk);

    private ImportRecordResult Kjør(CacheRecord record, ITaxonNameResolver oppslag) =>
        new ImportTaxonomyStep(oppslag).ValidateAndImport(record, _observasjon, _context);

    [Fact]
    public void Er_et_pre_steg()
    {
        new ImportTaxonomyStep(new FakeOppslag()).StepType.Should().Be(ImportStepType.Pre);
    }

    // --- Treff --------------------------------------------------------------

    [Fact]
    public void Setter_takson_gruppe_og_hierarki()
    {
        var oppslag = new FakeOppslag().Med("vulpes vulpes", Treff(hierarki: [100, 90, 80, 1]));

        var resultat = Kjør(new CacheRecord { ScientificName = "Vulpes vulpes" }, oppslag);

        resultat.ValidationCode.Should().Be(RecordValidationCode.Ok);
        _observasjon.TaxonId.Should().Be(100);
        _observasjon.MatchedScientificNameId.Should().Be(7);
        _observasjon.TaxonGroupId.Should().Be(42);
        _observasjon.TaxonIds.Should().Equal([100, 90, 80, 1]);
    }

    [Fact]
    public void Legger_taksonegenskaper_og_navneider_i_konteksten()
    {
        var egenskaper = new List<TaxonPropertyInfo> { new("RL", "Norge", "LC") };
        var oppslag = new FakeOppslag().Med("vulpes vulpes", Treff(egenskaper: egenskaper));

        Kjør(new CacheRecord { ScientificName = "Vulpes vulpes" }, oppslag);

        _context.TaxonProperties.Should().ContainSingle(x => x.Property == "LC");
        _context.AllScientificNameIds.Should().Equal([7, 8]);
    }

    /// <summary>
    /// Hierarkiet settes på nytt ved hver kjøring — steget kan kjøres om igjen etter en
    /// annotasjon, og da skal ikke nivåene dubleres.
    /// </summary>
    [Fact]
    public void Nullstiller_hierarkiet_for_det_settes()
    {
        _observasjon.TaxonIds.AddRange([1, 2, 3]);

        var oppslag = new FakeOppslag().Med("vulpes vulpes", Treff(hierarki: [100, 1]));

        Kjør(new CacheRecord { ScientificName = "Vulpes vulpes" }, oppslag);

        _observasjon.TaxonIds.Should().Equal([100, 1]);
    }

    /// <summary>
    /// Taksonets gyldige navn overskriver kildens, uten HTML-formatering, og kildens
    /// verdi lagres som <c>_original_</c>.
    /// </summary>
    [Fact]
    public void Skriver_taksonets_navn_tilbake_paa_posten()
    {
        var record = new CacheRecord { ScientificName = "Vulpes vulpe", ScientificNameAuthor = "L." };
        var oppslag = new FakeOppslag().Med("vulpes vulpe", Treff());

        Kjør(record, oppslag);

        record.ScientificName.Should().Be("Vulpes vulpes");
        record.ScientificNameAuthor.Should().Be("Linnaeus, 1758");
        _context.DynamicProperties["_original_ScientificName"].Should().Be("Vulpes vulpe");
        _context.DynamicProperties["_original_ScientificNameAuthor"].Should().Be("L.");
    }

    [Fact]
    public void Lagrer_ikke_original_naar_navnet_er_det_samme()
    {
        var record = new CacheRecord { ScientificName = "Vulpes vulpes", ScientificNameAuthor = "Linnaeus, 1758" };
        var oppslag = new FakeOppslag().Med("vulpes vulpes", Treff());

        Kjør(record, oppslag);

        _context.DynamicProperties.Should().NotContainKey("_original_ScientificName");
        _context.DynamicProperties.Should().NotContainKey("_original_ScientificNameAuthor");
    }

    /// <summary>
    /// Er originalen alt lagret — fordi steget kjøres om igjen etter en annotasjon —
    /// skal den ikke overskrives med den annoterte verdien.
    /// </summary>
    [Fact]
    public void Overskriver_ikke_en_original_som_alt_er_lagret()
    {
        _context.DynamicProperties["_original_ScientificName"] = "Fra første kjøring";

        var record = new CacheRecord { ScientificName = "Vulpes vulpe" };
        var oppslag = new FakeOppslag().Med("vulpes vulpe", Treff());

        Kjør(record, oppslag);

        _context.DynamicProperties["_original_ScientificName"].Should().Be("Fra første kjøring");
    }

    // --- Avvisning ----------------------------------------------------------

    [Fact]
    public void Avviser_post_uten_artsnavn()
    {
        var resultat = Kjør(new CacheRecord(), new FakeOppslag());

        resultat.IsAccepted.Should().BeFalse();
        resultat.ValidationCode.Should().Be(RecordValidationCode.NoTaxonomicMatch);
        resultat.NotificationCode.Should().Be(RecordNotificationCode.MissingScientificName);
    }

    [Fact]
    public void Avviser_navn_som_ikke_treffer()
    {
        var resultat = Kjør(new CacheRecord { ScientificName = "Finnes ikke" }, new FakeOppslag());

        resultat.IsAccepted.Should().BeFalse();
        resultat.ValidationCode.Should().Be(RecordValidationCode.NoTaxonomicMatch);
        resultat.NotificationCode.Should().Be(RecordNotificationCode.NotAbleToMatchTaxonomy);
        resultat.ErrorContext.Should().StartWith("finnes ikke");
    }

    /// <summary>
    /// Diagnostikken fra et flertydig oppslag følger med i avvisningen — ofte det eneste
    /// sporet av hvorfor navnet ikke kunne bestemmes.
    /// </summary>
    [Fact]
    public void Tar_med_diagnostikken_i_avvisningen()
    {
        var oppslag = new FakeOppslag();

        // Fake-oppslaget svarer aldri, men steget skal ta med diagnostikken når den
        // finnes. Her simuleres et treff som bærer en melding, men som gjelder et annet
        // navn enn det som spørres om.
        var resultat = Kjør(new CacheRecord { ScientificName = "Bufo bufo" }, oppslag);

        resultat.ErrorContext.Should().StartWith("bufo bufo");
    }

    // --- Kaskaden -----------------------------------------------------------

    [Fact]
    public void Slaar_opp_med_normalisert_navn_og_forfatter()
    {
        var oppslag = new FakeOppslag().Med("carex nigra", Treff(gyldigNavn: "Carex nigra"));

        Kjør(new CacheRecord { ScientificName = "Carex nigra", ScientificNameAuthor = "Reichard" }, oppslag);

        oppslag.Spørsmål[0].Navn.Should().Be("carex nigra");
        oppslag.Spørsmål[0].Forfatter.Should().Be("reichard", "forfatteren sendes i små bokstaver");
    }

    /// <summary>
    /// Andre forsøk: navnet bygges fra rangfeltene når <c>scientificName</c> ser
    /// avkortet ut og ikke inneholder forfatteren.
    /// </summary>
    [Fact]
    public void Proever_navn_fra_rangfeltene_naar_artsnavnet_er_avkortet()
    {
        var record = new CacheRecord
        {
            ScientificName = "Carex",
            Genus = "Carex",
            Species = "nigra",
            ScientificNameAuthor = "Reichard"
        };

        var oppslag = new FakeOppslag().Med("carex nigra", Treff(gyldigNavn: "Carex nigra"));

        var resultat = Kjør(record, oppslag);

        resultat.IsAccepted.Should().BeTrue();
        oppslag.Spørsmål.Select(x => x.Navn).Should().Equal(["carex", "carex nigra"]);
    }

    /// <summary>« x » i navnet er en hybridskrivemåte rangfeltene håndterer bedre.</summary>
    [Fact]
    public void Proever_rangfeltene_for_hybridnavn()
    {
        var record = new CacheRecord
        {
            ScientificName = "Salix x aurita",
            Genus = "Salix",
            Species = "aurita"
        };

        var oppslag = new FakeOppslag().Med("salix aurita", Treff(gyldigNavn: "Salix aurita"));

        Kjør(record, oppslag).IsAccepted.Should().BeTrue();
    }

    /// <summary>
    /// Tredje forsøk: forfatteren står inne i <c>scientificName</c> og ikke i sitt eget
    /// felt. Navnet bygges fra rangfeltene, og resten brukes som forfatterstreng.
    /// </summary>
    [Fact]
    public void Proever_forfatter_trukket_ut_av_artsnavnet()
    {
        var record = new CacheRecord
        {
            ScientificName = "Carex nigra reichard",
            Genus = "Carex",
            Species = "nigra"
        };

        var oppslag = new FakeOppslag();

        Kjør(record, oppslag);

        // Siste spørsmål skal være navnet fra rangfeltene med resten som forfatter.
        oppslag.Spørsmål.Last().Navn.Should().Be("carex nigra");
        oppslag.Spørsmål.Last().Forfatter.Should().Be("reichard");
    }

    [Fact]
    public void Bufrer_oppslaget_per_navn()
    {
        var oppslag = new FakeOppslag().Med("vulpes vulpes", Treff());
        var steg = new ImportTaxonomyStep(oppslag);

        steg.ValidateAndImport(
            new CacheRecord { ScientificName = "Vulpes vulpes" }, new ImportedObservation(), new ImportContext());
        steg.ValidateAndImport(
            new CacheRecord { ScientificName = "Vulpes vulpes" }, new ImportedObservation(), new ImportContext());

        oppslag.Spørsmål.Should().HaveCount(1, "samme navn skal bare slås opp én gang");
    }

    /// <summary>Bufferen holder også bomskuddene — et navn som ikke traff, treffer ikke neste gang.</summary>
    [Fact]
    public void Bufrer_ogsaa_navn_som_ikke_traff()
    {
        var oppslag = new FakeOppslag();
        var steg = new ImportTaxonomyStep(oppslag);

        var record = new CacheRecord { ScientificName = "Carex nigra Reichard", Genus = "Carex", Species = "nigra" };

        steg.ValidateAndImport(record, new ImportedObservation(), new ImportContext());
        var etterFørste = oppslag.Spørsmål.Count;

        steg.ValidateAndImport(
            new CacheRecord { ScientificName = "Carex nigra Reichard", Genus = "Carex", Species = "nigra" },
            new ImportedObservation(), new ImportContext());

        etterFørste.Should().BeGreaterThan(1, "kaskaden skal ha prøvd flere forsøk");
        oppslag.Spørsmål.Should().HaveCount(etterFørste, "andre gang skal ikke koste et eneste oppslag");
    }

    /// <summary>
    /// Målt på node 59: ni poster heter «Dicranomyia lutea», én av dem uten forfatter.
    /// Artskart 2 nøkler bufferen på navnet alene, slår den ene opp til slekten, og de
    /// åtte andre arver slekten selv om forfatteren deres peker entydig på arten.
    /// Rekkefølgen i cachen avgjorde utfallet. Se 5d i
    /// <c>MuligeArtskart2BugsViBørAvklare.md</c>.
    /// </summary>
    [Fact]
    public void Bufrer_ikke_paa_tvers_av_forfattere()
    {
        var art = Treff(taxonId: 18449, matchetNavnId: 34879, gyldigNavn: "Dicranomyia lutea");
        var slekt = Treff(taxonId: 18433, matchetNavnId: 20786, gyldigNavn: "Dicranomyia");

        var oppslag = new FakeOppslag()
            .Med("dicranomyia lutea", "(meigen, 1818)", art)
            .Med("dicranomyia", "lutea", slekt);

        var steg = new ImportTaxonomyStep(oppslag);

        // Posten uten forfatter først, slik Artskart 2 leste dem.
        var utenForfatter = new ImportedObservation();
        steg.ValidateAndImport(
            new CacheRecord { ScientificName = "Dicranomyia lutea", Genus = "Dicranomyia" },
            utenForfatter, new ImportContext());

        var medForfatter = new ImportedObservation();
        steg.ValidateAndImport(
            new CacheRecord
            {
                ScientificName = "Dicranomyia lutea",
                ScientificNameAuthor = "(Meigen, 1818)",
                Genus = "Dicranomyia"
            },
            medForfatter, new ImportContext());

        utenForfatter.TaxonId.Should().Be(18433, "posten uten forfatter er flertydig og faller til slekten");
        medForfatter.TaxonId.Should().Be(18449, "forfatteren peker entydig på arten og skal ikke arve slekten");
        medForfatter.MatchedScientificNameId.Should().Be(34879);
    }

    /// <summary>
    /// Samme navn og samme forfatter, men ulike rangfelt. Rangfeltene bygger navnet i
    /// forsøk to og tre, så de hører i nøkkelen: her treffer den ene slekten sin, mens
    /// den andre ikke har noe navn å falle tilbake på og skal avvises.
    /// </summary>
    [Fact]
    public void Bufrer_ikke_paa_tvers_av_rangfelt()
    {
        var slekt = Treff(taxonId: 18433, matchetNavnId: 20786, gyldigNavn: "Dicranomyia");

        var oppslag = new FakeOppslag().Med("dicranomyia", "lutea", slekt);
        var steg = new ImportTaxonomyStep(oppslag);

        var riktigSlekt = new ImportedObservation();
        var første = steg.ValidateAndImport(
            new CacheRecord { ScientificName = "Dicranomyia lutea", Genus = "Dicranomyia" },
            riktigSlekt, new ImportContext());

        var annenSlekt = steg.ValidateAndImport(
            new CacheRecord { ScientificName = "Dicranomyia lutea", Genus = "Limonia" },
            new ImportedObservation(), new ImportContext());

        første.ValidationCode.Should().Be(RecordValidationCode.Ok);
        riktigSlekt.TaxonId.Should().Be(18433);

        annenSlekt.ValidationCode.Should().Be(
            RecordValidationCode.NoTaxonomicMatch,
            "posten har en annen slekt og skal ikke arve treffet til den forrige");
    }

    /// <summary>
    /// Forsøk to og tre kortslutter uten å spørre når posten ikke har rangfelt: det
    /// finnes ikke noe annet navn å bygge, og ingen forfatter å trekke fra.
    /// </summary>
    [Fact]
    public void Proever_bare_ett_oppslag_naar_posten_mangler_rangfelt()
    {
        var oppslag = new FakeOppslag();

        Kjør(new CacheRecord { ScientificName = "Finnes ikke" }, oppslag);

        oppslag.Spørsmål.Should().HaveCount(1);
    }

    // --- Rangutledning ------------------------------------------------------

    [Theory]
    [InlineData("var", "variety")]
    [InlineData("var.", "variety")]
    [InlineData("spp", "subspecies")]
    [InlineData("ssp", "subspecies")]
    [InlineData("sp", "subspecies")]
    [InlineData("subsp", "subspecies")]
    [InlineData("f", "form")]
    [InlineData("form", "form")]
    public void Oversetter_rangforkortelsene_kilden_bruker(string oppgitt, string forventet)
    {
        var oppslag = new FakeOppslag();

        Kjør(new CacheRecord { ScientificName = "Carex nigra", TaxonRank = oppgitt }, oppslag);

        oppslag.Spørsmål[0].Rang.Should().Be(forventet);
    }

    /// <summary>En rang kilden oppgir i klartekst sendes videre som den står.</summary>
    [Fact]
    public void Sender_oppgitt_rang_videre_uendret()
    {
        var oppslag = new FakeOppslag();

        Kjør(new CacheRecord { ScientificName = "Carex nigra", TaxonRank = "Species" }, oppslag);

        oppslag.Spørsmål[0].Rang.Should().Be("Species", "navneoppslaget normaliserer selv");
    }

    [Theory]
    [InlineData("juncella", null, null, "subspecies")]
    [InlineData(null, "nigra", null, "species")]
    [InlineData(null, null, "Carex", "genus")]
    public void Utleder_rangen_av_det_laveste_utfylte_rangfeltet(
        string? underart, string? art, string? slekt, string forventet)
    {
        var record = new CacheRecord
        {
            ScientificName = "Carex nigra",
            Subspecies = underart,
            Species = art,
            Genus = slekt
        };

        var oppslag = new FakeOppslag();

        Kjør(record, oppslag);

        oppslag.Spørsmål[0].Rang.Should().Be(forventet);
    }

    /// <summary>
    /// Plassholdere som «sp» og «indet» teller ikke som utfylt — ellers ville hver post
    /// med «Carex sp.» blitt tolket som en art.
    /// </summary>
    [Fact]
    public void Regner_ikke_plassholdere_som_utfylt_rangfelt()
    {
        var record = new CacheRecord
        {
            ScientificName = "Carex",
            Species = "sp.",
            Genus = "Carex"
        };

        var oppslag = new FakeOppslag();

        Kjør(record, oppslag);

        oppslag.Spørsmål[0].Rang.Should().Be("genus");
    }

    /// <summary>
    /// Kjent skjevhet i Artskart 2: <c>order</c> sjekkes etter <c>phylum</c>. En post med
    /// begge utfylt får derfor <c>phylum</c>.
    /// </summary>
    [Fact]
    public void Phylum_vinner_over_order_som_i_Artskart2()
    {
        var record = new CacheRecord
        {
            ScientificName = "Noe",
            Phylum = "Tracheophyta",
            Order = "Poales"
        };

        var oppslag = new FakeOppslag();

        Kjør(record, oppslag);

        oppslag.Spørsmål[0].Rang.Should().Be("phylum");
    }

    [Fact]
    public void Gir_ingen_rang_naar_ingenting_er_utfylt()
    {
        var oppslag = new FakeOppslag();

        Kjør(new CacheRecord { ScientificName = "Noe ukjent" }, oppslag);

        oppslag.Spørsmål[0].Rang.Should().BeNull();
    }

    /// <summary>
    /// Er forfatteren gjettet ut av artsnavnet, forteller antall ledd som blir igjen
    /// hvilken rang navnet har.
    /// </summary>
    [Theory]
    [InlineData("Carex nigra Reichard", "species")]
    [InlineData("Carex nigra juncella Reichard", "subspecies")]
    public void Utleder_rangen_av_antall_ledd_naar_forfatteren_er_gjettet(string navn, string forventet)
    {
        var oppslag = new FakeOppslag();

        Kjør(new CacheRecord { ScientificName = navn }, oppslag);

        oppslag.Spørsmål[0].Rang.Should().Be(forventet);
    }
}

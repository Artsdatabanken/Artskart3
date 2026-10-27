using Artskart3.Core.Application.Import;
using Artskart3.Core.Application.Import.Models;
using Artskart3.Core.Application.Import.Steps;
using FluentAssertions;

namespace Artskart3.Tests.Unit;

public class ImportStatusForRegionStepTests
{
    /// <summary>Kategoritabellen slik den står i basen: 15 rader, NE er Id 1.</summary>
    private sealed class FakeKategorier : ICategoryLookup
    {
        private static readonly Dictionary<string, CategoryRef> Koder = new(StringComparer.OrdinalIgnoreCase)
        {
            ["NE"] = new(1, "NE"),
            ["NA"] = new(2, "NA"),
            ["NR"] = new(3, "NR"),
            ["NK"] = new(4, "NK"),
            ["LO"] = new(5, "LO"),
            ["PH"] = new(6, "PH"),
            ["HI"] = new(7, "HI"),
            ["SE"] = new(8, "SE"),
            ["LC"] = new(9, "LC"),
            ["DD"] = new(10, "DD"),
            ["NT"] = new(11, "NT"),
            ["VU"] = new(12, "VU"),
            ["EN"] = new(13, "EN"),
            ["CR"] = new(14, "CR"),
            ["RE"] = new(15, "RE")
        };

        public CategoryRef? ByCode(string code) => Koder.GetValueOrDefault(code);
    }

    private const int NE = 1;
    private const int LC = 9;
    private const int VU = 12;
    private const int EN = 13;
    private const int SE = 8;
    private const int HI = 7;

    private readonly ImportStatusForRegionStep _steg = new(new FakeKategorier());
    private readonly ImportContext _context = new();

    private ImportedObservation Observasjon(bool påSvalbard = false)
    {
        var lokalitet = new ImportedLocation { LookupId = "test" };

        lokalitet.Areas.Add(påSvalbard
            ? new LocationArea(99, 6, "21", "Svalbard")
            : new LocationArea(1, 1, "0301", "Oslo"));

        return new ImportedObservation { Location = lokalitet };
    }

    private ImportRecordResult Kjør(ImportedObservation observasjon, params TaxonPropertyInfo[] egenskaper)
    {
        _context.TaxonProperties = egenskaper.ToList();

        return _steg.ValidateAndImport(new CacheRecord(), observasjon, _context);
    }

    [Fact]
    public void Er_et_post_steg()
    {
        _steg.StepType.Should().Be(ImportStepType.Post);
    }

    [Fact]
    public void Gjor_ingenting_uten_taksonegenskaper()
    {
        var observasjon = Observasjon();

        var resultat = Kjør(observasjon);

        resultat.ValidationCode.Should().Be(RecordValidationCode.Ok);
        observasjon.CategoryId.Should().BeNull();
    }

    [Fact]
    public void Setter_rodlistekategorien_for_fastlandet()
    {
        var observasjon = Observasjon();

        Kjør(observasjon, new TaxonPropertyInfo("RL2015", "N", "VU"));

        observasjon.CategoryId.Should().Be(VU);
    }

    [Fact]
    public void Setter_svalbardkategorien_for_en_post_pa_svalbard()
    {
        var observasjon = Observasjon(påSvalbard: true);

        Kjør(observasjon,
            new TaxonPropertyInfo("RL2015", "N", "VU"),
            new TaxonPropertyInfo("RL2015", "S", "EN"));

        observasjon.CategoryId.Should().Be(EN, "på Svalbard gjelder svalbardvurderingen");
    }

    [Fact]
    public void Setter_fastlandskategorien_for_en_post_pa_fastlandet()
    {
        var observasjon = Observasjon();

        Kjør(observasjon,
            new TaxonPropertyInfo("RL2015", "N", "VU"),
            new TaxonPropertyInfo("RL2015", "S", "EN"));

        observasjon.CategoryId.Should().Be(VU);
    }

    /// <summary>
    /// Finnes bare vurderingen for den andre regionen, er taksonet ikke vurdert her —
    /// og da skal det ikke vises en kategori fra feil region.
    /// </summary>
    [Fact]
    public void Gir_ikke_vurdert_naar_bare_svalbardvurderingen_finnes_og_posten_er_pa_fastlandet()
    {
        var observasjon = Observasjon();

        Kjør(observasjon, new TaxonPropertyInfo("RL2015", "S", "EN"));

        observasjon.CategoryId.Should().Be(NE);
    }

    [Fact]
    public void Gir_ikke_vurdert_naar_bare_fastlandsvurderingen_finnes_og_posten_er_pa_svalbard()
    {
        var observasjon = Observasjon(påSvalbard: true);

        Kjør(observasjon, new TaxonPropertyInfo("RL2015", "N", "VU"));

        observasjon.CategoryId.Should().Be(NE);
    }

    [Fact]
    public void Gir_ingen_kategori_naar_ingen_vurdering_gjelder_en_region()
    {
        var observasjon = Observasjon();

        Kjør(observasjon, new TaxonPropertyInfo("RL2015", "X", "VU"));

        observasjon.CategoryId.Should().BeNull();
    }

    // --- Nyeste vurdering vinner -------------------------------------------

    [Fact]
    public void Nyeste_vurdering_vinner()
    {
        var observasjon = Observasjon();

        Kjør(observasjon,
            new TaxonPropertyInfo("RL2010", "N", "EN"),
            new TaxonPropertyInfo("RL2021", "N", "LC"),
            new TaxonPropertyInfo("RL2015", "N", "VU"));

        observasjon.CategoryId.Should().Be(LC, "2021 er nyest");
    }

    /// <summary>
    /// Artskart 2 bruker <c>int.Parse</c> på de fire siste tegnene i prefikset og
    /// kaster for et prefiks uten årstall — et unntak som feller hele nodeimporten. Her
    /// sorteres slike bakerst.
    /// </summary>
    [Fact]
    public void Taaler_prefiks_uten_arstall()
    {
        var observasjon = Observasjon();

        Kjør(observasjon,
            new TaxonPropertyInfo("RL", "N", "EN"),
            new TaxonPropertyInfo("RL2015", "N", "VU"));

        observasjon.CategoryId.Should().Be(VU, "den med årstall skal vinne");
    }

    [Fact]
    public void Taaler_kort_prefiks()
    {
        var observasjon = Observasjon();

        Kjør(observasjon, new TaxonPropertyInfo("X", "N", "VU"));

        observasjon.CategoryId.Should().Be(VU);
    }

    // --- Fremmedart mot rødliste -------------------------------------------

    /// <summary>En reell fremmedartsvurdering slår en rødlistestatus.</summary>
    [Fact]
    public void Fremmedartsvurdering_slar_rodlistestatus()
    {
        var observasjon = Observasjon();

        Kjør(observasjon,
            new TaxonPropertyInfo("RL2015", "N", "NA"),
            new TaxonPropertyInfo("FA2012", "N", "SE"));

        observasjon.CategoryId.Should().Be(SE);
    }

    [Fact]
    public void Fremmedartsvurdering_slar_rodlistestatus_ogsaa_uten_uegnet_rodliste()
    {
        var observasjon = Observasjon();

        Kjør(observasjon,
            new TaxonPropertyInfo("RL2015", "N", "LC"),
            new TaxonPropertyInfo("FA2012", "N", "HI"));

        observasjon.CategoryId.Should().Be(HI);
    }

    /// <summary>
    /// «Ikke risikovurdert» som fremmed art slår en rødlistestatus som bare sier «ikke
    /// egnet» — men <c>NR</c> vises som <c>NE</c>, siden den sier at noen har sett på
    /// taksonet og konkludert med ingenting.
    /// </summary>
    [Fact]
    public void Ikke_risikovurdert_gir_ikke_vurdert()
    {
        var observasjon = Observasjon();

        Kjør(observasjon,
            new TaxonPropertyInfo("RL2015", "N", "NA"),
            new TaxonPropertyInfo("FA2012", "N", "NR"));

        observasjon.CategoryId.Should().Be(NE);
    }

    /// <summary>
    /// Er rødlistestatusen en reell vurdering, slår «ikke risikovurdert» ingenting — og
    /// da er det den nyeste som gjelder.
    /// </summary>
    [Fact]
    public void Ikke_risikovurdert_slar_ikke_en_reell_rodlistestatus()
    {
        var observasjon = Observasjon();

        Kjør(observasjon,
            new TaxonPropertyInfo("RL2021", "N", "VU"),
            new TaxonPropertyInfo("FA2012", "N", "NR"));

        observasjon.CategoryId.Should().Be(VU, "2021 er nyest, og NR slår ikke en reell vurdering");
    }

    // --- Kodebehandling ----------------------------------------------------

    /// <summary>Koder lengre enn to tegn kuttes: «VU°» og «LC*» finnes i dataene.</summary>
    [Theory]
    [InlineData("VU°", VU)]
    [InlineData("LC*", LC)]
    [InlineData("EN(a)", EN)]
    public void Kutter_kategorikoden_til_to_tegn(string kode, int forventet)
    {
        var observasjon = Observasjon();

        Kjør(observasjon, new TaxonPropertyInfo("RL2015", "N", kode));

        observasjon.CategoryId.Should().Be(forventet);
    }

    [Fact]
    public void Gir_ikke_vurdert_for_en_kode_som_ikke_finnes()
    {
        var observasjon = Observasjon();

        Kjør(observasjon, new TaxonPropertyInfo("RL2015", "N", "ZZ"));

        observasjon.CategoryId.Should().Be(NE);
    }

    [Fact]
    public void Gir_ikke_vurdert_for_tom_kode()
    {
        var observasjon = Observasjon();

        Kjør(observasjon, new TaxonPropertyInfo("RL2015", "N", ""));

        observasjon.CategoryId.Should().Be(NE);
    }

    // --- Svalbard-oppslaget -------------------------------------------------

    /// <summary>
    /// Svalbard kjennes igjen på områdekoden 21. Posten kan mangle lokalitet helt — en
    /// post uten koordinat har ingen områder.
    /// </summary>
    [Fact]
    public void Regner_post_uten_lokalitet_som_pa_fastlandet()
    {
        var observasjon = new ImportedObservation();

        Kjør(observasjon, new TaxonPropertyInfo("RL2015", "N", "VU"));

        observasjon.CategoryId.Should().Be(VU);
    }

    [Fact]
    public void Regner_post_uten_omrader_som_pa_fastlandet()
    {
        var observasjon = new ImportedObservation { Location = new ImportedLocation { LookupId = "tom" } };

        Kjør(observasjon, new TaxonPropertyInfo("RL2015", "S", "EN"));

        observasjon.CategoryId.Should().Be(NE, "ingen områder betyr ikke Svalbard");
    }
}

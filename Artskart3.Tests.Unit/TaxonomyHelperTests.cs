using Artskart3.Core.Application.Import.Helpers;
using Artskart3.Core.Application.Import.Models;
using FluentAssertions;

namespace Artskart3.Tests.Unit;

public class TaxonomyHelperTests
{
    [Theory]
    [InlineData("Vulpes vulpes", "vulpes vulpes")]
    [InlineData("  Vulpes   vulpes  ", "vulpes vulpes")]
    [InlineData("Carex sp.", "carex")]
    [InlineData("Carex indet", "carex")]
    [InlineData("Carex var nigra", "carex nigra")]
    [InlineData("Carex ssp nigra", "carex nigra")]
    [InlineData("Carex subsp nigra", "carex nigra")]
    [InlineData("Vulpes vulpes f alba", "vulpes vulpes alba")]
    [InlineData("Hieracium sensu lato", "hieracium")]
    [InlineData("Hieracium sensu stricto", "hieracium")]
    public void CleanScientificName_normaliserer_navnet(string input, string forventet)
    {
        TaxonomyHelper.CleanScientificName(input).Should().Be(forventet);
    }

    /// <summary>Artsnavnebasen bruker «agg.» og «-gruppen», ikke «agg» og «-group».</summary>
    [Theory]
    [InlineData("Rubus fruticosus agg.", "rubus fruticosus agg.")]
    [InlineData("Taraxacum Ruderalia-group", "taraxacum ruderalia-gruppen")]
    public void CleanScientificName_beholder_norske_former(string input, string forventet)
    {
        TaxonomyHelper.CleanScientificName(input).Should().Be(forventet);
    }

    [Fact]
    public void CleanScientificName_normaliserer_hybridtegnet()
    {
        TaxonomyHelper.CleanScientificName("Betula × pubescens").Should().Be("betula ×pubescens");
    }

    [Theory]
    [InlineData("Carex aff nigra", "carex nigra")]
    [InlineData("Carex cf nigra", "carex nigra")]
    [InlineData("aff Carex nigra", "carex nigra")]
    [InlineData("cf. Carex nigra", "carex nigra")]
    public void ExtractScientificName_fjerner_aff_og_cf_og_flagger_usikker_bestemmelse(
        string scientificName, string forventet)
    {
        var record = new CacheRecord { ScientificName = scientificName };

        TaxonomyHelper.ExtractScientificName(record, null).Should().Be(forventet);
        record.IdentificationPrecision.Should().BeTrue();
    }

    [Fact]
    public void ExtractScientificName_flagger_ikke_sikre_bestemmelser()
    {
        var record = new CacheRecord { ScientificName = "Carex nigra" };

        TaxonomyHelper.ExtractScientificName(record, null).Should().Be("carex nigra");
        record.IdentificationPrecision.Should().BeFalse();
    }

    [Fact]
    public void ExtractScientificName_fjerner_forfatterstrengen_fra_navnet()
    {
        var record = new CacheRecord
        {
            ScientificName = "Vulpes vulpes Linnaeus, 1758",
            ScientificNameAuthor = "Linnaeus, 1758"
        };

        TaxonomyHelper.ExtractScientificName(record, record.ScientificNameAuthor)
            .Should().Be("vulpes vulpes");
    }

    /// <summary>
    /// Asymmetrien i Artskart 2: betingelsen ser på feltet, mens det er argumentet som
    /// fjernes. Står feltet med stor forbokstav og argumentet er lowercaset, slår
    /// <c>Replace</c> ikke til, og forfatteren blir stående i navnet.
    /// </summary>
    [Fact]
    public void ExtractScientificName_fjerner_ikke_forfatter_naar_argumentet_har_annen_skrivemaate()
    {
        var record = new CacheRecord
        {
            ScientificName = "Vulpes vulpes Linnaeus, 1758",
            ScientificNameAuthor = "Linnaeus, 1758"
        };

        TaxonomyHelper.ExtractScientificName(record, "linnaeus, 1758")
            .Should().Be("vulpes vulpes linnaeus, 1758");
    }

    [Fact]
    public void ExtractScientificName_bygger_navn_fra_rangfeltene_naar_artsnavnet_mangler()
    {
        var record = new CacheRecord { Genus = "Carex", Species = "nigra" };

        TaxonomyHelper.ExtractScientificName(record, null).Should().Be("carex nigra");
    }

    [Fact]
    public void ExtractScientificName_bruker_rangfeltene_naar_artsnavnfeltet_hoppes_over()
    {
        var record = new CacheRecord
        {
            ScientificName = "Noe helt annet",
            Genus = "Carex",
            Species = "nigra",
            Subspecies = "juncella"
        };

        TaxonomyHelper.ExtractScientificName(record, null, skipScientificNameField: true)
            .Should().Be("carex nigra juncella");
    }

    /// <summary>
    /// BOLD-navn er strekkodeklustre. Det virkelige navnet ligger i det ukjente feltet
    /// «species» når kilden har tatt det med.
    /// </summary>
    [Fact]
    public void ExtractScientificName_bytter_ut_BOLD_navn_med_species_fra_ukjente_felt()
    {
        var record = new CacheRecord
        {
            ScientificName = "BOLD:AAA1234",
            UnknownFields = new Dictionary<string, string> { ["species"] = "Carex nigra" }
        };

        TaxonomyHelper.ExtractScientificName(record, null).Should().Be("carex nigra");
    }

    [Fact]
    public void ExtractScientificName_beholder_BOLD_navn_uten_species_i_ukjente_felt()
    {
        var record = new CacheRecord { ScientificName = "BOLD:AAA1234" };

        TaxonomyHelper.ExtractScientificName(record, null).Should().Be("bold:aaa1234");
    }

    [Fact]
    public void ExtractScientificName_gir_tom_streng_naar_ingenting_kan_utledes()
    {
        TaxonomyHelper.ExtractScientificName(new CacheRecord(), null).Should().BeEmpty();
    }

    [Theory]
    [InlineData("Carex", "nigra", "juncella", "carex nigra juncella")]
    [InlineData("Carex", "nigra", null, "carex nigra")]
    [InlineData("Carex", null, null, "carex")]
    public void ConstructScientificNameFromOtherFields_foelger_rangrekkefolgen(
        string? genus, string? species, string? subspecies, string forventet)
    {
        var record = new CacheRecord { Genus = genus, Species = species, Subspecies = subspecies };

        TaxonomyHelper.ConstructScientificNameFromOtherFields(record, string.Empty)
            .ToLowerInvariant().Should().Be(forventet);
    }

    [Fact]
    public void ConstructScientificNameFromOtherFields_faller_videre_nedover_rangene()
    {
        var record = new CacheRecord
        {
            Family = "Cyperaceae",
            Order = "Poales",
            Class = "Liliopsida",
            Phylum = "Tracheophyta",
            Kingdom = "Plantae"
        };

        TaxonomyHelper.ConstructScientificNameFromOtherFields(record, string.Empty).Should().Be("Cyperaceae");
    }

    [Fact]
    public void ConstructScientificNameFromOtherFields_gir_tilbake_navnet_uendret_uten_rangfelt()
    {
        TaxonomyHelper.ConstructScientificNameFromOtherFields(new CacheRecord(), "uendret")
            .Should().Be("uendret");
    }

    [Fact]
    public void ExtractAuthorString_bruker_feltet_naar_det_finnes()
    {
        var record = new CacheRecord { ScientificNameAuthor = "Linnaeus, 1758" };

        TaxonomyHelper.ExtractAuthorString(record).Should().Be("linnaeus, 1758");
    }

    /// <summary>
    /// Mangler feltet, gjettes forfatteren som første ledd etter det første som
    /// begynner med stor bokstav og er mer enn ett tegn.
    /// </summary>
    [Fact]
    public void ExtractAuthorString_gjetter_forfatter_fra_artsnavnet()
    {
        var record = new CacheRecord { ScientificName = "Carex nigra Linnaeus 1753" };

        TaxonomyHelper.ExtractAuthorString(record).Should().Be("linnaeus 1753");
    }

    [Fact]
    public void ExtractAuthorString_gjetter_ingenting_fra_et_rent_artsnavn()
    {
        var record = new CacheRecord { ScientificName = "Carex nigra" };

        TaxonomyHelper.ExtractAuthorString(record).Should().BeEmpty();
    }

    [Fact]
    public void RemoveScientificNameParanteseContent_fjerner_parentesinnhold()
    {
        TaxonomyHelper.RemoveScientificNameParanteseContent("Limosa lapponica (Linnaeus, 1758)")
            .Should().Be("Limosa lapponica");
    }
}

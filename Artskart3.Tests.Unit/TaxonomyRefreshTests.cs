extern alias workers;

using System.Text.Json;
using Artskart3.Core.Application.Taxonomy;
using Artskart3.Core.Domain.Entities;
using FluentAssertions;
using TaxonomyMapping = workers::Artskart3.Workers.Taxonomy.TaxonomyMapping;
using TaxonomyJson = workers::Artskart3.Workers.Taxonomy.TaxonomyJson;

namespace Artskart3.Tests.Unit;

/// <summary>
/// Mappingen fra Artskart 2s taksonomidokumenter til Artskart 3s entiteter.
///
/// DETTE ER PARITETSKRAV, ikke stilvalg. Hver test svarer til en linje i Artskart 2s
/// <c>Taxon.MapFrom</c>, og avviker mappingen, havner observasjoner på andre taksoner
/// enn i Artskart 2.
/// </summary>
public class TaxonomyRefreshTests
{
    private static readonly DateTime Nå = new(2026, 10, 15, 12, 0, 0, DateTimeKind.Utc);

    private static TaxonInfoDocument Dokument() => new()
    {
        Id = 47871,
        TaxonId = 47871,
        CategoryValue = TaxonomicCategory.Species,
        ValidScientificNameId = 71898,
        ValidScientificName = "Letharia vulpina",
        TaxonGroupId = 66,
        ExistsInCountry = true,
        ScientificNameIdHiarchy = [71898, 71896, 71443],
        TaxonIdHiarchy = [47871, 47869, 47416]
    };

    /// <summary>
    /// Artskart 2: <c>ParentTaxonId == 0 ? TaxonIdHiarchy.FirstOrDefault(x =&gt; x !=
    /// TaxonId) : ParentTaxonId</c>. Første ledd i hierarkiet er taksonet selv, så
    /// forelderen er det andre.
    /// </summary>
    [Fact]
    public void Henter_forelderen_fra_hierarkiet_naar_den_mangler()
    {
        var rad = new Taxon();

        TaxonomyMapping.Apply(rad, Dokument(), Nå);

        rad.ParentTaxonId.Should().Be(47869, "47871 er taksonet selv og skal hoppes over");
    }

    [Fact]
    public void Bruker_oppgitt_forelder_naar_den_finnes()
    {
        var dokument = Dokument();
        dokument.ParentTaxonId = 12345;

        var rad = new Taxon();

        TaxonomyMapping.Apply(rad, dokument, Nå);

        rad.ParentTaxonId.Should().Be(12345);
    }

    /// <summary>
    /// Artskart 2 foretrekker den formaterte varianten. Det er grunnen til at navnene i
    /// indeksen bærer kursivtagger, og riggen sammenligner dem ordrett.
    /// </summary>
    [Fact]
    public void Foretrekker_det_formaterte_navnet()
    {
        var dokument = Dokument();
        dokument.ValidScientificNameFormatted = "<i>Letharia vulpina</i>";

        var rad = new Taxon();

        TaxonomyMapping.Apply(rad, dokument, Nå);

        rad.ValidScientificName.Should().Be("<i>Letharia vulpina</i>");
    }

    [Fact]
    public void Faller_tilbake_paa_det_uformaterte_navnet()
    {
        var rad = new Taxon();

        TaxonomyMapping.Apply(rad, Dokument(), Nå);

        rad.ValidScientificName.Should().Be("Letharia vulpina");
    }

    /// <summary>
    /// Hierarkiene er <c>int[]</c> i dokumentet og <c>nvarchar(200)</c> i basen. Målt
    /// format i begge basene: komma uten mellomrom.
    /// </summary>
    [Fact]
    public void Skriver_hierarkiene_som_komma_uten_mellomrom()
    {
        var rad = new Taxon();

        TaxonomyMapping.Apply(rad, Dokument(), Nå);

        rad.TaxonIdHiarchy.Should().Be("47871,47869,47416");
        rad.ScientificNameIdHiarchy.Should().Be("71898,71896,71443");
    }

    /// <summary>
    /// Rangen skrives som tall. Målt i gullsettene: 22 er <c>species</c>, og 4 og 6 er
    /// <c>subphylum</c> og <c>class</c> — det var den forskjellen som flyttet «Anthozoa»
    /// mellom taksonomisnapshotene.
    /// </summary>
    [Fact]
    public void Skriver_rangen_som_tallet_fra_enumen()
    {
        var rad = new Taxon();

        TaxonomyMapping.Apply(rad, Dokument(), Nå);

        rad.TaxonRankId.Should().Be(22);
        ((int)TaxonomicCategory.Subphylum).Should().Be(4);
        ((int)TaxonomicCategory.Class).Should().Be(6);
    }

    /// <summary>
    /// <c>ExternalTaxonId</c> finnes ikke i Artskart 2, og
    /// <c>ObservationDetailsRepository</c> faller tilbake på <c>TaxonId</c> så lenge den
    /// er 0. Mappingen skal derfor la den være.
    /// </summary>
    [Fact]
    public void Roerer_ikke_ExternalTaxonId()
    {
        var rad = new Taxon();

        TaxonomyMapping.Apply(rad, Dokument(), Nå);

        rad.ExternalTaxonId.Should().Be(0);
    }

    [Fact]
    public void Setter_slettetidspunkt_bare_naar_kilden_sier_slettet()
    {
        var levende = new Taxon();
        TaxonomyMapping.Apply(levende, Dokument(), Nå);
        levende.IsDeleted.Should().BeFalse();
        levende.DeletedAt.Should().BeNull();

        var slettet = Dokument();
        slettet.IsDeleted = true;

        var rad = new Taxon();
        TaxonomyMapping.Apply(rad, slettet, Nå);
        rad.IsDeleted.Should().BeTrue();
        rad.DeletedAt.Should().Be(Nå);
    }

    /// <summary>
    /// RavenDB 3.5 lagrer enumer som strenger med standardoppsettet, men kan lagre dem
    /// som tall. Deserialiseringen må ta begge — her mot de faktiske
    /// serialiseringsinnstillingene, ikke en kopi av dem.
    /// </summary>
    [Theory]
    [InlineData("\"species\"", TaxonomicCategory.Species)]
    [InlineData("\"Species\"", TaxonomicCategory.Species)]
    [InlineData("\"class\"", TaxonomicCategory.Class)]
    [InlineData("22", TaxonomicCategory.Species)]
    [InlineData("4", TaxonomicCategory.Subphylum)]
    [InlineData("\"noe-helt-annet\"", TaxonomicCategory.NotSet)]
    [InlineData("9999", TaxonomicCategory.NotSet)]
    public void Leser_rangen_som_streng_eller_tall(string json, TaxonomicCategory forventet)
    {
        var dokument = JsonSerializer.Deserialize<TaxonInfoDocument>(
            $"{{\"Id\":1,\"CategoryValue\":{json}}}", TaxonomyJson.Options);

        dokument!.CategoryValue.Should().Be(forventet);
    }

    /// <summary>
    /// Feltnavnene i kilden har skrivefeil — <c>PrefferedPopularname</c>,
    /// <c>vernacularNameID</c> — og deserialiseringen må treffe dem, ikke de rettede
    /// navnene i Artskart 3s entiteter.
    /// </summary>
    [Fact]
    public void Treffer_kildens_feltnavn_inkludert_skrivefeilene()
    {
        const string json = """
            {
              "Id": 47871,
              "PrefferedPopularname": "ulvelav",
              "ScientificNames": [
                { "ScientificNameId": 71898, "Accepted": true,
                  "ScientificNameFormatted": "<i>Letharia vulpina</i>" }
              ],
              "PopularNames": [
                { "vernacularNameID": 25470, "language": "nb-NO",
                  "Name": "ulvelav", "Preffered": true }
              ]
            }
            """;

        var dokument = JsonSerializer.Deserialize<TaxonInfoDocument>(json, TaxonomyJson.Options);

        dokument!.PrefferedPopularname.Should().Be("ulvelav");
        dokument.ScientificNames.Should().ContainSingle()
            .Which.ScientificNameFormatted.Should().Be("<i>Letharia vulpina</i>");
        dokument.PopularNames.Should().ContainSingle()
            .Which.vernacularNameID.Should().Be(25470);
        dokument.PopularNames[0].Preffered.Should().BeTrue();
    }
}

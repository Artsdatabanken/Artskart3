using Artskart3.Core.Application.DTOs;
using Artskart3.Core.Application.Services;
using Artskart3.Core.Application.Services.Interfaces;
using FluentAssertions;

namespace Artskart3.Tests.Unit;

/// <summary>
/// Tester for fasett-registeret: hvilke dimensjoner som kan vises som fasett, og hvilket
/// bøttenivå et gitt filter skal leses fra.
/// </summary>
public class FacetCountCacheDimensionsTests
{
    private sealed class Taxa(Dictionary<int, int> ranks, Dictionary<int, List<int>>? ordersUnder = null)
        : ITaxonHierarchyService
    {
        public int? GetTaxonRankId(int taxonId) => ranks.TryGetValue(taxonId, out var r) ? r : null;
        public List<TaxonTreeNodeDto> GetChildren(int? parentTaxonId) => [];
        public List<int> GetDescendantSpeciesIds(int taxonId) => [];

        public List<int> GetDescendantIdsAtRank(int taxonId, int targetRankId) =>
            ordersUnder?.TryGetValue(taxonId, out var o) == true ? o : [];

        public List<TaxonAncestryDto> GetAncestries(IEnumerable<int> taxonIds) =>
            taxonIds.Select(id => new TaxonAncestryDto { Id = id, ParentIds = [] }).ToList();
    }

    /// <summary>
    /// Konteksten bygges fra observasjonsfilteret, ikke lokasjonsfilteret — det er det
    /// fasett-endepunktet tar imot, og testen holder den veien åpen.
    /// </summary>
    private static CacheDimensionContext Ctx(ObservationSearchFilterDto filter, ITaxonHierarchyService? taxa = null) =>
        new(filter, taxa ?? new Taxa([]), new StubAreaHierarchyService());

    private static FacetDimension Facet(string key) =>
        FacetCountCacheDimensions.FindDimension(key)!;

    private static FacetLookup Resolve(string facetKey, ObservationSearchFilterDto filter, ITaxonHierarchyService? taxa = null) =>
        FacetCountCacheDimensions.ResolveDimensionFacet(Facet(facetKey), Ctx(filter, taxa));

    // -----------------------------------------------------------------------
    // Registerets form — endres denne, må fasett-bufferen bygges på nytt
    // -----------------------------------------------------------------------

    [Fact]
    public void Fasettene_peker_paa_stabile_dimensjons_ider()
    {
        // Nøklene er API-kontrakt mot frontend, id-ene ligger i bufferdataene. Feiler
        // denne, er én av de to endret, og begge krever at noe annet endres i takt.
        FacetCountCacheDimensions.All.ToDictionary(f => f.Key, f => f.DimensionId)
            .Should().Equal(new Dictionary<string, byte>
            {
                ["taxonGroup"] = 7,
                ["category"] = 5,
                ["behavior"] = 3,
                ["basisOfRecord"] = 4,
                ["institution"] = 6,
                ["registrationStatus"] = 2,
            });
    }

    [Fact]
    public void Maskefasettene_peker_paa_stabile_bit()
    {
        FacetCountCacheDimensions.MaskFacets.ToDictionary(f => f.Key, f => f.Bit)
            .Should().Equal(new Dictionary<string, byte>
            {
                ["withImages"] = FilterMask.HasMediaFiles,
                ["sensitive"] = FilterMask.Sensitive,
                ["changedByAnnotation"] = FilterMask.ChangedByAnnotation,
                ["uncertainIdentification"] = FilterMask.UncertainIdentification,
                ["introduced"] = FilterMask.Introduced,
            });
    }

    [Fact]
    public void Alle_fasetter_er_enkeltverdi_dimensjoner()
    {
        // Et flerverdi-filter ville talt en observasjon én gang per verdi, og et
        // intervall har ingen liste å vise tall ved. Konstruktøren kaster, men denne
        // sier det som en egenskap ved registeret.
        FacetCountCacheDimensions.All.Should().OnlyContain(f => f.Dimension.Kind == CacheDimensionKind.SingleValue);
    }

    [Fact]
    public void Grupperingskolonnen_kommer_fra_dimensjonsregisteret()
    {
        // Ikke en egen kolonneliste her: to definisjoner av samme bøtte er det som gir
        // riktige tall for feil filter.
        FacetCountCacheDimensions.All.Select(f => f.GroupByColumn)
            .Should().Equal("TaxonGroupId", "CategoryId", "BehaviorId", "BasisOfRecordId",
                "InstitutionOrgId", "RegistrationStatusId");
    }

    [Fact]
    public void Skjemaversjonen_er_deterministisk()
    {
        // Verdien er pinnet med vilje. Feiler denne, har registeret endret form, og da må
        // fasett-bufferen bygges på nytt OG @SchemaVersion i
        // Scripts/BuildFacetCountCache.sql oppdateres. Uten det blir en nybygget buffer
        // ignorert uten at noe feiler — bare tregt.
        FacetCountCacheDimensions.SchemaVersion.Should().Be(229709519);
    }

    [Fact]
    public void Skjemaversjonen_arver_omraaderegisterets_form()
    {
        // Bøttene og pargruppe-id-ene er områdebufferens. Endres de, må fasett-bufferen
        // bygges om selv om ingenting i fasett-registeret er rørt. Testen under er en
        // påminnelse om koblingen; selve verdien dekkes av testen over.
        AreaCountCacheDimensions.SchemaVersion.Should().Be(723897205);
    }

    [Fact]
    public void Ukjent_noekkel_gir_null()
    {
        FacetCountCacheDimensions.FindDimension("fins-ikke").Should().BeNull();
        FacetCountCacheDimensions.FindMaskFacet("fins-ikke").Should().BeNull();
    }

    // -----------------------------------------------------------------------
    // Dimensjonsfasetter
    // -----------------------------------------------------------------------

    [Fact]
    public void Tomt_filter_leses_fra_level1()
    {
        var lookup = Resolve("category", new ObservationSearchFilterDto());

        lookup.Level.Should().Be(FacetLookupLevel.Level1);
        lookup.GroupByDimensionId.Should().Be(5);
        lookup.Constraints.Should().BeEmpty();
        lookup.PairId.Should().BeNull();
    }

    [Fact]
    public void Egen_dimensjon_begrenser_ikke_fasetten()
    {
        // Brukeren har krysset av to kategorier. Kategorifasetten skal likevel svare for
        // alle kategorier — tallet er «hva får du hvis du velger denne i stedet». Hadde
        // egen dimensjon telt, ville dette blitt Level2 mot seg selv.
        var lookup = Resolve("category", new ObservationSearchFilterDto { CategoryIds = [9, 12] });

        lookup.Level.Should().Be(FacetLookupLevel.Level1);
        lookup.Constraints.Should().BeEmpty();
    }

    [Fact]
    public void Ett_annet_filter_leses_fra_level2()
    {
        var lookup = Resolve("category", new ObservationSearchFilterDto { TaxonGroupIds = [8] });

        lookup.Level.Should().Be(FacetLookupLevel.Level2);
        lookup.GroupByDimensionId.Should().Be(5);
        lookup.PairId.Should().Be(AreaCountCacheDimensions.GetPairId(5, 7));
        lookup.Constraints.Should().HaveCount(1);
        lookup.Constraints[0].Dimension.Id.Should().Be(7);
        ((BucketSet)lookup.Constraints[0].Buckets).Values.Should().Equal(8);
    }

    [Fact]
    public void Pargruppen_er_den_samme_uansett_hvilken_side_fasetten_staar_paa()
    {
        // Samme par, speilvendt. Bufferen har én rad per par, så begge oppslagene må
        // treffe den samme — bare på motsatt bøttekolonne.
        var kategoriFasett = Resolve("category", new ObservationSearchFilterDto { TaxonGroupIds = [8] });
        var gruppeFasett = Resolve("taxonGroup", new ObservationSearchFilterDto { CategoryIds = [9] });

        gruppeFasett.PairId.Should().Be(kategoriFasett.PairId);
        kategoriFasett.GroupByIsBucketA.Should().BeTrue();   // kategori er id 5, taksongruppe 7
        gruppeFasett.GroupByIsBucketA.Should().BeFalse();
    }

    [Fact]
    public void To_andre_filtre_dekkes_ikke_av_dimensjonsfasetten()
    {
        // Fasetten bruker selv opp den ene siden av paret, så to andre ville krevd et
        // tredje bøttenivå. Her faller oppslaget til aggregering — som er raskt, nettopp
        // fordi tre filtre er selektive.
        var lookup = Resolve("category", new ObservationSearchFilterDto
        {
            TaxonGroupIds = [8],
            BasisOfRecordIds = [1],
        });

        lookup.Level.Should().Be(FacetLookupLevel.NotCovered);
    }

    [Fact]
    public void Omraadevalg_er_en_dimensjon_som_alle_andre()
    {
        var lookup = Resolve("category", new ObservationSearchFilterDto { CountyIds = ["50"] });

        lookup.Level.Should().Be(FacetLookupLevel.Level2);
        lookup.Constraints[0].Dimension.Id.Should().Be(12);
        lookup.PairId.Should().Be(AreaCountCacheDimensions.GetPairId(5, 12));
    }

    // -----------------------------------------------------------------------
    // Fall-through: filtre bufferen ikke kan svare på
    // -----------------------------------------------------------------------

    [Fact]
    public void Takson_uten_underliggende_taksa_dekkes_ikke()
    {
        // Bøtta er ordenen og teller alltid etterkommerne med. Ber filteret om bare det
        // valgte taksonet, finnes ingen bøtte som uttrykker det.
        var lookup = Resolve("category", new ObservationSearchFilterDto
        {
            TaxonIds = [1234],
            IncludeDescendantTaxa = false,
        });

        lookup.Level.Should().Be(FacetLookupLevel.NotCovered);
    }

    [Fact]
    public void Takson_under_ordensnivaa_dekkes_ikke()
    {
        var taxa = new Taxa(new Dictionary<int, int> { [1234] = 22 });   // art, under orden (11)

        var lookup = Resolve("category", new ObservationSearchFilterDto { TaxonIds = [1234] }, taxa);

        lookup.Level.Should().Be(FacetLookupLevel.NotCovered);
    }

    [Fact]
    public void Katalognummer_dekkes_ikke()
    {
        // ObservationIds er en vilkårlig liste fra typeahead, uten et endelig vokabular
        // å bøtte på. Samme forbehold som i AreaCountCacheService.
        var lookup = Resolve("category", new ObservationSearchFilterDto { ObservationIds = [1, 2, 3] });

        lookup.Level.Should().Be(FacetLookupLevel.NotCovered);
    }

    [Fact]
    public void Matrikkelenheter_dekkes_ikke()
    {
        var lookup = Resolve("category", new ObservationSearchFilterDto { CadastralUnitIds = ["1101-1/2"] });

        lookup.Level.Should().Be(FacetLookupLevel.NotCovered);
    }

    // -----------------------------------------------------------------------
    // Maskefasetter — de bruker ikke opp en bøttedimensjon og naar ett hakk lenger
    // -----------------------------------------------------------------------

    private static FacetLookup ResolveMask(string key, bool value, ObservationSearchFilterDto filter) =>
        FacetCountCacheDimensions.ResolveMaskFacet(FacetCountCacheDimensions.FindMaskFacet(key)!, value, Ctx(filter));

    [Fact]
    public void Maskefasett_uten_andre_filtre_leses_fra_level0()
    {
        var lookup = ResolveMask("withImages", true, new ObservationSearchFilterDto());

        lookup.Level.Should().Be(FacetLookupLevel.Level0);
        lookup.GroupByDimensionId.Should().BeNull();
        lookup.Constraints.Should().BeEmpty();
        lookup.Mask.Care.Should().Be(FilterMask.HasMediaFiles);
        lookup.Mask.Want.Should().Be(FilterMask.HasMediaFiles);
    }

    [Fact]
    public void Maskefasett_med_ett_filter_leses_fra_level1()
    {
        var lookup = ResolveMask("withImages", true, new ObservationSearchFilterDto { TaxonGroupIds = [8] });

        lookup.Level.Should().Be(FacetLookupLevel.Level1);
        lookup.Constraints.Should().HaveCount(1);
        lookup.PairId.Should().BeNull();
    }

    [Fact]
    public void Maskefasett_med_to_filtre_leses_fra_level2()
    {
        var lookup = ResolveMask("withImages", true, new ObservationSearchFilterDto
        {
            TaxonGroupIds = [8],
            CategoryIds = [9],
        });

        lookup.Level.Should().Be(FacetLookupLevel.Level2);
        lookup.PairId.Should().Be(AreaCountCacheDimensions.GetPairId(5, 7));

        // Sortert på dimensjons-id, slik at den første hører til BucketA.
        lookup.Constraints.Select(c => c.Dimension.Id).Should().Equal((byte)5, (byte)7);
    }

    [Fact]
    public void Maskefasett_med_tre_filtre_dekkes_ikke()
    {
        var lookup = ResolveMask("withImages", true, new ObservationSearchFilterDto
        {
            TaxonGroupIds = [8],
            CategoryIds = [9],
            BasisOfRecordIds = [1],
        });

        lookup.Level.Should().Be(FacetLookupLevel.NotCovered);
    }

    [Fact]
    public void Maskefasett_overstyrer_brukerens_eget_valg_paa_samme_bit()
    {
        // Brukeren har valgt «med bilde». Alternativet «uten bilde» skal likevel svare
        // for hvor mange som mangler bilde — ikke for null.
        var lookup = ResolveMask("withImages", false, new ObservationSearchFilterDto { WithImages = true });

        lookup.Mask.Care.Should().Be(FilterMask.HasMediaFiles);
        lookup.Mask.Want.Should().Be(0);
    }

    [Fact]
    public void Maskefasett_beholder_de_andre_bool_filtrene()
    {
        var lookup = ResolveMask("withImages", true, new ObservationSearchFilterDto
        {
            Sensitive = true,
            Introduced = false,
        });

        lookup.Mask.Care.Should().Be((byte)(FilterMask.HasMediaFiles | FilterMask.Sensitive | FilterMask.Introduced));
        lookup.Mask.Want.Should().Be((byte)(FilterMask.HasMediaFiles | FilterMask.Sensitive));
    }

    // -----------------------------------------------------------------------
    // FilterMask.WithBit
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void WithBit_setter_biten_uansett_hva_filteret_sa(bool value)
    {
        var utgangspunkt = FilterMask.Select(new ObservationSearchFilterDto { WithImages = !value });

        var resultat = FilterMask.WithBit(utgangspunkt, FilterMask.HasMediaFiles, value);

        resultat.Care.Should().Be(FilterMask.HasMediaFiles);
        resultat.Want.Should().Be(value ? FilterMask.HasMediaFiles : (byte)0);
    }

    [Fact]
    public void WithBit_roerer_ikke_de_andre_bitene()
    {
        var utgangspunkt = FilterMask.Select(new ObservationSearchFilterDto { Sensitive = true });

        var resultat = FilterMask.WithBit(utgangspunkt, FilterMask.Introduced, false);

        resultat.Care.Should().Be((byte)(FilterMask.Sensitive | FilterMask.Introduced));
        resultat.Want.Should().Be(FilterMask.Sensitive);
    }
}

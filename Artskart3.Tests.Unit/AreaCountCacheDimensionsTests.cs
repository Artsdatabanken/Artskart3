using System.Reflection;
using Artskart3.Core.Application.DTOs;
using Artskart3.Core.Application.Services;
using Artskart3.Core.Application.Services.Interfaces;
using FluentAssertions;

namespace Artskart3.Tests.Unit;

/// <summary>
/// Tester for dimensjonsregisteret til områdebufferen.
/// </summary>
public class AreaCountCacheDimensionsTests
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

    private static CacheDimensionContext Ctx(LocationSearchFilterDto filter, ITaxonHierarchyService? taxa = null) =>
        new(filter, taxa ?? new Taxa([]), new StubAreaHierarchyService());

    private static DimensionSelection Select(string name, LocationSearchFilterDto filter, ITaxonHierarchyService? taxa = null) =>
        AreaCountCacheDimensions.All.Single(d => d.Name == name).Select(Ctx(filter, taxa));

    private static int[] Values(DimensionSelection selection) =>
        ((BucketSet)selection.Buckets!).Values.ToArray();

    // -----------------------------------------------------------------------
    // Registerets form — endres denne, må bufferen bygges på nytt
    // -----------------------------------------------------------------------

    [Fact]
    public void Dimensjons_idene_er_stabile()
    {
        // Id-ene ligger i bufferdataene. Endres de, peker eksisterende rader på feil
        // dimensjon. Denne testen skal feile hvis noen omnummererer.
        AreaCountCacheDimensions.All.ToDictionary(d => d.Id, d => d.Name)
            .Should().Equal(new Dictionary<byte, string>
            {
                [2] = "Regstatus",
                [3] = "Atferd",
                [4] = "Funntype",
                [5] = "Kategori",
                [6] = "Institusjon",
                [7] = "Taksongruppe",
                [8] = "Takson",
                [9] = "Datasett",
                [10] = "Periode",
                [11] = "Koordpresisjon",
                [12] = "Geografi",
                [13] = "Prosjekt",
            });
    }

    [Fact]
    public void Tolv_dimensjoner_gir_sekstiseks_par()
    {
        AreaCountCacheDimensions.PairCount.Should().Be(66);
    }

    [Fact]
    public void Pargruppe_id_er_uavhengig_av_rekkefolge()
    {
        AreaCountCacheDimensions.GetPairId(10, 3).Should().Be(AreaCountCacheDimensions.GetPairId(3, 10));
    }

    [Fact]
    public void Pargruppe_idene_er_unike_og_dekker_en_til_sekstiseks()
    {
        var ids = AreaCountCacheDimensions.All;
        var pairIds = (from a in ids from b in ids where a.Id < b.Id
                       select AreaCountCacheDimensions.GetPairId(a.Id, b.Id)).ToArray();

        pairIds.Should().HaveCount(66);
        pairIds.Distinct().Should().HaveCount(66);
        pairIds.Should().OnlyContain(id => id >= 1 && id <= 66);
    }

    [Fact]
    public void Skjemaversjonen_er_deterministisk()
    {
        // Ikke string.GetHashCode: den er randomisert per prosess og ville gjort hver
        // omstart til en ny versjon — altså en buffer som aldri ble brukt.
        //
        // Verdien er pinnet med vilje. Feiler denne, har registeret endret form, og da
        // må bufferen bygges på nytt OG @SchemaVersion i Scripts/BuildAreaCountCache.sql
        // oppdateres. Uten det vil en nybygget buffer bli ignorert uten at noen skjønner
        // hvorfor — den feiler stille, bare tregt.
        AreaCountCacheDimensions.SchemaVersion.Should().Be(723897205);
    }

    // -----------------------------------------------------------------------
    // Tripwire: et nytt filter må enten bli en dimensjon eller bevisst utelates
    // -----------------------------------------------------------------------

    [Fact]
    public void Alle_filteregenskaper_er_gjort_rede_for()
    {
        // Egenskaper som ikke påvirker antallet i det hele tatt.
        string[] ikkeFilter = ["Epsg", "MaxResults", "Envelope", "HasActiveFilters", "HasObservationAttributeFilters"];

        // Filtre bufferen ikke dekker. Hver av dem MÅ ha en eksplisitt fall-through i
        // AreaCountCacheService, LocationCountCacheService og PolygonLocationStore —
        // bufferne velger bøtter ved å ramse opp kjente dimensjoner, så et filter som
        // ikke er en dimensjon blir aldri konsultert, og svaret blir raskt og for høyt.
        //
        // Katalognummer: ObservationIds er en vilkårlig liste uten endelig vokabular å
        // bøtte på, og trenger det ikke — id-ene er allerede slått opp, så filteret er
        // et seek.
        //
        // IncludeDescendantTaxa er ikke et filter for seg, men endrer hvordan TaxonIds
        // leses. Bøtta i Takson-dimensjonen er OrderTaxonId og teller alltid
        // etterkommerne med, så «bare de valgte taksa» kan ikke uttrykkes der —
        // SelectTaxonOrders svarer NotCacheable og tellingen tar det. Minnelageret
        // for polygoner treffer derimot eksakt på observasjonens eget takson.
        string[] bevisstUtenfor = ["ObservationIds", "IncludeDescendantTaxa"];

        // Bool-filtrene er ikke dimensjoner, men bit i FilterMask — en tredje nøkkel
        // på hver bufferrad. De koster derfor ingen dimensjonsplass, og to vanlige
        // filtre pluss hvilken som helst kombinasjon av disse besvares fortsatt fra
        // bufferen. Et nytt bool-filter hører hjemme her, ikke som egen dimensjon.
        string[] dekketAvMasken =
            ["WithImages", "ChangedByAnnotation", "Sensitive", "UncertainIdentification", "Introduced"];

        // Fire av disse hører til samme dimensjon: Geografi. Semi-joinen ORer kommune,
        // fylke, Svalbard, verneområde og havområde sammen, så de ER ett filter.
        string[] dekketAvDimensjon =
        [
            "RegistrationStatusId", "BehaviorIds", "BasisOfRecordIds", "CategoryIds",
            "OrganizationIds", "TaxonGroupIds", "TaxonIds", "DatasetOrgIds", "Period",
            "CoordinatePrecision", "ProjectOrgIds",
            "MunicipalityIds", "CountyIds", "OceanAreaIds", "RestrictedAreaIds",
        ];

        // Masken må dekke nøyaktig de fem bitene, ellers er et filter stille utelatt
        // fra bøttingen selv om det står i lista over.
        dekketAvMasken.Should().HaveCount(5,
            "FilterMask har fem bit, og hvert av dem må svare til ett filterfelt");

        typeof(LocationSearchFilterDto)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(p => p.Name)
            .Should().BeEquivalentTo([.. ikkeFilter, .. bevisstUtenfor, .. dekketAvMasken, .. dekketAvDimensjon],
                "et nytt filter må bli en dimensjon, et maskebit, eller føres opp som bevisst " +
                "utenfor — ellers ville bufferen svart som om filteret ikke var satt");
    }

    // -----------------------------------------------------------------------
    // Utvalg per dimensjon
    // -----------------------------------------------------------------------

    [Fact]
    public void Tomt_filter_gir_ingen_aktive_dimensjoner()
    {
        var ctx = Ctx(new LocationSearchFilterDto());
        AreaCountCacheDimensions.All.Select(d => d.Select(ctx))
            .Should().OnlyContain(s => !s.IsActive && s.IsCacheable);
    }

    [Fact]
    public void Tomt_filter_gir_inaktiv_maske()
    {
        // Ingen bool satt betyr at maskepredikatet utelates helt og cellens masker
        // summeres. Var den aktiv med Care = 0, ville (Mask & 0) == 0 vært sant for
        // alt — samme svar, men et unødvendig restledd på hver rad.
        FilterMask.Select(new LocationSearchFilterDto()).IsActive.Should().BeFalse();
    }

    [Theory]
    [InlineData(true, FilterMask.HasMediaFiles)]
    [InlineData(false, 0)]
    public void Bilder_setter_sitt_bit_i_masken(bool withImages, int forventetWant)
    {
        var maske = FilterMask.Select(new LocationSearchFilterDto { WithImages = withImages });

        maske.Care.Should().Be(FilterMask.HasMediaFiles);
        maske.Want.Should().Be((byte)forventetWant);
    }

    [Fact]
    public void Flere_boolfiltre_samles_i_ett_predikat()
    {
        // Poenget med masken: fire bool-filtre koster null dimensjonsplasser, der de
        // tidligere spiste fire og tvang oppslaget ned i databasen.
        var maske = FilterMask.Select(new LocationSearchFilterDto
        {
            ChangedByAnnotation = true,
            Sensitive = false,
            Introduced = true,
        });

        maske.Care.Should().Be(FilterMask.ChangedByAnnotation | FilterMask.Sensitive | FilterMask.Introduced);
        maske.Want.Should().Be(FilterMask.ChangedByAnnotation | FilterMask.Introduced);

        // Artsbestemmelse er ikke satt, så den biten skal stå fritt.
        (maske.Care & FilterMask.UncertainIdentification).Should().Be(0);
    }

    [Fact]
    public void Maskebitene_er_unike_og_dekker_fem_filtre()
    {
        byte[] bits =
        [
            FilterMask.ChangedByAnnotation, FilterMask.Sensitive,
            FilterMask.UncertainIdentification, FilterMask.Introduced, FilterMask.HasMediaFiles,
        ];

        bits.Distinct().Should().HaveCount(5, "to filtre på samme bit ville vært samme filter");
        bits.Aggregate(0, (acc, b) => acc | b).Should().Be(FilterMask.Count - 1,
            "bitene skal dekke maskerommet sammenhengende fra 1 til 16");
    }

    [Fact]
    public void Atferd_utenfor_tinyint_gir_tomt_sett_ikke_ufiltrert()
    {
        // (byte)257 ville blitt 1 ved unchecked cast. Da hadde kartet vist atferd 1 for
        // en forespørsel som ikke skulle truffet noe. Samme vakt som i ApplyCommonFilters.
        var selection = Select("Atferd", new LocationSearchFilterDto { BehaviorIds = [257, 300] });

        selection.IsActive.Should().BeTrue();
        selection.IsCacheable.Should().BeTrue();
        Values(selection).Should().BeEmpty();
    }

    [Fact]
    public void Atferd_beholder_verdier_innenfor_tinyint()
    {
        Values(Select("Atferd", new LocationSearchFilterDto { BehaviorIds = [2, 300, 3] })).Should().Equal(2, 3);
    }

    [Fact]
    public void Kategori_fjerner_duplikater()
    {
        Values(Select("Kategori", new LocationSearchFilterDto { CategoryIds = [12, 12, 14] })).Should().Equal(12, 14);
    }

    // -----------------------------------------------------------------------
    // Takson — kun ordensnivå
    // -----------------------------------------------------------------------

    [Fact]
    public void Takson_paa_ordensniva_velges_direkte()
    {
        var taxa = new Taxa(new Dictionary<int, int> { [500] = 11 });
        Values(Select("Takson", new LocationSearchFilterDto { TaxonIds = [500] }, taxa)).Should().Equal(500);
    }

    [Fact]
    public void Takson_over_ordensniva_loses_til_ordener()
    {
        var taxa = new Taxa(
            new Dictionary<int, int> { [7] = 6 },                       // klasse
            new Dictionary<int, List<int>> { [7] = [500, 501] });
        Values(Select("Takson", new LocationSearchFilterDto { TaxonIds = [7] }, taxa)).Should().BeEquivalentTo([500, 501]);
    }

    [Fact]
    public void Takson_under_ordensniva_kan_ikke_besvares_fra_bufferen()
    {
        // Art, slekt og familie finnes ikke som bøtter, og en orden ville vært for vid.
        // De ligger uansett i den raske enden — 725, 742 og 757 ms.
        var taxa = new Taxa(new Dictionary<int, int> { [9000] = 22 });
        var selection = Select("Takson", new LocationSearchFilterDto { TaxonIds = [9000] }, taxa);

        selection.IsActive.Should().BeTrue();
        selection.IsCacheable.Should().BeFalse();
    }

    [Fact]
    public void Takson_uten_underliggende_ordener_kan_ikke_besvares()
    {
        // F.eks. en liten rekke uten videre inndeling. Å la den falle bort stille ville
        // gitt ufiltrerte tall — samme feilen som Cephalorhyncha-testen fanget.
        var taxa = new Taxa(new Dictionary<int, int> { [40] = 3 });
        Select("Takson", new LocationSearchFilterDto { TaxonIds = [40] }, taxa).IsCacheable.Should().BeFalse();
    }

    [Fact]
    public void Ukjent_takson_kan_ikke_besvares()
    {
        Select("Takson", new LocationSearchFilterDto { TaxonIds = [123] }, new Taxa([])).IsCacheable.Should().BeFalse();
    }

    // -----------------------------------------------------------------------
    // Intervaller
    // -----------------------------------------------------------------------

    [Fact]
    public void Periode_oversettes_til_bokkeintervall()
    {
        var selection = Select("Periode", new LocationSearchFilterDto
        {
            Period = new PeriodDto { From = 2015, To = 2020 }
        });

        var range = (BucketRange)selection.Buckets!;
        range.From.Should().Be(201501);
        range.To.Should().Be(202012);
        range.Months.Should().BeNull();
    }

    [Fact]
    public void Periode_med_bare_maaneder_er_aktiv()
    {
        var selection = Select("Periode", new LocationSearchFilterDto
        {
            Period = new PeriodDto { Months = [6, 7] }
        });

        selection.IsActive.Should().BeTrue();
        var range = (BucketRange)selection.Buckets!;
        range.From.Should().BeNull();
        range.To.Should().BeNull();
        range.Months.Should().BeEquivalentTo([6, 7]);
    }

    [Fact]
    public void Koordpresisjon_med_bare_nedre_grense_holder_ovre_aapen()
    {
        var selection = Select("Koordpresisjon", new LocationSearchFilterDto
        {
            CoordinatePrecision = new CoordinatePrecisionDto { From = 100 }
        });

        var range = (BucketRange)selection.Buckets!;
        range.From.Should().Be(100);
        range.To.Should().BeNull();
    }

    [Fact]
    public void Tomt_periodeobjekt_er_ikke_aktivt()
    {
        Select("Periode", new LocationSearchFilterDto { Period = new PeriodDto() }).IsActive.Should().BeFalse();
    }

    // -----------------------------------------------------------------------
    // Flerverdi
    // -----------------------------------------------------------------------

    [Fact]
    public void Geografi_er_flerverdi_og_gir_medlemsider()
    {
        // BucketSet inneholder medlems-id-er, ikke bøtte-id-er. Oppslaget må slå dem opp
        // i medlemstabellen — en observasjon kan ligge i flere områder, og bøtta er hele
        // mengden den tilhører.
        var dimension = AreaCountCacheDimensions.All.Single(d => d.Name == "Geografi");
        dimension.Kind.Should().Be(CacheDimensionKind.MultiValue);

        var selection = dimension.Select(Ctx(new LocationSearchFilterDto
        {
            RestrictedAreaIds = ["Naturbase VV00000575", "Naturbase VV00000489"]
        }));

        Values(selection).Should().Equal(3_000_575, 3_000_489);
    }

    [Fact]
    public void Geografi_pakker_omraadetypen_inn_i_medlems_iden()
    {
        // Kommune 4206 og verneområde 4206 er ulike områder. Uten typen i id-en ville
        // de kollapset til samme medlem, og et kommunevalg ville truffet verneområdet.
        Values(Select("Geografi", new LocationSearchFilterDto { MunicipalityIds = ["4206"] }))
            .Should().Equal(1_004_206);

        Values(Select("Geografi", new LocationSearchFilterDto { OceanAreaIds = ["13"] }))
            .Should().Equal(4_000_013);
    }

    [Fact]
    public void Fylkevalg_treffer_ogsaa_Svalbard()
    {
        // Semi-joinen matcher EntityTypeId = 6 mot countyIds, ikke mot en egen liste.
        // Slår ikke oppslaget opp begge typene, svarer bufferen noe annet enn tellingen.
        Values(Select("Geografi", new LocationSearchFilterDto { CountyIds = ["42"] }))
            .Should().Equal(2_000_042, 6_000_042);
    }

    [Fact]
    public void Geografi_samler_alle_omraadetyper_i_ett_utvalg()
    {
        // Ett utvalg på tvers av typer, fordi semi-joinen ORer dem sammen. Det er
        // nettopp derfor de er én dimensjon og ikke fire.
        var selection = Select("Geografi", new LocationSearchFilterDto
        {
            MunicipalityIds = ["5001"],
            OceanAreaIds = ["13"],
            RestrictedAreaIds = ["Naturbase VV00000575"],
        });

        selection.IsActive.Should().BeTrue();
        Values(selection).Should().BeEquivalentTo([1_005_001, 4_000_013, 3_000_575]);
    }

    [Fact]
    public void Geografi_uten_omraadevalg_er_ikke_aktiv()
    {
        Select("Geografi", new LocationSearchFilterDto { TaxonGroupIds = [8] }).IsActive.Should().BeFalse();
    }

    /// <summary>
    /// Et områdevalg der ingen Fid kan slås opp er IKKE det samme som «ingen
    /// geografi valgt». Avgjorde dimensjonen på medlemslista, ville den svart at
    /// filteret ikke gjelder geografi, og bufferen hadde gitt tall for hele landet
    /// — mens tellingen gir tomt for samme filter. En aktiv dimensjon med tom
    /// bøttemengde er det ene svaret som er trygt.
    /// </summary>
    [Theory]
    [InlineData("county")]
    [InlineData("municipality")]
    [InlineData("restricted")]
    [InlineData("ocean")]
    public void Geografi_med_uoppslagbart_omraade_er_aktiv_men_tom(string type)
    {
        var filter = new LocationSearchFilterDto();
        switch (type)
        {
            case "county": filter.CountyIds = ["Nordland"]; break;
            case "municipality": filter.MunicipalityIds = ["Bodø"]; break;
            case "restricted": filter.RestrictedAreaIds = ["ikke en fid"]; break;
            case "ocean": filter.OceanAreaIds = ["Barentshavet"]; break;
        }

        var selection = Select("Geografi", filter);

        selection.IsActive.Should().BeTrue();
        selection.IsCacheable.Should().BeTrue();
        Values(selection).Should().BeEmpty();
    }

    [Fact]
    public void Prosjekt_er_flerverdi()
    {
        // 745 066 observasjoner har mer enn ett datasett, så prosjekt kan ikke være
        // en enkeltverdi-kolonne.
        AreaCountCacheDimensions.All.Single(d => d.Name == "Prosjekt")
            .Kind.Should().Be(CacheDimensionKind.MultiValue);
    }
}

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
                [1] = "Bilder",
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
    public void Tretten_dimensjoner_gir_syttiatte_par()
    {
        AreaCountCacheDimensions.PairCount.Should().Be(78);
    }

    [Fact]
    public void Pargruppe_id_er_uavhengig_av_rekkefolge()
    {
        AreaCountCacheDimensions.GetPairId(10, 3).Should().Be(AreaCountCacheDimensions.GetPairId(3, 10));
    }

    [Fact]
    public void Pargruppe_idene_er_unike_og_dekker_en_til_syttiatte()
    {
        var ids = AreaCountCacheDimensions.All;
        var pairIds = (from a in ids from b in ids where a.Id < b.Id
                       select AreaCountCacheDimensions.GetPairId(a.Id, b.Id)).ToArray();

        pairIds.Should().HaveCount(78);
        pairIds.Distinct().Should().HaveCount(78);
        pairIds.Should().OnlyContain(id => id >= 1 && id <= 78);
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
        AreaCountCacheDimensions.SchemaVersion.Should().Be(1194527040);
    }

    // -----------------------------------------------------------------------
    // Tripwire: et nytt filter må enten bli en dimensjon eller bevisst utelates
    // -----------------------------------------------------------------------

    [Fact]
    public void Alle_filteregenskaper_er_gjort_rede_for()
    {
        // Egenskaper som ikke påvirker antallet i det hele tatt.
        string[] ikkeFilter = ["Epsg", "MaxResults", "Envelope", "HasActiveFilters", "HasObservationAttributeFilters"];

        // Katalognummer er det eneste observasjonsfilteret bufferen ikke dekker.
        // ObservationIds er en vilkårlig liste uten endelig vokabular å bøtte på, og
        // trenger det ikke — id-ene er allerede slått opp, så filteret er et seek.
        string[] bevisstUtenfor = ["ObservationIds"];

        // Fire av disse hører til samme dimensjon: Geografi. Semi-joinen ORer kommune,
        // fylke, Svalbard, verneområde og havområde sammen, så de ER ett filter.
        string[] dekketAvDimensjon =
        [
            "WithImages", "RegistrationStatusId", "BehaviorIds", "BasisOfRecordIds", "CategoryIds",
            "OrganizationIds", "TaxonGroupIds", "TaxonIds", "DatasetOrgId", "Period",
            "CoordinatePrecision", "ProjectOrgId",
            "MunicipalityIds", "CountyIds", "OceanAreaIds", "RestrictedAreaIds",
        ];

        typeof(LocationSearchFilterDto)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(p => p.Name)
            .Should().BeEquivalentTo([.. ikkeFilter, .. bevisstUtenfor, .. dekketAvDimensjon],
                "et nytt filter må enten bli en dimensjon eller føres opp som bevisst utenfor — " +
                "ellers ville bufferen svart som om filteret ikke var satt");
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
    public void Bilder_blir_null_eller_en()
    {
        Values(Select("Bilder", new LocationSearchFilterDto { WithImages = true })).Should().Equal(1);
        Values(Select("Bilder", new LocationSearchFilterDto { WithImages = false })).Should().Equal(0);
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

    [Fact]
    public void Prosjekt_er_flerverdi()
    {
        // 745 066 observasjoner har mer enn ett datasett, så prosjekt kan ikke være
        // en enkeltverdi-kolonne.
        AreaCountCacheDimensions.All.Single(d => d.Name == "Prosjekt")
            .Kind.Should().Be(CacheDimensionKind.MultiValue);
    }
}

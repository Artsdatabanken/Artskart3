using Artskart3.Core.Application.DTOs;
using Artskart3.Core.Application.Services;
using Artskart3.Core.Application.Services.Implementations;
using Artskart3.Core.Application.Services.Interfaces;
using Artskart3.Core.Domain.RepositoryInterfaces;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Artskart3.Tests.Unit;

/// <summary>
/// Tester for kildevalget i fasett-tjenesten: bufferen først, aggregering som
/// fall-through, og at et ukjent navn koster ett tall og ikke hele svaret.
///
/// Aggregeringene går gjennom <see cref="IFacetAggregationRunner"/>, som kjører dem
/// samtidig på hver sin tilkobling. Tjenestens ansvar er å samle opp hvilke som trengs,
/// sende dem i én runde, og sette svarene tilbake på rett plass — det er det som testes
/// her, ikke selve parallelliteten.
/// </summary>
public class FacetCountServiceTests
{
    private readonly Mock<IFacetCountCacheService> _cache = new();
    private readonly Mock<ISearchRepository> _repository = new();
    private readonly Mock<IFacetAggregationRunner> _runner = new();

    private FacetCountService Sut() =>
        new(_cache.Object, _repository.Object, _runner.Object, NullLogger<FacetCountService>.Instance);

    private static ObservationSearchFilterDto Filter() => new();

    /// <summary>Bufferen svarer på alt, med de oppgitte tallene.</summary>
    private void CacheAnswers(Dictionary<int, int> counts)
    {
        _cache.Setup(c => c.TryGetDimensionCountsAsync(
                It.IsAny<FacetDimension>(), It.IsAny<IObservationFilter>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(counts);
    }

    /// <summary>Bufferen dekker ikke filteret.</summary>
    private void CacheMisses()
    {
        _cache.Setup(c => c.TryGetDimensionCountsAsync(
                It.IsAny<FacetDimension>(), It.IsAny<IObservationFilter>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Dictionary<int, int>?)null);

        _cache.Setup(c => c.TryGetMaskCountAsync(
                It.IsAny<FacetMaskOption>(), It.IsAny<bool>(), It.IsAny<IObservationFilter>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((int?)null);
    }

    // -----------------------------------------------------------------------
    // Kildevalget
    // -----------------------------------------------------------------------

    [Fact]
    public async Task Bufferen_brukes_naar_den_svarer_og_aggregeringen_roeres_ikke()
    {
        CacheAnswers(new Dictionary<int, int> { [8] = 42 });

        var svar = await Sut().GetFacetCountsAsync(Filter(), ["taxonGroup"], [], cancellationToken: CancellationToken.None);

        svar.Dimensions.Should().ContainSingle();
        svar.Dimensions[0].Key.Should().Be("taxonGroup");
        svar.Dimensions[0].Source.Should().Be("cache");
        svar.Dimensions[0].Buckets.Should().ContainSingle(b => b.Id == 8 && b.Count == 42);

        _runner.VerifyNoOtherCalls();

        // Matrikkeloppslaget er et kall mot Kartverket. Det skal ikke skje når
        // ingenting skal aggregeres.
        _repository.Verify(r => r.ResolveCadastralLocationIdsAsync(
            It.IsAny<IObservationFilter>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Null_fra_bufferen_faller_gjennom_til_aggregering()
    {
        CacheMisses();

        _runner.Setup(r => r.AggregateDimensionsAsync(
                It.IsAny<IObservationFilter>(), It.IsAny<IReadOnlyList<FacetDimension>>(),
                It.IsAny<FilterMask.Selection>(), It.IsAny<int[]?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([new Dictionary<int, int> { [5] = 7 }]);

        var svar = await Sut().GetFacetCountsAsync(Filter(), ["category"], [], cancellationToken: CancellationToken.None);

        svar.Dimensions[0].Source.Should().Be("aggregated");
        svar.Dimensions[0].Buckets.Should().ContainSingle(b => b.Id == 5 && b.Count == 7);
    }

    /// <summary>
    /// En TOM ordbok fra bufferen betyr «filteret treffer ingenting», ikke «vet ikke».
    /// Faller den gjennom til aggregering her, gjør vi et dyrt kall for å få det samme
    /// svaret — på et filter bufferen nettopp har sagt gir null treff.
    /// </summary>
    [Fact]
    public async Task Tom_ordbok_fra_bufferen_er_et_svar_og_aggregeres_ikke()
    {
        CacheAnswers([]);

        var svar = await Sut().GetFacetCountsAsync(Filter(), ["category"], [], cancellationToken: CancellationToken.None);

        svar.Dimensions[0].Source.Should().Be("cache");
        svar.Dimensions[0].Buckets.Should().BeEmpty();

        _runner.VerifyNoOtherCalls();
    }

    // -----------------------------------------------------------------------
    // Samling av aggregeringene
    // -----------------------------------------------------------------------

    /// <summary>
    /// Alle bommene skal sendes i ÉN runde, ikke én av gangen. Det er hele grunnen til
    /// at kjøreren finnes: sekvensielt ble kallet 9,4 s med seks åpne grupper.
    /// </summary>
    [Fact]
    public async Task Alle_bommene_sendes_i_en_runde()
    {
        CacheMisses();

        IReadOnlyList<FacetDimension>? sendte = null;
        _runner.Setup(r => r.AggregateDimensionsAsync(
                It.IsAny<IObservationFilter>(), It.IsAny<IReadOnlyList<FacetDimension>>(),
                It.IsAny<FilterMask.Selection>(), It.IsAny<int[]?>(), It.IsAny<CancellationToken>()))
            .Callback<IObservationFilter, IReadOnlyList<FacetDimension>, FilterMask.Selection, int[]?, CancellationToken>(
                (_, f, _, _, _) => sendte = f)
            .ReturnsAsync([new Dictionary<int, int> { [1] = 1 }, new Dictionary<int, int> { [2] = 2 }]);

        await Sut().GetFacetCountsAsync(Filter(), ["taxonGroup", "category"], [], cancellationToken: CancellationToken.None);

        _runner.Verify(r => r.AggregateDimensionsAsync(
            It.IsAny<IObservationFilter>(), It.IsAny<IReadOnlyList<FacetDimension>>(),
            It.IsAny<FilterMask.Selection>(), It.IsAny<int[]?>(), It.IsAny<CancellationToken>()), Times.Once);

        sendte!.Select(f => f.Key).Should().Equal("taxonGroup", "category");
    }

    /// <summary>
    /// Svaret skal beholde rekkefølgen klienten ba om, også når noen grupper kom fra
    /// bufferen og andre ble aggregert.
    /// </summary>
    [Fact]
    public async Task Rekkefoelgen_beholdes_naar_kildene_blandes()
    {
        // taksongruppe fra bufferen, kategori aggregert.
        _cache.Setup(c => c.TryGetDimensionCountsAsync(
                It.Is<FacetDimension>(f => f.Key == "taxonGroup"), It.IsAny<IObservationFilter>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<int, int> { [8] = 42 });

        _cache.Setup(c => c.TryGetDimensionCountsAsync(
                It.Is<FacetDimension>(f => f.Key == "category"), It.IsAny<IObservationFilter>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((Dictionary<int, int>?)null);

        _runner.Setup(r => r.AggregateDimensionsAsync(
                It.IsAny<IObservationFilter>(), It.IsAny<IReadOnlyList<FacetDimension>>(),
                It.IsAny<FilterMask.Selection>(), It.IsAny<int[]?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([new Dictionary<int, int> { [9] = 7 }]);

        var svar = await Sut().GetFacetCountsAsync(
            Filter(), ["taxonGroup", "category"], [], cancellationToken: CancellationToken.None);

        svar.Dimensions.Select(d => d.Key).Should().Equal("taxonGroup", "category");
        svar.Dimensions[0].Source.Should().Be("cache");
        svar.Dimensions[1].Source.Should().Be("aggregated");
        svar.Dimensions[1].Buckets.Should().ContainSingle(b => b.Id == 9 && b.Count == 7);
    }

    /// <summary>
    /// Matrikkeloppslaget går mot Kartverket. Det skal gjøres ÉN gang og deles, ikke
    /// én gang per aggregering.
    /// </summary>
    [Fact]
    public async Task Matrikkeloppslaget_gjoeres_en_gang_og_deles()
    {
        CacheMisses();

        _repository.Setup(r => r.ResolveCadastralLocationIdsAsync(
                It.IsAny<IObservationFilter>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([101, 102]);

        _runner.Setup(r => r.AggregateDimensionsAsync(
                It.IsAny<IObservationFilter>(), It.IsAny<IReadOnlyList<FacetDimension>>(),
                It.IsAny<FilterMask.Selection>(), It.IsAny<int[]?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([new Dictionary<int, int>(), new Dictionary<int, int>()]);

        _runner.Setup(r => r.AggregateTotalsAsync(
                It.IsAny<IObservationFilter>(), It.IsAny<IReadOnlyList<FilterMask.Selection>>(),
                It.IsAny<int[]?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([1, 2]);

        await Sut().GetFacetCountsAsync(
            Filter(), ["taxonGroup", "category"], ["withImages"], cancellationToken: CancellationToken.None);

        _repository.Verify(r => r.ResolveCadastralLocationIdsAsync(
            It.IsAny<IObservationFilter>(), It.IsAny<CancellationToken>()), Times.Once);

        _runner.Verify(r => r.AggregateDimensionsAsync(
            It.IsAny<IObservationFilter>(), It.IsAny<IReadOnlyList<FacetDimension>>(),
            It.IsAny<FilterMask.Selection>(), It.Is<int[]?>(ids => ids!.SequenceEqual(new[] { 101, 102 })),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    // -----------------------------------------------------------------------
    // Formen på svaret
    // -----------------------------------------------------------------------

    [Fact]
    public async Task Boetter_uten_treff_utelates_og_resten_sorteres()
    {
        CacheAnswers(new Dictionary<int, int> { [12] = 3, [4] = 0, [9] = 11 });

        var svar = await Sut().GetFacetCountsAsync(Filter(), ["category"], [], cancellationToken: CancellationToken.None);

        svar.Dimensions[0].Buckets.Select(b => b.Id).Should().Equal(9, 12);
    }

    [Fact]
    public async Task Gjentatte_navn_gir_ett_innslag()
    {
        CacheAnswers(new Dictionary<int, int> { [8] = 1 });

        var svar = await Sut().GetFacetCountsAsync(
            Filter(), ["taxonGroup", "taxonGroup"], [], cancellationToken: CancellationToken.None);

        svar.Dimensions.Should().ContainSingle();
    }

    [Fact]
    public async Task Ukjent_navn_hoppes_over_framfor_aa_velte_svaret()
    {
        // Klient og server kan være i utakt over en deploy. Da skal det koste ett
        // manglende tall, ikke en feilmelding for hele panelet.
        CacheAnswers(new Dictionary<int, int> { [8] = 1 });

        var svar = await Sut().GetFacetCountsAsync(
            Filter(), ["fins-ikke", "taxonGroup"], ["heller-ikke"], cancellationToken: CancellationToken.None);

        svar.Dimensions.Should().ContainSingle(d => d.Key == "taxonGroup");
        svar.BoolFilters.Should().BeEmpty();
    }

    // -----------------------------------------------------------------------
    // Bool-fasettene
    // -----------------------------------------------------------------------

    [Fact]
    public async Task Bool_fasetten_spoer_om_baade_paa_og_av()
    {
        _cache.Setup(c => c.TryGetMaskCountAsync(
                It.IsAny<FacetMaskOption>(), true, It.IsAny<IObservationFilter>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(30);

        _cache.Setup(c => c.TryGetMaskCountAsync(
                It.IsAny<FacetMaskOption>(), false, It.IsAny<IObservationFilter>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(70);

        var svar = await Sut().GetFacetCountsAsync(Filter(), [], ["withImages"], cancellationToken: CancellationToken.None);

        svar.BoolFilters.Should().ContainSingle();
        svar.BoolFilters[0].Key.Should().Be("withImages");
        svar.BoolFilters[0].WhenTrue.Should().Be(30);
        svar.BoolFilters[0].WhenFalse.Should().Be(70);
    }

    /// <summary>
    /// Fall-through for en bool-fasett må sende maskeutvalget med biten satt til
    /// alternativets verdi — ikke brukerens eget valg. Ellers ville «uten bilde» svart
    /// med brukerens «med bilde» og gitt null.
    /// </summary>
    [Fact]
    public async Task Bool_fall_through_overstyrer_brukerens_eget_valg_paa_samme_bit()
    {
        CacheMisses();

        IReadOnlyList<FilterMask.Selection>? sendteMasker = null;
        _runner.Setup(r => r.AggregateTotalsAsync(
                It.IsAny<IObservationFilter>(), It.IsAny<IReadOnlyList<FilterMask.Selection>>(),
                It.IsAny<int[]?>(), It.IsAny<CancellationToken>()))
            .Callback<IObservationFilter, IReadOnlyList<FilterMask.Selection>, int[]?, CancellationToken>(
                (_, m, _, _) => sendteMasker = m)
            .ReturnsAsync([11, 22]);

        // Brukeren har valgt «med bilde».
        var filter = new ObservationSearchFilterDto { WithImages = true };

        var svar = await Sut().GetFacetCountsAsync(filter, [], ["withImages"], cancellationToken: CancellationToken.None);

        // HiddenByAnnotation er alltid i Care: standardfilteret ekskluderer skjulte
        // observasjoner, så den biten har en påkrevd verdi selv uten at testen setter den.
        const byte medSkjult = FilterMask.HasMediaFiles | FilterMask.HiddenByAnnotation;

        sendteMasker.Should().HaveCount(2);
        sendteMasker![0].Care.Should().Be(medSkjult);
        sendteMasker[0].Want.Should().Be(FilterMask.HasMediaFiles);   // alternativet «med bilde»
        sendteMasker[1].Care.Should().Be(medSkjult);
        sendteMasker[1].Want.Should().Be(0);                          // alternativet «uten bilde»

        // Og svarene skal havne på rett alternativ.
        svar.BoolFilters[0].WhenTrue.Should().Be(11);
        svar.BoolFilters[0].WhenFalse.Should().Be(22);
    }

    /// <summary>
    /// Bufferen kan dekke det ene alternativet og ikke det andre — «med bilde» er en
    /// maske bufferen har, «uten bilde» like så, men dekningen avhenger av resten av
    /// filteret. Da skal bare bommen aggregeres.
    /// </summary>
    [Fact]
    public async Task Bare_alternativet_bufferen_bommer_paa_aggregeres()
    {
        _cache.Setup(c => c.TryGetMaskCountAsync(
                It.IsAny<FacetMaskOption>(), true, It.IsAny<IObservationFilter>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(30);

        _cache.Setup(c => c.TryGetMaskCountAsync(
                It.IsAny<FacetMaskOption>(), false, It.IsAny<IObservationFilter>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((int?)null);

        _runner.Setup(r => r.AggregateTotalsAsync(
                It.IsAny<IObservationFilter>(), It.IsAny<IReadOnlyList<FilterMask.Selection>>(),
                It.IsAny<int[]?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([70]);

        var svar = await Sut().GetFacetCountsAsync(Filter(), [], ["withImages"], cancellationToken: CancellationToken.None);

        svar.BoolFilters[0].WhenTrue.Should().Be(30);
        svar.BoolFilters[0].WhenFalse.Should().Be(70);
    }
}

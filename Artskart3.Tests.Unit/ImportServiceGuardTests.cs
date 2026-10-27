extern alias workers;

using Artskart3.Core.Application.Import;
using Artskart3.Core.Application.Import.Models;
using Artskart3.Core.Application.Import.Writing;
using Artskart3.Core.Application.Persistence;
using Artskart3.Core.Domain.Entities;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using ImportService = workers::Artskart3.Workers.Import.ImportService;

namespace Artskart3.Tests.Unit;

/// <summary>
/// Sperrene i <see cref="ImportService"/>: maskeringen som ikke finnes ennå, noden som
/// ikke står i registeret, og prosessmotor-Id-en som må finnes.
///
/// TESTPROSJEKTET REFERERER WORKERS MED ET EXTERN ALIAS, fordi både Api og Workers har
/// en <c>Program</c>-klasse fra toppnivåsetninger. Integrasjonsprosjektet gjør det
/// samme, av samme grunn.
/// </summary>
public class ImportServiceGuardTests
{
    /// <summary>
    /// Leser uten poster. Moq gir <c>null</c> for en metode som returnerer
    /// <c>IAsyncEnumerable</c>, og da kaster <c>await foreach</c> — så den lages for hånd.
    /// </summary>
    private sealed class TomLeser : ICacheRecordReader
    {
        public async IAsyncEnumerable<CacheReadItem> ReadNodeAsync(
            int nodeId,
            CacheReadOptions options,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
        {
            await Task.CompletedTask;

            yield break;
        }

        public Task<CacheNodeStatistics> GetNodeStatisticsAsync(
            int nodeId, CacheEntityStatus entityStatus, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<(int NodeId, int RecordCount)>> GetNodesAsync(
            CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private readonly Mock<IDataSourceRegistry> _register = new();
    private readonly Mock<IObservationWriter> _skriver = new();

    private static DataSourceConfig Konfig(bool sensitiv = false) =>
        new(43, "Testnode", sensitiv, Guid.Empty, null, ImportPropertyOverrides.None, false);

    /// <summary>
    /// En kontekst med én rad i <c>ProsessEngineHistory</c>, nok til at
    /// motor-Id-oppslaget går.
    /// </summary>
    private static IArtsKartDbContext Kontekst(bool medProsessmotor = true)
    {
        var options = new DbContextOptionsBuilder<Artskart3.Infrastructure.Data.ArtskartDbContext>()
            .UseInMemoryDatabase($"guard-{Guid.NewGuid()}")
            .Options;

        var context = new Artskart3.Infrastructure.Data.ArtskartDbContext(options);

        if (medProsessmotor)
        {
            // Id-en settes eksplisitt: kolonnen er ValueGeneratedNever, slik den er i
            // basen, så ingen provider deler den ut.
            context.Set<ProsessEngineHistory>().Add(new ProsessEngineHistory
            {
                Id = 4,
                Name = "test",
                Description = "test",
                PublishDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
            });

            context.SaveChanges();
        }

        return context;
    }

    private ImportService Tjeneste(ImportOptions innstillinger, IArtsKartDbContext? context = null) =>
        new(
            new TomLeser(),
            _register.Object,
            new ImportStepChainFactory(
                Mock.Of<IOrganizationRegistry>(),
                Mock.Of<IReferenceValueRegistry>(),
                Mock.Of<ITaxonNameResolver>(),
                Mock.Of<IGeographyService>(),
                Mock.Of<ICoordinateTransformer>(),
                Mock.Of<ICategoryLookup>()),
            _skriver.Object,
            Mock.Of<IOrganizationCreator>(),
            Mock.Of<IReferenceVariantWriter>(),
            context ?? Kontekst(),
            innstillinger,
            TimeProvider.System,
            NullLogger<ImportService>.Instance,
            NullLogger<ImportBatchProcessor>.Instance);

    /// <summary>
    /// Maskeringen av sensitive arter er fase 8. Uten den skriver importen eksakte
    /// koordinater for arter som skal maskeres, og planen gjør det til en sperre for
    /// produksjonssetting.
    /// </summary>
    [Fact]
    public async Task Nekter_a_kjore_uten_maskering()
    {
        _register.Setup(x => x.ByNodeIdAsync(43, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Konfig());

        var tjeneste = Tjeneste(new ImportOptions());

        var handling = async () => await tjeneste.ImportNodeAsync(43, null, CancellationToken.None);

        await handling.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*AllowImportWithoutMasking*");
    }

    [Fact]
    public async Task Kjorer_nar_sperren_er_slatt_av_med_vilje()
    {
        _register.Setup(x => x.ByNodeIdAsync(43, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Konfig());

        var tjeneste = Tjeneste(new ImportOptions { AllowImportWithoutMasking = true });

        var resultat = await tjeneste.ImportNodeAsync(43, null, CancellationToken.None);

        resultat.NodeId.Should().Be(43);
        resultat.Read.Should().Be(0, "leseren er en attrapp uten poster");
    }

    /// <summary>
    /// For en node der hele datasettet er sensitivt hjelper ikke flaggen: da er det
    /// ikke enkeltarter som skal maskeres, det er alt.
    /// </summary>
    [Fact]
    public async Task Nekter_alltid_a_kjore_en_sensitiv_node()
    {
        _register.Setup(x => x.ByNodeIdAsync(43, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Konfig(sensitiv: true));

        var tjeneste = Tjeneste(new ImportOptions { AllowImportWithoutMasking = true });

        var handling = async () => await tjeneste.ImportNodeAsync(43, null, CancellationToken.None);

        await handling.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*merket sensitiv*");
    }

    [Fact]
    public async Task Nekter_a_kjore_en_node_som_ikke_star_i_registeret()
    {
        _register.Setup(x => x.ByNodeIdAsync(99, It.IsAny<CancellationToken>()))
            .ReturnsAsync((DataSourceConfig?)null);

        var tjeneste = Tjeneste(new ImportOptions { AllowImportWithoutMasking = true });

        var handling = async () => await tjeneste.ImportNodeAsync(99, null, CancellationToken.None);

        await handling.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*finnes ikke i DataSource*");
    }

    /// <summary>
    /// Uten prosessmotor-Id kan ingen post merkes med hvilken versjon av tolkningen som
    /// skrev den, og hash-sjekken i fase 9 ville hoppet over alt (plan 4.7.3).
    /// </summary>
    [Fact]
    public async Task Nekter_a_kjore_uten_prosessmotor()
    {
        _register.Setup(x => x.ByNodeIdAsync(43, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Konfig());

        var tjeneste = Tjeneste(
            new ImportOptions { AllowImportWithoutMasking = true },
            Kontekst(medProsessmotor: false));

        var handling = async () => await tjeneste.ImportNodeAsync(43, null, CancellationToken.None);

        await handling.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*ProsessEngineHistory er tom*");
    }

    [Fact]
    public async Task Kjorer_ingen_noder_nar_registeret_er_tomt()
    {
        _register.Setup(x => x.ActiveNodesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var resultat = await Tjeneste(new ImportOptions()).ImportActiveNodesAsync(CancellationToken.None);

        resultat.Should().BeEmpty();
    }

    /// <summary>
    /// En node som feiler skal ikke stoppe de andre — feilhåndteringen per node er
    /// fase 9, men kjøringen skal komme gjennom lista.
    /// </summary>
    [Fact]
    public async Task En_node_som_feiler_stopper_ikke_de_andre()
    {
        _register.Setup(x => x.ByNodeIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Konfig());
        _register.Setup(x => x.ByNodeIdAsync(2, It.IsAny<CancellationToken>()))
            .ReturnsAsync((DataSourceConfig?)null);
        _register.Setup(x => x.ByNodeIdAsync(3, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Konfig());

        var tjeneste = Tjeneste(new ImportOptions
        {
            AllowImportWithoutMasking = true,
            Nodes = [1, 2, 3]
        });

        var resultat = await tjeneste.ImportActiveNodesAsync(CancellationToken.None);

        resultat.Should().HaveCount(2, "node 2 finnes ikke i registeret");
        resultat.Select(x => x.NodeId).Should().Equal(1, 3);
    }
}

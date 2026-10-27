using Artskart3.Core.Application.Import;
using Artskart3.Core.Application.Import.Steps;
using Artskart3.Core.Application.Import.Writing;
using FluentAssertions;
using Moq;

namespace Artskart3.Tests.Unit;

public class ImportStepChainFactoryTests
{
    private readonly ImportStepChainFactory _fabrikk = new(
        Mock.Of<IOrganizationRegistry>(),
        Mock.Of<IReferenceValueRegistry>(),
        Mock.Of<ITaxonNameResolver>(),
        Mock.Of<IGeographyService>(),
        Mock.Of<ICoordinateTransformer>(),
        Mock.Of<ICategoryLookup>());

    /// <summary>
    /// Rekkefølgen er Artskart 2s listerekkefølge, og den er tolkning: flere steg leser
    /// det et tidligere steg skrev.
    /// </summary>
    [Fact]
    public void Bygger_stegene_i_artskart_2s_rekkefolge()
    {
        var kjede = _fabrikk.Create();

        kjede.Steps.Select(x => x.GetType()).Should().Equal(
            typeof(ImportRecordIdStep),
            typeof(ImportCheckUniqueIdStep),
            typeof(ImportBasisOfRecordStep),
            typeof(ImportBehaviorStep),
            typeof(ImportTaxonomyStep),
            typeof(ImportGeometryStep),
            typeof(ImportSimplePropertiesStep),
            typeof(ImportMediaStep),
            typeof(ImportStatusForRegionStep),
            typeof(ImportCheckValidDatesStep));
    }

    /// <summary>
    /// Identiteten må være satt før noe annet kan slås opp, og en slettet post kjører
    /// bare dette steget.
    /// </summary>
    [Fact]
    public void Identitetssteget_er_forst_og_er_markert()
    {
        var kjede = _fabrikk.Create();

        kjede.Steps[0].Should().BeOfType<ImportRecordIdStep>().And.BeAssignableTo<IImportIdentityStep>();
    }

    [Fact]
    public void Bygger_en_ny_kjede_hver_gang()
    {
        _fabrikk.Create().Should().NotBeSameAs(_fabrikk.Create());
    }

    [Fact]
    public void Steget_som_oppretter_organisasjoner_far_oppretteren()
    {
        var kjede = _fabrikk.Create(organizations: Mock.Of<IOrganizationCreator>());

        kjede.Steps.OfType<ImportRecordIdStep>().Single().CanCreate.Should().BeTrue();
    }

    /// <summary>
    /// Uten oppretter kjører importen i opprett-ikke-modus (plan 4.8).
    /// </summary>
    [Fact]
    public void Uten_oppretter_er_det_opprett_ikke_modus()
    {
        var kjede = _fabrikk.Create();

        kjede.Steps.OfType<ImportRecordIdStep>().Single().CanCreate.Should().BeFalse();
    }

    [Fact]
    public void Kjeden_har_de_tre_fasene_fordelt_riktig()
    {
        var kjede = _fabrikk.Create();

        kjede.Steps.Count(x => x.StepType == ImportStepType.Pre).Should().Be(5);
        kjede.Steps.Count(x => x.StepType == ImportStepType.Parallel).Should().Be(2);
        kjede.Steps.Count(x => x.StepType == ImportStepType.Post).Should().Be(3);
    }
}

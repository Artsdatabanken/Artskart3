using Artskart3.Core.Application.Import.Steps;

namespace Artskart3.Core.Application.Import.Writing;

/// <summary>
/// Bygger stegkjeden for én nodekjøring.
///
/// REKKEFØLGEN ER TOLKNING, IKKE OPPSETT. Den er listerekkefølgen i Artskart 2s
/// <c>ImportServiceTasksProducer.CreateImportServiceTasks()</c>, og flere steg leser
/// det et tidligere steg skrev:
///
/// <list type="bullet">
/// <item><c>ImportCheckUniqueId</c> leser <c>ProxyId</c> fra <c>ImportRecordId</c>.</item>
/// <item><c>ImportStatusForRegion</c> leser taksonegenskapene fra
/// <c>ImportTaxonomy</c> og områdene fra <c>ImportGeometry</c>.</item>
/// <item><c>ImportSimpleProperties</c> kopierer <c>record.Notes</c>, som
/// <c>ImportBasisOfRecord</c>, <c>ImportBehavior</c> og <c>ImportGeometry</c> har
/// skrevet til.</item>
/// </list>
///
/// Fasetypen på hvert steg avgjør når det faktisk kjører, så listerekkefølgen gjelder
/// innenfor hver fase — se <see cref="ImportStepType"/>.
///
/// EN NY KJEDE PER NODE. <c>ImportCheckUniqueIdStep</c> husker identitetene den har
/// sett, og <c>ImportRecordIdStep</c> husker organisasjonene det har opprettet. Begge
/// hører til én nodekjøring.
///
/// TO STEG MANGLER ENNÅ: <c>ImportCheckSensitiveSpecies</c> (fase 8) og
/// <c>ImportLocationStore</c>, som er erstattet av at <see cref="ImportBatchProcessor"/>
/// skriver lokalitetene som del av runden.
/// </summary>
public class ImportStepChainFactory
{
    private readonly IOrganizationRegistry _organisasjoner;
    private readonly IReferenceValueRegistry _referanseverdier;
    private readonly ITaxonNameResolver _navneoppslag;
    private readonly IGeographyService _geografi;
    private readonly ICoordinateTransformer _transformator;
    private readonly ICategoryLookup _kategorier;
    private readonly TimeProvider _klokke;

    public ImportStepChainFactory(
        IOrganizationRegistry organisasjoner,
        IReferenceValueRegistry referanseverdier,
        ITaxonNameResolver navneoppslag,
        IGeographyService geografi,
        ICoordinateTransformer transformator,
        ICategoryLookup kategorier,
        TimeProvider? klokke = null)
    {
        _organisasjoner = organisasjoner;
        _referanseverdier = referanseverdier;
        _navneoppslag = navneoppslag;
        _geografi = geografi;
        _transformator = transformator;
        _kategorier = kategorier;
        _klokke = klokke ?? TimeProvider.System;
    }

    /// <summary>
    /// Kjeden for én nodekjøring.
    /// </summary>
    /// <param name="organizations">
    /// Oppretteren for ukjente koder, eller <c>null</c> for opprett-ikke-modus — se
    /// <see cref="IOrganizationCreator"/>.
    /// </param>
    /// <param name="variants">
    /// Skriveren for ukjente funntyper og atferder, eller <c>null</c>. Utfallet for
    /// posten er det samme uansett; listen er bokføring.
    /// </param>
    public ImportRecordProcessor Create(
        IOrganizationCreator? organizations = null,
        IReferenceVariantWriter? variants = null) =>
        new(
        [
            new ImportRecordIdStep(_organisasjoner, organizations),
            new ImportCheckUniqueIdStep(),
            new ImportBasisOfRecordStep(_referanseverdier, variants),
            new ImportBehaviorStep(_referanseverdier, variants),
            new ImportTaxonomyStep(_navneoppslag),
            new ImportGeometryStep(_geografi, _transformator),
            new ImportSimplePropertiesStep(),
            new ImportMediaStep(),
            new ImportStatusForRegionStep(_kategorier),
            new ImportCheckValidDatesStep(_klokke)
        ]);
}

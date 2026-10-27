namespace Artskart3.Workers.Taxonomy;

/// <summary>
/// Oppsett for speilingen av taksonomien fra Artskart 2s RavenDB.
///
/// MIDLERTIDIG MED VILJE. Hele denne mappen forsvinner når harvest-delen flytter
/// dokumentene til SQL Server. Innstillingene er derfor holdt enkle og samlet, ikke
/// spredt i konfigurasjonen.
/// </summary>
public sealed class TaxonomyOptions
{
    /// <summary>RavenDB-serveren, for eksempel <c>http://localhost:8080/</c>.</summary>
    public string BaseAddress { get; init; } = "http://localhost:8080/";

    /// <summary>
    /// Databasen. Artskart 2 bruker <c>Artskart2Core</c>; riggen skal peke på den
    /// frosne kopien, ikke den i drift — importen i Artskart 2 SKRIVER til RavenDB.
    /// </summary>
    public string Database { get; init; } = "Artskart2Core";

    /// <summary>Samlingen med taksoner. Dokument-id-ene er <c>TaxonInfos/{id}</c>.</summary>
    public string TaxonCollection { get; init; } = "TaxonInfos";

    public string SpeciesGroupCollection { get; init; } = "SpeciesGroups";

    /// <summary>
    /// Bruk indeksspørringen i stedet for strømmen. Strømmen er riktig vei for en hel
    /// samling, men Artskart 2 leser gjennom <c>Raven/DocumentsByEntityName</c>, og det
    /// er reserveveien om strømmeendepunktet ikke svarer som forventet.
    /// </summary>
    public bool UseIndexQuery { get; init; }

    /// <summary>Sidestørrelse for indeksspørringen. Brukes ikke av strømmen.</summary>
    public int PageSize { get; init; } = 1024;

    /// <summary>
    /// Hopp over taksoner kilden har merket slettet.
    ///
    /// PÅ SOM STANDARD, fordi Artskart 2s indeks ikke har dem — malt: 0 slettede
    /// taksoner og 0 navn som peker pa et slettet takson. Slas den av, overskriver de
    /// slettede dokumentene navnenes takson og Accepted-flagg.
    /// </summary>
    public bool SkipDeletedTaxa { get; init; } = true;

    /// <summary>
    /// Hvor mange rader som skrives per lagring. Artskart 2 bruker 1 000 for taksoner,
    /// og det er beholdt.
    /// </summary>
    public int BatchSize { get; init; } = 1000;
}

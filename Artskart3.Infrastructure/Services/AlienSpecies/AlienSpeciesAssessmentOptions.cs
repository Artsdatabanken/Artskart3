namespace Artskart3.Infrastructure.Services.AlienSpecies;

public sealed class AlienSpeciesAssessmentOptions
{
    public const string SectionName = "AlienSpeciesAssessment";

    public bool Enabled { get; set; } = true;

    public string BaseUrl { get; set; } = "https://lister.artsdatabanken.no/odata/v1/";

    public string CurrentPath { get; set; } =
        "AlienSpeciesAssessment?$select=id,area,taxonId,category";

    public string Snapshot2023Path { get; set; } =
        "alienspeciesassessment2023?$select=id,evaluationContext,scientificName,category";

    public int TimeoutSeconds { get; set; } = 30;

    public int RetryCount { get; set; } = 3;

    public int MaxRetryDelaySeconds { get; set; } = 10;

    public int MaxPageCount { get; set; } = 1000;

    public int MinimumCurrentAssessmentCount { get; set; } = 1;

    public string CategoryTypeName { get; set; } = "Fremmedart";

    public string CurrentAssessmentUrlPrefix { get; set; } =
        "https://lister.artsdatabanken.no/arter/fremmedartslista/";

    public bool ForceSnapshot2023Refresh { get; set; }
}

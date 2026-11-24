using System.Text.Json.Serialization;
using Artskart3.Core.Domain.Enums;

namespace Artskart3.Infrastructure.Services.AlienSpecies;

public sealed record CurrentAlienSpeciesAssessment(
    [property: JsonPropertyName("id")] int Id,
    [property: JsonPropertyName("area")] string Area,
    [property: JsonPropertyName("taxonId")] int TaxonId,
    [property: JsonPropertyName("category")] string Category);

public sealed record AlienSpeciesAssessment2023(
    [property: JsonPropertyName("id")] int Id,
    [property: JsonPropertyName("evaluationContext")] string EvaluationContext,
    [property: JsonPropertyName("scientificName")] AlienSpeciesScientificName ScientificName,
    [property: JsonPropertyName("category")] string Category);

public sealed record AlienSpeciesScientificName(
    [property: JsonPropertyName("scientificNameId")] int ScientificNameId);

public sealed record NormalizedTaxonAssessment(
    int SourceTaxonIdentifier,
    string Area,
    string CategoryCode,
    string ExternalAssessmentId,
    TaxonAssessmentSource Source);

public sealed record TaxonAssessmentFetchResult<T>(IReadOnlyList<T> Items, int PageCount);

public sealed record ODataPage<T>(
    [property: JsonPropertyName("value")] List<T> Value,
    [property: JsonPropertyName("@odata.nextLink")] string? NextLink);

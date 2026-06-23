using System.Net.Http.Json;
using System.Text.Json;
using Artskart3.Core.Application.DTOs;
using Artskart3.Core.Application.ExternalModels;
using Artskart3.Core.Application.Configuration;
using Artskart3.Core.Application.Services.Interfaces;
using Microsoft.Extensions.Options;

namespace Artskart3.Core.Application.Services.Implementations;

public class SpeciesService : ISpeciesService
{
    private readonly HttpClient _httpClient;
    private readonly NorTaxaOptions _options;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public SpeciesService(HttpClient httpClient, IOptions<NorTaxaOptions> options)
    {
        _httpClient = httpClient;
        _options = options.Value;
    }

    public async Task<List<SpeciesDto>> SearchSpeciesAsync(string searchInput, CancellationToken cancellationToken = default)
    {
        if (int.TryParse(searchInput, out var taxonId) && taxonId > 0)
        {
            return await SearchByTaxonIdAsync(taxonId, cancellationToken);
        }

        return await SearchByNameAsync(searchInput, cancellationToken);
    }

    private async Task<List<SpeciesDto>> SearchByNameAsync(string name, CancellationToken cancellationToken)
    {
        var encoded = Uri.EscapeDataString(name);
        var url = $"api/v1/TaxonName/Search?Search={encoded}&MaxResults={_options.MaxResults}";

        using var response = await _httpClient.GetAsync(url, cancellationToken);

        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            return [];

        response.EnsureSuccessStatusCode();

        var results = await response.Content.ReadFromJsonAsync<List<NorTaxaSearchResult>>(JsonOptions, cancellationToken);
        return results?.Select(MapFromSearchResult).ToList() ?? [];
    }

    private async Task<List<SpeciesDto>> SearchByTaxonIdAsync(int taxonId, CancellationToken cancellationToken)
    {
        var url = $"api/v1/TaxonName/ByTaxonId/{taxonId}";

        using var response = await _httpClient.GetAsync(url, cancellationToken);

        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            return [];

        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<NorTaxaByTaxonIdResult>(JsonOptions, cancellationToken);
        if (result == null) return [];

        return [MapFromByTaxonIdResult(result)];
    }

    private static SpeciesDto MapFromSearchResult(NorTaxaSearchResult r) => new()
    {
        TaxonId = r.TaxonId,
        ScientificName = r.AcceptedScientificName.Name,
        ScientificNameFormatted = r.AcceptedScientificName.NameFormatted,
        Author = r.AcceptedScientificName.Author,
        Rank = r.AcceptedScientificName.Rank,
        PreferredVernacularNames = r.PreferredVernacularNames.Select(v => new VernacularNameDto
        {
            Name = v.Name,
            Language = v.NameLanguageIso
        }).ToList(),
        VernacularNameSynonyms = r.VernacularNameSynonyms.Select(v => new VernacularNameDto
        {
            Name = v.Name,
            Language = v.NameLanguageIso
        }).ToList(),
        ScientificNameSynonyms = r.ScientificNameSynonyms.Select(s => new ScientificNameSynonymDto
        {
            Name = s.Name,
            NameFormatted = s.NameFormatted,
            Author = s.Author,
            Rank = s.Rank
        }).ToList()
    };

    private static SpeciesDto MapFromByTaxonIdResult(NorTaxaByTaxonIdResult r)
    {
        var accepted = r.ScientificNames.FirstOrDefault(s => s.TaxonomicStatus == "Accepted");
        if (accepted == null) return new SpeciesDto { TaxonId = r.TaxonId };

        var synonyms = r.ScientificNames.Where(s => s.TaxonomicStatus == "Synonym");
        var recommendedVernaculars = r.VernacularNames.Where(v => v.VernacularNameStatus == "Recommended");
        var notRecommendedVernaculars = r.VernacularNames.Where(v => v.VernacularNameStatus != "Recommended");

        return new SpeciesDto
        {
            TaxonId = r.TaxonId,
            ScientificName = accepted.ScientificNamePresentation,
            ScientificNameFormatted = accepted.ScientificNamePresentationHtml,
            Author = accepted.ScientificNameAuthorship,
            Rank = accepted.TaxonRank,
            PreferredVernacularNames = recommendedVernaculars.Select(v => new VernacularNameDto
            {
                Name = v.VernacularName,
                Language = v.LanguageIsoCode
            }).ToList(),
            VernacularNameSynonyms = notRecommendedVernaculars.Select(v => new VernacularNameDto
            {
                Name = v.VernacularName,
                Language = v.LanguageIsoCode
            }).ToList(),
            ScientificNameSynonyms = synonyms.Select(s => new ScientificNameSynonymDto
            {
                Name = s.ScientificNamePresentation,
                NameFormatted = s.ScientificNamePresentationHtml,
                Author = s.ScientificNameAuthorship,
                Rank = s.TaxonRank
            }).ToList()
        };
    }
}

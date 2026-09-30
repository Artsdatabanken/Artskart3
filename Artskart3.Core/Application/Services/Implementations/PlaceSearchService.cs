using System.Net.Http.Json;
using System.Text.Json;
using Artskart3.Core.Application.Configuration;
using Artskart3.Core.Application.DTOs;
using Artskart3.Core.Application.ExternalModels;
using Artskart3.Core.Application.Services.Interfaces;
using Microsoft.Extensions.Options;

namespace Artskart3.Core.Application.Services.Implementations;

public class PlaceSearchService : IPlaceSearchService
{
    private readonly HttpClient _httpClient;
    private readonly GeonorgeOptions _options;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public PlaceSearchService(HttpClient httpClient, IOptions<GeonorgeOptions> options)
    {
        _httpClient = httpClient;
        _options = options.Value;
    }

    public async Task<List<PlaceSearchResultDto>> SearchPlacesAsync(string searchInput, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(searchInput))
            return [];

        var encoded = Uri.EscapeDataString(searchInput.Trim());
        var url = $"sted?sok={encoded}&treffPerSide={_options.MaxResults}&utkoordsys={_options.OutputCoordinateSystem}";

        using var response = await _httpClient.GetAsync(url, cancellationToken);

        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            return [];

        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<GeonorgeStedResponse>(JsonOptions, cancellationToken);
        return result?.Navn.Select(MapFromGeonorgeSted).ToList() ?? [];
    }

    private static PlaceSearchResultDto MapFromGeonorgeSted(GeonorgeSted sted) => new()
    {
        StedsNummer = sted.Stedsnummer,
        Name = sted.Stedsnavn.FirstOrDefault(n => n.Navnestatus == "hovednavn")?.Skrivemate
            ?? sted.Stedsnavn.FirstOrDefault()?.Skrivemate
            ?? string.Empty,
        NavneObjektType = sted.Navneobjekttype ?? string.Empty,
        RecommendedZoom = NavneTyper.GetLevelForType(sted.Navneobjekttype),
        East = sted.Representasjonspunkt?.Ost ?? 0,
        North = sted.Representasjonspunkt?.Nord ?? 0,
        CoordinateSystem = sted.Representasjonspunkt?.Koordsys ?? 0,
        Municipalities = sted.Kommuner.Select(k => k.Kommunenavn ?? string.Empty).Where(n => n.Length > 0).ToList(),
        Counties = sted.Fylker.Select(f => f.Fylkesnavn ?? string.Empty).Where(n => n.Length > 0).ToList(),
        AlternativeNames = sted.Stedsnavn
            .Where(n => n.Navnestatus != "hovednavn")
            .Select(n => new PlaceNameAlternativeDto { Name = n.Skrivemate ?? string.Empty, Language = n.Sprak })
            .Where(a => a.Name.Length > 0)
            .ToList()
    };
}

using Artskart3.Core.Application.DTOs;

namespace Artskart3.Core.Application.Services.Interfaces;

/// <summary>
/// Oppslag av matrikkelenheter (gårds- og bruksnummer) mot Kartverkets Eiendom API.
/// </summary>
public interface ICadastralUnitService
{
    /// <summary>
    /// Henter matrikkelenheten med omriss, eller null hvis Kartverket ikke kjenner den.
    /// </summary>
    Task<CadastralUnitDto?> GetAsync(string id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lokasjonene som ligger innenfor minst én av matrikkelenhetene.
    /// </summary>
    Task<int[]> GetLocationIdsAsync(IEnumerable<string> ids, CancellationToken cancellationToken = default);
}

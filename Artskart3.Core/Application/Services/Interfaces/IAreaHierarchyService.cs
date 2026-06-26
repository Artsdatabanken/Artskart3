namespace Artskart3.Core.Application.Services.Interfaces;

/// <summary>
/// Oppstartslastet oppslag for områdehierarki (kommune→fylke) og Fid→EntityId-konvertering.
/// Data lastes én gang fra Area-tabellen og holdes i minne.
/// </summary>
public interface IAreaHierarchyService
{
    /// <summary>
    /// Returnerer fylkes-Fid for en gitt kommune-Fid, eller null hvis ukjent.
    /// </summary>
    string? GetCountyFid(string municipalityFid);

    /// <summary>
    /// Returnerer alle kommune-Fid-er som tilhører et gitt fylke.
    /// </summary>
    IReadOnlyList<string> GetMunicipalityFids(string countyFid);

    /// <summary>
    /// Konverterer en Fid-streng til EntityId (int) for bruk i ObservationEntityIndex.
    /// Håndterer historiske fylkes-Fid-er med understrek (f.eks. "15_2017" → 152017).
    /// </summary>
    int? FidToEntityId(string fid);

    /// <summary>
    /// Konverterer en verneområde-Fid til EntityId ved å fjerne "Naturbase VV"-prefiks.
    /// </summary>
    int? RestrictedAreaFidToEntityId(string fid);

    /// <summary>
    /// Batch-konvertering av Fid-er til EntityId-er. Ignorerer ugyldige Fid-er.
    /// </summary>
    int[] FidsToEntityIds(string[]? fids);

    /// <summary>
    /// Batch-konvertering av verneområde-Fid-er til EntityId-er. Ignorerer ugyldige Fid-er.
    /// </summary>
    int[] RestrictedAreaFidsToEntityIds(string[]? fids);
}

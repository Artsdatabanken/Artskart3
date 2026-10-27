namespace Artskart3.Core.Application.Import;

/// <summary>
/// Oppslag fra kategorikode til kategori. 15 rader i <c>Category</c>, så hele tabellen
/// ligger i minnet.
/// </summary>
public interface ICategoryLookup
{
    /// <summary>
    /// Kategorien med denne koden, eller <c>null</c>. Koden er to tegn: <c>LC</c>,
    /// <c>VU</c>, <c>SE</c> og så videre.
    /// </summary>
    CategoryRef? ByCode(string code);
}

/// <summary>
/// En kategori — rødlistestatus eller fremmedartsrisiko.
/// </summary>
/// <param name="Id">Blir <c>Observation.CategoryId</c>.</param>
/// <param name="Code">Tobokstavskoden.</param>
public sealed record CategoryRef(int Id, string Code);

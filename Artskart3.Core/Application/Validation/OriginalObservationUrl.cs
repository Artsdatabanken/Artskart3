namespace Artskart3.Core.Application.Validation;

/// <summary>
/// Finner lenken til funnsiden hos dataleverandøren i ObservationDetail.AssociatedReferences, som
/// lagres som «|»-separerte verdier. Lista inneholder også litteraturhenvisninger, DOI-er og rene
/// forsider (som alle Rovbase-referansene). De viser ikke selve funnet og hoppes over.
/// </summary>
public static class OriginalObservationUrl
{
    public static string? FromAssociatedReferences(string? associatedReferences) =>
        associatedReferences?
            .Split('|', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .FirstOrDefault(IsObservationPage);

    private static bool IsObservationPage(string value)
    {
        // Uri godtar mellomrom, så «https://a.no/1 (lest 2020)» ville blitt én ødelagt lenke.
        if (value.Any(char.IsWhiteSpace) || !WebUrl.IsValid(value)) return false;
        var uri = new Uri(value);
        var frontPage = uri.PathAndQuery == "/" && uri.Fragment.Length == 0;
        var publication = uri.Host == "doi.org" || uri.Host.EndsWith(".doi.org", StringComparison.Ordinal);
        return !frontPage && !publication;
    }
}

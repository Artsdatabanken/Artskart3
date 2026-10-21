using System.Text.RegularExpressions;

namespace Artskart3.Core.Application.Validation;

/// <summary>
/// Matrikkelnummer på formen Kommunenr-Gårdsnr/Bruksnr[/Festenr[/Seksjonsnr]],
/// f.eks. 1101-1/2. Dette er ID-en en matrikkelenhet har i filteret.
/// </summary>
public static partial class CadastralUnitId
{
    // [0-9], ikke \d: \d godtar også andre Unicode-sifre, som int.Parse avviser.
    [GeneratedRegex(@"^([0-9]{4})-([0-9]{1,5})/([0-9]{1,5})(?:/([0-9]{1,5}))?(?:/([0-9]{1,5}))?$")]
    private static partial Regex Pattern();

    /// <summary>
    /// Kanonisk form uten mellomrom og ledende nuller, eller null hvis formatet er feil.
    /// </summary>
    public static string? Normalize(string? input)
    {
        if (string.IsNullOrWhiteSpace(input)) return null;

        var match = Pattern().Match(string.Concat(input.Where(c => !char.IsWhiteSpace(c))));
        if (!match.Success) return null;

        var numbers = match.Groups.Values.Skip(2)
            .Where(g => g.Success)
            .Select(g => int.Parse(g.Value))
            .ToList();

        // Festenummer og seksjonsnummer 0 betyr at enheten ikke har noe, så 1101-1/2/0 er 1101-1/2.
        while (numbers.Count > 2 && numbers[^1] == 0)
        {
            numbers.RemoveAt(numbers.Count - 1);
        }

        return $"{match.Groups[1].Value}-{string.Join('/', numbers)}";
    }
}

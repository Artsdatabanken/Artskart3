namespace Artskart3.Core.Domain.Entities;

/// <summary>
/// Historiske institusjonskoder som fortsatt står i kildedataene. Speiling av
/// <c>Institution.Aliases</c> i Artskart 2, der de ligger som en liste på
/// institusjonsdokumentet «in case we are moving duplicate institutions».
///
/// DEN ER KRITISK, IKKE KOSMETISK. Importen slår opp alias FØRST, deretter
/// institusjonskoden, og avviser posten om ingen av dem treffer. Mangler et alias,
/// forsvinner postene fra den koden — de blir ikke feilplassert, de blir borte, og det
/// er stille datatap i en mengde ingen ser på.
///
/// EGEN TABELL, IKKE EN KOMMASEPARERT KOLONNE: oppslaget går fra alias til
/// organisasjon, og da må alias være indeksert og unik.
/// </summary>
public class OrganizationAlias
{
    /// <summary>Organisasjonen aliaset peker på. Alltid en institusjon (type 1).</summary>
    public int OrganizationId { get; set; }

    /// <summary>Den historiske koden.</summary>
    public string Alias { get; set; } = null!;

    public virtual Organization Organization { get; set; } = null!;
}

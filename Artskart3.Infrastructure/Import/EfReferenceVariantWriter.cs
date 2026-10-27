using Artskart3.Core.Application.Import;
using Artskart3.Core.Application.Persistence;
using Artskart3.Core.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Artskart3.Infrastructure.Import;

/// <summary>
/// Fører ukjente verdier opp i <c>Variants</c> på «unrecognised»-raden.
///
/// KOLONNEN ER <c>nvarchar(300)</c>, og det er en reell grense: Artskart 2 holdt listen
/// i et RavenDB-dokument uten lengdebegrensning, så en kilde som sender mange ulike
/// ugyldige verdier kan fylle den opp. Da skrives det ikke mer, og det logges som en
/// advarsel. Det er trygt fordi listen er bokføring: posten får «unrecognised» uansett,
/// og variantene på den raden kan ikke treffe i et oppslag. Det eneste som går tapt er
/// oversikten over hvilket søppel kildene sender — og det tapet er synlig i loggen.
/// Målt 14. oktober 2026 bruker funntyperaden 9 av 300 tegn, og atferdstabellen har
/// ingen slik rad ennå.
///
/// EN VERDI SOM ALT STÅR I LISTEN GIR INGEN SKRIVING.
/// </summary>
public class EfReferenceVariantWriter : IReferenceVariantWriter
{
    /// <summary>Lengden på <c>Variants</c>-kolonnen i begge tabellene.</summary>
    private const int MaksLengde = 300;

    /// <summary>
    /// Id-en en ny «unrecognised»-rad får. <c>BasisOfRecord</c> har den alt på -1, og
    /// <c>Behavior</c> får den samme konvensjonen. Id-en er <c>ValueGeneratedNever</c> i
    /// begge tabellene, så den må velges her.
    /// </summary>
    private const int UgjenkjentId = -1;

    private const string UgjenkjentNavn = "unrecognised";

    private readonly IArtsKartDbContext _context;
    private readonly ILogger<EfReferenceVariantWriter> _logger;

    /// <summary>
    /// Verdier denne instansen alt har behandlet. Sparer en spørring per post for en
    /// kilde som sender samme ugyldige verdi om og om igjen — som er det vanlige.
    /// </summary>
    private readonly HashSet<(ReferenceValueKind, string)> _behandlet = [];

    public EfReferenceVariantWriter(IArtsKartDbContext context, ILogger<EfReferenceVariantWriter> logger)
    {
        _context = context;
        _logger = logger;
    }

    public void RecordUnrecognisedValue(ReferenceValueKind kind, string value)
    {
        if (string.IsNullOrEmpty(value)) return;

        if (!_behandlet.Add((kind, value))) return;

        var skilletegn = ReferenceVariantSeparator.For(kind);

        if (value.Contains(skilletegn))
        {
            // En variant som inneholder skilletegnet kan ikke representeres i kolonnen —
            // den ville blitt to varianter ved neste lesing.
            _logger.LogWarning(
                "Verdien {Verdi} inneholder skilletegnet for {Tabell} og kan ikke føres opp som variant.",
                value,
                kind);

            return;
        }

        if (kind == ReferenceValueKind.Behavior)
        {
            SkrivAtferd(value, skilletegn);
        }
        else
        {
            SkrivFunntype(value, skilletegn);
        }
    }

    private void SkrivFunntype(string verdi, char skilletegn)
    {
        var rad = _context.Set<BasisOfRecord>().FirstOrDefault(x => x.Name == UgjenkjentNavn);

        if (rad is null)
        {
            Opprett(new BasisOfRecord { Id = UgjenkjentId, Name = UgjenkjentNavn, Variants = verdi },
                ReferenceValueKind.BasisOfRecord, verdi);

            return;
        }

        var ny = NyListe(rad.Variants, verdi, skilletegn, ReferenceValueKind.BasisOfRecord);

        if (ny is null) return;

        rad.Variants = ny;
        Lagre();

        _logger.LogInformation("Førte opp {Verdi} som ukjent funntype-variant", verdi);
    }

    private void SkrivAtferd(string verdi, char skilletegn)
    {
        var rad = _context.Set<Behavior>().FirstOrDefault(x => x.Name == UgjenkjentNavn);

        if (rad is null)
        {
            Opprett(new Behavior { Id = UgjenkjentId, Name = UgjenkjentNavn, Variants = verdi },
                ReferenceValueKind.Behavior, verdi);

            return;
        }

        var ny = NyListe(rad.Variants, verdi, skilletegn, ReferenceValueKind.Behavior);

        if (ny is null) return;

        rad.Variants = ny;
        Lagre();

        _logger.LogInformation("Førte opp {Verdi} som ukjent atferd-variant", verdi);
    }

    /// <summary>
    /// Den nye variantlisten, eller <c>null</c> når det ikke skal skrives: verdien står
    /// der alt, eller listen er full.
    /// </summary>
    private string? NyListe(string? eksisterende, string verdi, char skilletegn, ReferenceValueKind kind)
    {
        var liste = eksisterende ?? string.Empty;

        if (liste.Split(skilletegn).Contains(verdi, StringComparer.Ordinal)) return null;

        var ny = liste.Length == 0 ? verdi : liste + skilletegn + verdi;

        if (ny.Length <= MaksLengde) return ny;

        _logger.LogWarning(
            "Variantlisten for {Tabell} er full ({Lengde} av {Maks} tegn). {Verdi} føres ikke opp.",
            kind,
            liste.Length,
            MaksLengde,
            verdi);

        return null;
    }

    private void Opprett<T>(T rad, ReferenceValueKind kind, string verdi)
        where T : class
    {
        _context.Set<T>().Add(rad);
        Lagre();

        _logger.LogInformation("Opprettet {Tabell}-raden «unrecognised» og førte opp {Verdi}", kind, verdi);
    }

    /// <summary>
    /// Synkront mot en asynkron kontekst, av samme grunn som i
    /// <see cref="EfOrganizationCreator"/>: stegkjeden er synkron, og dette skjer én
    /// gang per ny ugyldig verdi — ikke per post.
    /// </summary>
    private void Lagre() => _context.SaveChangesAsync().GetAwaiter().GetResult();
}

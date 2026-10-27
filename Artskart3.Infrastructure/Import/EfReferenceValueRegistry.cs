using Artskart3.Core.Application.Import;
using Artskart3.Core.Application.Persistence;
using Artskart3.Core.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Artskart3.Infrastructure.Import;

/// <summary>
/// Funntyper og atferder med variantene sine, lastet fra indeksbasen ved første bruk.
/// 14 + 6 rader.
/// </summary>
public class EfReferenceValueRegistry : IReferenceValueRegistry
{
    /// <summary>
    /// Radene en variant aldri skal kunne peke på. Variantene på dem er en logg over
    /// hvilke ugyldige verdier kildene har sendt, ikke et oppslag.
    /// </summary>
    private static readonly string[] IkkeOppslagbare = ["unrecognised", "ukjent"];

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<EfReferenceValueRegistry> _logger;
    private readonly Lock _låse = new();

    private Dictionary<ReferenceValueKind, Oppslag>? _oppslag;

    public EfReferenceValueRegistry(
        IServiceScopeFactory scopeFactory,
        ILogger<EfReferenceValueRegistry> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public ReferenceValue? ByName(ReferenceValueKind kind, string name) =>
        string.IsNullOrWhiteSpace(name) ? null : Last()[kind].PåNavn.GetValueOrDefault(name);

    public ReferenceValue? ByVariant(ReferenceValueKind kind, string variant) =>
        string.IsNullOrEmpty(variant) ? null : Last()[kind].PåVariant.GetValueOrDefault(variant);

    public ReferenceValue? Unrecognised(ReferenceValueKind kind) => Last()[kind].Ugjenkjent;

    private Dictionary<ReferenceValueKind, Oppslag> Last()
    {
        if (_oppslag is not null) return _oppslag;

        lock (_låse)
        {
            if (_oppslag is not null) return _oppslag;

            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<IArtsKartDbContext>();

            var funntyper = context.Set<BasisOfRecord>()
                .AsNoTracking()
                .Select(x => new Rad(x.Id, x.Name, x.Variants))
                .ToList();

            var atferder = context.Set<Behavior>()
                .AsNoTracking()
                .Select(x => new Rad(x.Id, x.Name, x.Variants))
                .ToList();

            _oppslag = new Dictionary<ReferenceValueKind, Oppslag>
            {
                [ReferenceValueKind.BasisOfRecord] = Bygg(funntyper, ReferenceVariantSeparator.BasisOfRecord),
                [ReferenceValueKind.Behavior] = Bygg(atferder, ReferenceVariantSeparator.Behavior)
            };

            _logger.LogInformation(
                "Lastet referanseverdier: {Funntyper} funntyper ({FunntypeVarianter} varianter), "
                + "{Atferder} atferder ({AtferdVarianter} varianter)",
                funntyper.Count,
                _oppslag[ReferenceValueKind.BasisOfRecord].PåVariant.Count,
                atferder.Count,
                _oppslag[ReferenceValueKind.Behavior].PåVariant.Count);

            return _oppslag;
        }
    }

    /// <summary>
    /// Bygger de to ordbøkene for én tabell.
    ///
    /// VARIANTENE TRIMMES IKKE. Skilletegnet er en del av verdien i noen varianter —
    /// <c>BasisOfRecord</c> har «specimen, dead» og «observation, dead», som er
    /// kommaseparerte strenger i en kommaseparert kolonne. Oppdelingen gir da «specimen»
    /// og « dead». Trimmet vi, ville « dead» blitt «dead» og begynt å treffe poster
    /// Artskart 2 aldri har gjenkjent — Artskart 2 leser denne listen fra RavenDB, der
    /// den er en liste og ikke en streng. Utrimmet er leddet uoppnåelig, og det er det
    /// riktige: en variant som er borte skal være borte, ikke bli en ny regel.
    ///
    /// DELTE VARIANTER: to rader som oppgir samme variant er en feil i dataene.
    /// Artskart 2 bruker <c>SingleOrDefault</c> og kaster — et unntak som feller hele
    /// nodeimporten. Her vinner den laveste Id-en, og det logges.
    /// </summary>
    private Oppslag Bygg(List<Rad> rader, char skilletegn)
    {
        var påNavn = rader
            .Where(x => !string.IsNullOrWhiteSpace(x.Name))
            .GroupBy(x => x.Name, StringComparer.Ordinal)
            .ToDictionary(
                x => x.Key,
                x => Til(x.OrderBy(y => y.Id).First()),
                StringComparer.Ordinal);

        var varianter = rader
            .Where(x => !IkkeOppslagbare.Contains(x.Name, StringComparer.OrdinalIgnoreCase))
            .SelectMany(x => (x.Variants ?? string.Empty)
                .Split(skilletegn)
                .Where(v => v.Length > 0)
                .Select(v => (Variant: v, Rad: x)))
            .GroupBy(x => x.Variant, StringComparer.Ordinal)
            .ToList();

        foreach (var delt in varianter.Where(x => x.Select(y => y.Rad.Id).Distinct().Count() > 1))
        {
            _logger.LogWarning(
                "Varianten {Variant} står på flere rader ({Rader}). Den laveste Id-en vinner.",
                delt.Key,
                string.Join(", ", delt.Select(x => x.Rad.Name).Distinct()));
        }

        return new Oppslag
        {
            PåNavn = påNavn,
            PåVariant = varianter.ToDictionary(
                x => x.Key,
                x => Til(x.OrderBy(y => y.Rad.Id).First().Rad),
                StringComparer.Ordinal),
            Ugjenkjent = påNavn.GetValueOrDefault("unrecognised")
        };
    }

    private static ReferenceValue Til(Rad rad) => new(rad.Id, rad.Name);

    private sealed record Rad(int Id, string Name, string? Variants);

    private sealed class Oppslag
    {
        public required Dictionary<string, ReferenceValue> PåNavn { get; init; }

        public required Dictionary<string, ReferenceValue> PåVariant { get; init; }

        public ReferenceValue? Ugjenkjent { get; init; }
    }
}

/// <summary>
/// Skilletegnet i <c>Variants</c>-kolonnen, som ikke er det samme i de to tabellene.
/// Står slik i Artskart 2s <c>SqlBasisOfRecord</c> og <c>SqlBehavior</c>.
/// </summary>
public static class ReferenceVariantSeparator
{
    public const char BasisOfRecord = ',';

    public const char Behavior = '|';

    public static char For(ReferenceValueKind kind) =>
        kind == ReferenceValueKind.Behavior ? Behavior : BasisOfRecord;
}

using Artskart3.Core.Application.Import;
using Artskart3.Core.Application.Persistence;
using Artskart3.Core.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Artskart3.Infrastructure.Import;

/// <summary>
/// Organisasjonsregisteret, lastet fra indeksbasen ved første bruk.
///
/// HELE TABELLEN I MINNET. Den er 25 943 rader i produksjonslikt datasett — 57
/// utgivere, 7 758 datasett, 18 128 prosjekter. Oppslaget skjer per post, altså
/// titalls millioner ganger per full import, og en spørring per post er ikke verdt
/// noe. Artskart 2 bufrer det samme i tre felt på selve steget, men bare siste treff.
///
/// KODENE SAMMENLIGNES UTEN HENSYN TIL BOKSTAVSTØRRELSE. Artskart 2 slår opp i RavenDB,
/// som er bokstavstørrelsesuavhengig som standard, og versalerer kilden først. Dagens
/// data har ingen utgiverkoder som kolliderer når man ser bort fra bokstavstørrelse.
/// </summary>
public class EfOrganizationRegistry : IOrganizationRegistry
{
    /// <summary>Publisher — Artskart 2s <c>Institution</c>.</summary>
    private const int UtgiverType = 1;

    /// <summary>Dataset — Artskart 2s <c>Collection</c>.</summary>
    private const int DatasettType = 2;

    /// <summary>Project — Artskart 2s <c>Dataset</c>.</summary>
    private const int ProsjektType = 3;

    /// <summary>
    /// Nøkkelen er forelderens Id og en kode eller et navn. Teksten sammenlignes uten
    /// hensyn til bokstavstørrelse, slik RavenDB gjorde det.
    /// </summary>
    private static readonly NøkkelLikhet NøkkelSammenligner = new();

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<EfOrganizationRegistry> _logger;
    private readonly Lock _låse = new();

    private Oppslag? _oppslag;

    public EfOrganizationRegistry(IServiceScopeFactory scopeFactory, ILogger<EfOrganizationRegistry> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public OrganizationRef? PublisherByAlias(string alias) =>
        string.IsNullOrWhiteSpace(alias) ? null : Last().UtgiverPåAlias.GetValueOrDefault(alias.Trim());

    public OrganizationRef? PublisherByCode(string code) =>
        string.IsNullOrWhiteSpace(code) ? null : Last().UtgiverPåKode.GetValueOrDefault(code.Trim());

    public OrganizationRef? DatasetByCode(int publisherOrgId, string code) =>
        string.IsNullOrWhiteSpace(code)
            ? null
            : Last().DatasettPåKode.GetValueOrDefault((publisherOrgId, code.Trim()));

    public OrganizationRef? ProjectBySourceId(int datasetOrgId, string sourceDatasetId) =>
        string.IsNullOrWhiteSpace(sourceDatasetId)
            ? null
            : Last().ProsjektPåKildeId.GetValueOrDefault((datasetOrgId, sourceDatasetId.Trim()));

    public OrganizationRef? ProjectByName(int datasetOrgId, string name) =>
        string.IsNullOrWhiteSpace(name)
            ? null
            : Last().ProsjektPåNavn.GetValueOrDefault((datasetOrgId, name.Trim()));

    private Oppslag Last()
    {
        if (_oppslag is not null) return _oppslag;

        lock (_låse)
        {
            if (_oppslag is not null) return _oppslag;

            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<IArtsKartDbContext>();

            var rader = context.Set<Organization>()
                .AsNoTracking()
                .Where(x => x.OrganizationTypeId == UtgiverType
                            || x.OrganizationTypeId == DatasettType
                            || x.OrganizationTypeId == ProsjektType)
                .Select(x => new Rad(
                    x.Id,
                    x.Code,
                    x.Name,
                    x.ParentId,
                    x.OrganizationTypeId,
                    x.SourceDatasetId))
                .ToList();

            var alias = context.Set<OrganizationAlias>()
                .AsNoTracking()
                .Select(x => new { x.OrganizationId, x.Alias })
                .ToList();

            var utgivere = rader
                .Where(x => x.TypeId == UtgiverType)
                .ToDictionary(x => x.Id, Til);

            var datasett = rader.Where(x => x.TypeId == DatasettType && x.ParentId is not null).ToList();
            var prosjekter = rader.Where(x => x.TypeId == ProsjektType && x.ParentId is not null).ToList();

            var navnedubletter = prosjekter
                .Where(x => !string.IsNullOrWhiteSpace(x.Name))
                .GroupBy(x => (x.ParentId!.Value, x.Name), NøkkelSammenligner)
                .Count(x => x.Count() > 1);

            _oppslag = new Oppslag
            {
                UtgiverPåKode = utgivere.Values
                    .Where(x => !string.IsNullOrWhiteSpace(x.Code))
                    .GroupBy(x => x.Code!, StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(x => x.Key, x => x.OrderBy(y => y.Id).First(), StringComparer.OrdinalIgnoreCase),

                UtgiverPåAlias = alias
                    .Where(x => !string.IsNullOrWhiteSpace(x.Alias) && utgivere.ContainsKey(x.OrganizationId))
                    .GroupBy(x => x.Alias, StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(
                        x => x.Key,
                        x => utgivere[x.Min(y => y.OrganizationId)],
                        StringComparer.OrdinalIgnoreCase),

                DatasettPåKode = Grupper(datasett.Where(x => !string.IsNullOrWhiteSpace(x.Code)), x => x.Code!),

                ProsjektPåKildeId = Grupper(
                    prosjekter.Where(x => !string.IsNullOrWhiteSpace(x.SourceDatasetId)),
                    x => x.SourceDatasetId!),

                // Deler flere prosjekter navnet under samme datasett, velges det eldste.
                // Det er ikke et valg fordi det er riktig, men fordi navnet ikke kan
                // skille dem — og da er det bedre at utfallet er det samme hver gang enn
                // at det følger radrekkefølgen.
                ProsjektPåNavn = Grupper(
                    prosjekter.Where(x => !string.IsNullOrWhiteSpace(x.Name)),
                    x => x.Name)
            };

            _logger.LogInformation(
                "Lastet organisasjonsregisteret: {Utgivere} utgivere ({Alias} alias), {Datasett} datasett, "
                + "{Prosjekter} prosjekter ({MedKildeId} med datasetID)",
                utgivere.Count,
                _oppslag.UtgiverPåAlias.Count,
                _oppslag.DatasettPåKode.Count,
                prosjekter.Count,
                _oppslag.ProsjektPåKildeId.Count);

            if (navnedubletter > 0)
            {
                _logger.LogWarning(
                    "{Antall} prosjektnavn finnes mer enn én gang under samme datasett. Poster "
                    + "uten datasetID havner på det eldste av dem.",
                    navnedubletter);
            }

            return _oppslag;
        }
    }

    private static Dictionary<(int, string), OrganizationRef> Grupper(
        IEnumerable<Rad> rader,
        Func<Rad, string> nøkkel) =>
        rader
            .GroupBy(x => (x.ParentId!.Value, nøkkel(x)), NøkkelSammenligner)
            .ToDictionary(x => x.Key, x => Til(x.OrderBy(y => y.Id).First()), NøkkelSammenligner);

    private static OrganizationRef Til(Rad rad) => new(rad.Id, rad.Code, rad.Name, rad.ParentId);

    private sealed record Rad(int Id, string? Code, string Name, int? ParentId, int TypeId, string? SourceDatasetId);

    private sealed class NøkkelLikhet : IEqualityComparer<(int Forelder, string Tekst)>
    {
        public bool Equals((int Forelder, string Tekst) x, (int Forelder, string Tekst) y) =>
            x.Forelder == y.Forelder && StringComparer.OrdinalIgnoreCase.Equals(x.Tekst, y.Tekst);

        public int GetHashCode((int Forelder, string Tekst) obj) =>
            HashCode.Combine(obj.Forelder, StringComparer.OrdinalIgnoreCase.GetHashCode(obj.Tekst));
    }

    private sealed class Oppslag
    {
        public required Dictionary<string, OrganizationRef> UtgiverPåKode { get; init; }

        public required Dictionary<string, OrganizationRef> UtgiverPåAlias { get; init; }

        public required Dictionary<(int, string), OrganizationRef> DatasettPåKode { get; init; }

        public required Dictionary<(int, string), OrganizationRef> ProsjektPåKildeId { get; init; }

        public required Dictionary<(int, string), OrganizationRef> ProsjektPåNavn { get; init; }
    }
}

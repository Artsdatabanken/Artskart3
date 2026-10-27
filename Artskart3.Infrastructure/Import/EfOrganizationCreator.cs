using Artskart3.Core.Application.Import;
using Artskart3.Core.Application.Persistence;
using Artskart3.Core.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Artskart3.Infrastructure.Import;

/// <summary>
/// Oppretter manglende organisasjoner direkte i <c>Organization</c>.
///
/// DEN SLÅR OPP FØRST, og det er ikke et belte i tillegg til seler. Artskart 2 gjør det
/// samme — <c>AddCollection</c> laster dokumentet før den lager det, <c>AddDataset</c>
/// spør indeksen — og her finnes en konkret grunn: <see cref="EfOrganizationRegistry"/>
/// laster øyeblikksbildet sitt én gang per prosess. En rad opprettet under
/// nodekjøring 1 står ikke i det bildet når nodekjøring 2 begynner, og uten dette
/// oppslaget ville den blitt opprettet på nytt. <c>Organization</c> har ingen
/// unikhetsindeks på <c>(ParentId, Code)</c> som ville stoppet det.
///
/// RADEN SKRIVES MED EN GANG, ikke samlet opp til slutt. Planen (4.9.1) anbefaler å
/// samle nye rader og skrive dem én gang per staging-runde, og det er riktig for
/// varianter — men en organisasjon må ha en ekte <c>Id</c> før observasjonen som peker
/// på den kan skrives, og den Id-en kommer fra databasen. En ny kode er dessuten en
/// engangshendelse per kode per node, ikke noe som skjer per post.
///
/// NAVNENE FØLGER ARTSKART 2 så en rad opprettet av Artskart 3 ser ut som en rad
/// opprettet av Artskart 2: et datasett heter «{kode} hos {utgiver}», et prosjekt heter
/// det kilden kaller det.
///
/// <c>ExternalId</c> SETTES IKKE. Den peker på RavenDB-dokumentet raden ble speilet
/// fra, og en rad Artskart 3 oppretter har ikke noe slikt dokument. At feltet er tomt
/// er dermed opplysningen: denne raden kom fra importen, ikke fra speilingen.
/// </summary>
public class EfOrganizationCreator : IOrganizationCreator
{
    /// <summary>Dataset — Artskart 2s <c>Collection</c>.</summary>
    private const int DatasettType = 2;

    /// <summary>Project — Artskart 2s <c>Dataset</c>.</summary>
    private const int ProsjektType = 3;

    private readonly IArtsKartDbContext _context;
    private readonly TimeProvider _klokke;
    private readonly ILogger<EfOrganizationCreator> _logger;

    public EfOrganizationCreator(
        IArtsKartDbContext context,
        TimeProvider klokke,
        ILogger<EfOrganizationCreator> logger)
    {
        _context = context;
        _klokke = klokke;
        _logger = logger;
    }

    public OrganizationRef CreateDataset(OrganizationRef publisher, string code)
    {
        ArgumentNullException.ThrowIfNull(publisher);

        // Kodene sammenlignes av databasen, og standardkollasjonen er
        // bokstavstørrelsesuavhengig — som i RavenDB.
        var eksisterende = Finn(x =>
            x.OrganizationTypeId == DatasettType && x.ParentId == publisher.Id && x.Code == code);

        if (eksisterende is not null) return eksisterende;

        var rad = Ny(DatasettType, publisher.Id);

        rad.Code = code;
        rad.Name = $"{code} hos {publisher.Name}";

        Lagre(rad);

        _logger.LogInformation(
            "Opprettet datasett {Kode} ({Id}) under utgiver {Utgiver}", code, rad.Id, publisher.Code);

        return Til(rad);
    }

    public OrganizationRef CreateProject(OrganizationRef dataset, string? sourceDatasetId, string name)
    {
        ArgumentNullException.ThrowIfNull(dataset);

        // Artskart 2 krever treff på både datasett-ID og navn før den gjenbruker en
        // rad. Samme krav her: to prosjekter kan dele navn under samme datasett, og da
        // er det datasetID-en som skiller dem.
        var eksisterende = Finn(x =>
            x.OrganizationTypeId == ProsjektType
            && x.ParentId == dataset.Id
            && x.SourceDatasetId == sourceDatasetId
            && x.Name == name);

        if (eksisterende is not null) return eksisterende;

        var rad = Ny(ProsjektType, dataset.Id);

        // Prosjekter har ingen kode — det har de ikke i dagens 18 128 rader heller.
        rad.Name = name;
        rad.SourceDatasetId = sourceDatasetId;

        Lagre(rad);

        _logger.LogInformation(
            "Opprettet prosjekt {Navn} ({Id}, datasetID {KildeId}) under datasett {Datasett}",
            name,
            rad.Id,
            sourceDatasetId ?? "(ingen)",
            dataset.Code);

        return Til(rad);
    }

    private OrganizationRef? Finn(
        System.Linq.Expressions.Expression<Func<Organization, bool>> vilkår)
    {
        var rad = _context.Set<Organization>()
            .AsNoTracking()
            .Where(vilkår)
            .OrderBy(x => x.Id)
            .Select(x => new { x.Id, x.Code, x.Name, x.ParentId })
            .FirstOrDefault();

        return rad is null ? null : new OrganizationRef(rad.Id, rad.Code, rad.Name, rad.ParentId);
    }

    private Organization Ny(int typeId, int parentId)
    {
        var nå = _klokke.GetUtcNow().UtcDateTime;

        return new Organization
        {
            OrganizationTypeId = typeId,
            ParentId = parentId,
            Name = string.Empty,
            DateCreated = nå,
            DateModified = nå,
            CreatedAt = nå,
            UpdatedAt = nå
        };
    }

    private static OrganizationRef Til(Organization rad) =>
        new(rad.Id, rad.Code, rad.Name, rad.ParentId);

    /// <summary>
    /// Skriver raden og venter på Id-en.
    ///
    /// SYNKRONT MOT EN ASYNKRON KONTEKST, fordi <c>IImportStep</c> er synkron: hele
    /// stegkjeden er ren tolkning uten I/O, og dette er det ene unntaket. Det er trygt
    /// her — importen kjører i en worker uten synkroniseringskontekst — og det skjer én
    /// gang per ny kode, ikke per post.
    /// </summary>
    private void Lagre(Organization rad)
    {
        _context.Set<Organization>().Add(rad);
        _context.SaveChangesAsync().GetAwaiter().GetResult();
    }
}

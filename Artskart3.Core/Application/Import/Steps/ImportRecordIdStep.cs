using Artskart3.Core.Application.Import.Helpers;
using Artskart3.Core.Application.Import.Models;

namespace Artskart3.Core.Application.Import.Steps;

/// <summary>
/// Etablerer postens identitet og plasserer den hos en utgiver, et datasett og
/// eventuelle prosjekter. Port av Artskart 2s <c>ImportRecordId</c>.
///
/// DET FØRSTE STEGET, OG DET ENESTE EN SLETTET POST KJØRER. <c>ProxyId</c> er nøkkelen
/// alt annet henger på: uten den finnes det ingen observasjon å oppdatere og ingen rad
/// å slette. Derfor er det et <see cref="IImportIdentityStep"/>.
///
/// NAVNENE SKIFTER BETYDNING MELLOM LØSNINGENE. DwC <c>collectionCode</c> blir et
/// <c>Organization</c> av type 2, som Artskart 3 kaller <em>Dataset</em> og Artskart 2
/// kalte <em>Collection</em>. DwC <c>datasetID</c> blir type 3, som Artskart 3 kaller
/// <em>Project</em>. Se <see cref="IOrganizationRegistry"/> for hele tabellen.
///
/// STEGET HAR TILSTAND SOM HØRER TIL ÉN NODEKJØRING, av samme grunn som
/// <c>ImportCheckUniqueIdStep</c>: det husker organisasjonene det har opprettet. De
/// finnes ikke i registerets øyeblikksbilde, så uten dette minnet ville post nummer to
/// med samme ukjente kode opprettet raden på nytt. Kjeden må bygges per nodekjøring.
/// </summary>
public class ImportRecordIdStep : IImportIdentityStep
{
    private const int MaksKodelengde = 100;
    private const int MaksKatalognummerlengde = 200;
    private const int MaksOccurrenceIdLengde = 255;

    private readonly IOrganizationRegistry _register;
    private readonly IOrganizationCreator? _oppretter;

    /// <summary>
    /// Datasett og prosjekter steget har slått opp eller opprettet i denne kjøringen.
    ///
    /// ARTSKART 2 HAR TRE SLIKE BUFRE for å slippe å gå til RavenDB per post. Her er
    /// registeret allerede en ordbok i minnet, så det er ikke grunnen: bufferet finnes
    /// fordi en rad <em>steget selv</em> har opprettet ikke står i registerets
    /// øyeblikksbilde.
    /// </summary>
    private readonly Dictionary<string, OrganizationRef> _datasett = new(StringComparer.OrdinalIgnoreCase);

    private readonly Dictionary<string, OrganizationRef> _prosjekter = new(StringComparer.OrdinalIgnoreCase);

    public ImportRecordIdStep(IOrganizationRegistry register, IOrganizationCreator? oppretter = null)
    {
        _register = register;
        _oppretter = oppretter;
    }

    public ImportStepType StepType => ImportStepType.Pre;

    /// <summary>
    /// Om steget får opprette organisasjoner det ikke finner. Er den <c>false</c>,
    /// kjører importen i opprett-ikke-modus — se <see cref="IOrganizationCreator"/>.
    /// </summary>
    public bool CanCreate => _oppretter is not null;

    /// <summary>Antall organisasjoner steget har opprettet i denne kjøringen.</summary>
    public int CreatedCount { get; private set; }

    public ImportRecordResult ValidateAndImport(
        CacheRecord record,
        ImportedObservation observation,
        ImportContext context)
    {
        ArgumentNullException.ThrowIfNull(record);
        ArgumentNullException.ThrowIfNull(observation);

        if (!string.IsNullOrWhiteSpace(record.OccurrenceID))
        {
            observation.OccurrenceId = ImportValueHelper.Left(record.OccurrenceID.Trim(), MaksOccurrenceIdLengde);
        }

        if (string.IsNullOrEmpty(record.CatalogNumber?.Trim()))
        {
            return ImportRecordResult.Rejected(
                RecordValidationCode.CatalogNumberIsMissing, RecordNotificationCode.Ok, string.Empty);
        }

        observation.CatalogNumber = ImportValueHelper.Left(record.CatalogNumber, MaksKatalognummerlengde);

        if (string.IsNullOrWhiteSpace(record.InstitutionCode))
        {
            return ImportRecordResult.Rejected(
                RecordValidationCode.InstitutionCodeMissing, RecordNotificationCode.Ok, string.Empty);
        }

        // Versaler, så kutt, så trim — rekkefølgen er Artskart 2s.
        var kildekode = ImportValueHelper.Left(record.InstitutionCode.ToUpperInvariant(), MaksKodelengde)!.Trim();

        var datasettkode = record.CollectionCode is not null
            ? ImportValueHelper.Left(record.CollectionCode.ToUpperInvariant(), MaksKodelengde)!.Trim()
            : string.Empty;

        // Mangler samlingskoden, er den institusjonskoden. Det er ikke en nødløsning,
        // det er dagens datamodell: en institusjon uten samlinger har én samling som
        // heter det samme som den selv.
        if (string.IsNullOrWhiteSpace(datasettkode)) datasettkode = kildekode;

        var utgiver = _register.PublisherByAlias(kildekode) ?? _register.PublisherByCode(kildekode);

        if (utgiver is null)
        {
            // Identiteten settes likevel, av kildens egne koder: en avvist post skal
            // kunne slettes fra indexen hvis den har stått der før.
            observation.InstitutionCode = kildekode;
            observation.CollectionCode = datasettkode;
            SettIdentitet(observation);

            return ImportRecordResult.Rejected(
                RecordValidationCode.InstitutionCodeNotKnown, RecordNotificationCode.Ok, kildekode);
        }

        // DEN LAGREDE KODEN ER UTGIVERENS, IKKE KILDENS. Slår et alias til, er det den
        // kanoniske koden som havner på observasjonen — og i ProxyId.
        observation.InstitutionCode = utgiver.Code;
        observation.InstitutionOrgId = utgiver.Id;

        var datasett = FinnEllerOpprettDatasett(utgiver, datasettkode, out var opprettetDatasett);

        if (datasett is null)
        {
            return ImportRecordResult.Rejected(
                RecordValidationCode.OrganizationNotCreated,
                RecordNotificationCode.Ok,
                $"Datasett {utgiver.Code}/{datasettkode} finnes ikke, og opprettelse er slått av.");
        }

        observation.CollectionCode = datasett.Code;
        observation.DatasetOrgId = datasett.Id;

        SettIdentitet(observation);

        return KoblProsjekter(record, observation, datasett, opprettetDatasett);
    }

    /// <summary>
    /// <c>ProxyId</c> — av observasjonens koder, ikke kildens, og alltid i små
    /// bokstaver. Port av Artskart 2s <c>Observation.UpdateId()</c>.
    /// </summary>
    private static void SettIdentitet(ImportedObservation observation) =>
        observation.ProxyId = DarwinCoreHelper.CleanDarwinCoreId(
                observation.InstitutionCode,
                observation.CollectionCode,
                observation.CatalogNumber,
                observation.OccurrenceId)
            .ToLowerInvariant();

    private OrganizationRef? FinnEllerOpprettDatasett(OrganizationRef utgiver, string kode, out bool opprettet)
    {
        opprettet = false;

        var nøkkel = utgiver.Id + "_" + kode;

        if (_datasett.TryGetValue(nøkkel, out var bufret)) return bufret;

        var funnet = _register.DatasetByCode(utgiver.Id, kode);

        if (funnet is null && _oppretter is not null)
        {
            // Artskart 2 oppretter samlingen med små bokstaver selv om oppslaget skjedde
            // på den versalerte koden.
            funnet = _oppretter.CreateDataset(utgiver, kode.ToLowerInvariant());
            CreatedCount++;
            opprettet = true;
        }

        if (funnet is not null) _datasett[nøkkel] = funnet;

        return funnet;
    }

    /// <summary>
    /// Kobler posten til prosjektene kilden oppgir i <c>datasetID</c> og
    /// <c>datasetName</c>.
    ///
    /// BEGGE FELT ER FLERVERDIFELT, skilt med <c>|</c>. Oppgir kilden ID-er, må antallet
    /// navn stemme — ellers vet vi ikke hvilket navn som hører til hvilken ID, og posten
    /// avvises i stedet for å gjette.
    ///
    /// ARTSKART 2 SKREV <c>Datasets = [0]</c> for en post uten prosjekt. Her står lista
    /// tom: <c>ObservationProject</c> er en koblingstabell, og en rad som peker på
    /// organisasjon 0 ville vært en ødelagt fremmednøkkel, ikke en opplysning.
    /// </summary>
    private ImportRecordResult KoblProsjekter(
        CacheRecord record,
        ImportedObservation observation,
        OrganizationRef datasett,
        bool opprettetDatasett)
    {
        var kildeIder = DelOpp(record.DatasetId);
        var navn = DelOpp(record.DatasetName);

        if (kildeIder.Count > 0 && kildeIder.Count != navn.Count)
        {
            return ImportRecordResult.Rejected(
                RecordValidationCode.DatasetNameMissing,
                RecordNotificationCode.CountOfDatasetIdsAndNamesDiffers,
                $"DatasetIds: {record.DatasetId} -> datasetNames: {record.DatasetName}");
        }

        var opprettetProsjekt = false;

        if (kildeIder.Count > 0)
        {
            for (var i = 0; i < kildeIder.Count; i++)
            {
                var prosjekt = FinnEllerOpprettProsjekt(datasett, kildeIder[i], navn[i], ref opprettetProsjekt);

                if (prosjekt is null)
                {
                    return ImportRecordResult.Rejected(
                        RecordValidationCode.OrganizationNotCreated,
                        RecordNotificationCode.Ok,
                        $"Prosjekt {kildeIder[i]} ({navn[i]}) finnes ikke, og opprettelse er slått av.");
                }

                observation.ProjectOrgIds.Add(prosjekt.Id);
            }
        }
        else if (navn.Count > 0)
        {
            foreach (var enkeltnavn in navn)
            {
                var prosjekt = FinnEllerOpprettProsjekt(datasett, null, enkeltnavn, ref opprettetProsjekt);

                if (prosjekt is null)
                {
                    return ImportRecordResult.Rejected(
                        RecordValidationCode.OrganizationNotCreated,
                        RecordNotificationCode.Ok,
                        $"Prosjekt {enkeltnavn} finnes ikke, og opprettelse er slått av.");
                }

                observation.ProjectOrgIds.Add(prosjekt.Id);
            }
        }

        // Lagres re-joinet, altså uten de tomme leddene kilden kan ha sendt.
        if (kildeIder.Count > 0) observation.DatasetId = string.Join("|", kildeIder);
        if (navn.Count > 0) observation.DatasetName = string.Join("|", navn);

        // Artskart 2 returnerer CollectionCodeAdded for hver post, også de der ingenting
        // ble opprettet. Meldingen leses ingen steder for en godkjent post, men en kode
        // som betyr noe er bedre enn en som ikke gjør det.
        if (opprettetDatasett) return ImportRecordResult.Ok(RecordNotificationCode.CollectionCodeAdded);

        return opprettetProsjekt
            ? ImportRecordResult.Ok(RecordNotificationCode.DatasetAdded)
            : ImportRecordResult.Ok();
    }

    /// <summary>
    /// Prosjektet med denne ID-en eller dette navnet, opprettet om det ikke finnes.
    ///
    /// ID-EN GÅR FØRST. Navnet er ikke entydig — 902 par prosjekter deler navn under
    /// samme datasett i dagens data — så et oppslag på navn kan slå feil, mens
    /// <c>datasetID</c> er kildens egen nøkkel.
    /// </summary>
    private OrganizationRef? FinnEllerOpprettProsjekt(
        OrganizationRef datasett,
        string? kildeId,
        string navn,
        ref bool opprettet)
    {
        var nøkkel = datasett.Id + "_" + (kildeId ?? "navn:" + navn);

        if (_prosjekter.TryGetValue(nøkkel, out var bufret)) return bufret;

        var funnet = kildeId is not null
            ? _register.ProjectBySourceId(datasett.Id, kildeId) ?? _register.ProjectByName(datasett.Id, navn)
            : _register.ProjectByName(datasett.Id, navn);

        if (funnet is null && _oppretter is not null)
        {
            funnet = _oppretter.CreateProject(datasett, kildeId, navn);
            CreatedCount++;
            opprettet = true;
        }

        if (funnet is not null) _prosjekter[nøkkel] = funnet;

        return funnet;
    }

    /// <summary>
    /// Deler et flerverdifelt på <c>|</c>, trimmer leddene og kaster de tomme.
    /// </summary>
    private static List<string> DelOpp(string? verdi) =>
        string.IsNullOrEmpty(verdi)
            ? []
            : verdi.Split('|', StringSplitOptions.RemoveEmptyEntries)
                .Select(x => x.Trim())
                .Where(x => x.Length > 0)
                .ToList();
}

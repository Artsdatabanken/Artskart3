using Artskart3.Core.Application.Import.Writing;

namespace Artskart3.Workers.Import;

/// <summary>
/// Hangfire-inngangen til importen. Tynn med vilje: alt arbeidet ligger i
/// <see cref="ImportService"/>, og denne klassen finnes for at Hangfire skal ha noe å
/// kalle.
///
/// IKKE REGISTRERT SOM TILBAKEVENDENDE JOBB. Planen (4.6) vil ha harvest → import →
/// vedlikehold som en kjede, men importen skriver, og maskeringen av sensitive arter
/// (fase 8) finnes ikke ennå. Fram til den gjør det, startes jobben med vilje — fra
/// Hangfire-dashbordet eller fra en test — og aldri av en cron.
///
/// KØEN SKAL HA ÉN ARBEIDER, som <c>import_service_process</c> i Artskart 2. To
/// samtidige kjøringer ville skrevet til samme staging-navn og delt lokalitetsbuffer
/// uten lås.
///
/// VIKTIG — ObservationEntityIndex har en columnstore-indeks (IX_OEI_Columnstore).
/// Det legger noen føringer på hvordan daglige batcher må skrives:
///
/// - Bruk bulk-insert med minst 102 400 rader per setning. Da går radene rett inn i
///   en komprimert rowgroup. Mindre batcher havner i delta store (en vanlig b-tre)
///   og venter til ~1M rader har samlet seg før de komprimeres.
/// - Foretrekk append fremfor UPDATE. En UPDATE mot columnstore markerer den gamle
///   raden i delete-bitmappen og setter inn en ny versjon i delta store — den gamle
///   raden opptar fortsatt plass til neste rebuild. Skal eksisterende observasjoner
///   revideres, er bulk delete + insert billigere enn UPDATE-in-place.
/// - Kjør DatabaseMaintenanceJob (REORGANIZE) etter importen, se den klassen.
///
/// MERK: ObservationTaxonHierarchy har i dag ingen oppdateringssti — den populeres
/// kun én gang i migrasjon 20260702113806. Den må også vedlikeholdes herfra, og det
/// er fase 7 (<c>DerivedTableMaintainer</c>); ellers blir taksonfiltrering stående på
/// et frosset øyeblikksbilde.
/// </summary>
public class ImportJob
{
    private readonly ImportService _importService;
    private readonly ILogger<ImportJob> _logger;

    public ImportJob(ImportService importService, ILogger<ImportJob> logger)
    {
        _importService = importService;
        _logger = logger;
    }

    /// <summary>
    /// Den ordinære importen: alle nodene konfigurasjonen peker på, hver av dem fullt
    /// ut.
    /// </summary>
    public async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        var resultater = await _importService.ImportActiveNodesAsync(cancellationToken);

        foreach (var resultat in resultater)
        {
            _logger.LogInformation(
                "Node {Node}: {Lest} lest, {Importerte} importert, {Avviste} avvist, {Slettede} slettet, "
                + "{Ødelagte} ulesbare, {Runder} runder, {Sekunder} s",
                resultat.NodeId,
                resultat.Read,
                resultat.Imported,
                resultat.Rejected,
                resultat.Deleted,
                resultat.Malformed,
                resultat.Rounds,
                Math.Round(resultat.Elapsed.TotalSeconds, 1));
        }
    }

    /// <summary>
    /// Én node. Egen inngang fordi det er den vanligste operasjonelle situasjonen —
    /// å reimportere én datakilde uten å røre resten — og fordi det er slik en node
    /// verifiseres mot Artskart 2.
    /// </summary>
    /// <param name="nodeId">Noden.</param>
    /// <param name="changedAfter">
    /// Vannmerke for inkrementell lesing. <c>null</c> leser hele noden.
    /// </param>
    /// <param name="cancellationToken">Avbrytes mellom poster, ikke midt i en runde.</param>
    public async Task<ImportNodeResult> ImportNodeAsync(
        int nodeId,
        DateTime? changedAfter,
        CancellationToken cancellationToken) =>
        await _importService.ImportNodeAsync(nodeId, changedAfter, cancellationToken);

    /// <summary>
    /// Leser én node fra cachen og logger hva som finnes der. Skriver ingenting.
    ///
    /// Egen inngang fordi den hører til verifiseringen av lesestien: den kjøres manuelt
    /// mot en node, og er fortsatt den billigste måten å se om cachen svarer som
    /// forventet.
    /// </summary>
    public async Task ReadNodeAsync(int nodeId, int? maxRecords, CancellationToken cancellationToken)
    {
        await _importService.ReadNodeAsync(nodeId, maxRecords, cancellationToken);
    }
}
